using System.Data.Odbc;
using System.Data.OleDb;
using System.Diagnostics;
using System.Text.Json;
using Execute.Sql.Paradox.Models;
using Microsoft.Extensions.Configuration;

namespace Execute.Sql.Paradox.Services;

public class ConnectionService
{
    private readonly string _configPath;
    private readonly string _sqlCommandsPath;
    private readonly string _odbcConsoleExecutable;

    public ConnectionService(IWebHostEnvironment env, IConfiguration configuration)
    {
        _configPath = Path.Combine(env.ContentRootPath, "config");
        _sqlCommandsPath = Path.Combine(env.ContentRootPath, "sqlComands");
        Directory.CreateDirectory(_configPath);
        Directory.CreateDirectory(_sqlCommandsPath);

        var consolePath = configuration["OdbcConsole:ExecutablePath"] ?? @"OdbcConsole\OdbcConsole.exe";
        _odbcConsoleExecutable = Path.IsPathRooted(consolePath)
            ? consolePath
            : Path.Combine(env.ContentRootPath, consolePath);
    }

    public List<ConnectionConfig> GetAllConnections()
    {
        var connections = new List<ConnectionConfig>();
        foreach (var file in Directory.GetFiles(_configPath, "*.json"))
        {
            var json = File.ReadAllText(file);
            var config = JsonSerializer.Deserialize<ConnectionConfig>(json);
            if (config != null)
                connections.Add(config);
        }
        return connections;
    }

    public ConnectionConfig? GetConnection(string name)
    {
        var file = GetConnectionFilePath(name);
        if (!File.Exists(file))
            return null;
        var json = File.ReadAllText(file);
        return JsonSerializer.Deserialize<ConnectionConfig>(json);
    }

    public bool ConnectionExists(string name)
    {
        return File.Exists(GetConnectionFilePath(name));
    }

    public void SaveConnection(ConnectionConfig config)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(config, options);
        File.WriteAllText(GetConnectionFilePath(config.Name), json);
    }

    public void DeleteConnection(string name)
    {
        var file = GetConnectionFilePath(name);
        if (File.Exists(file))
            File.Delete(file);
    }

    public (bool success, string error) TestConnection(ConnectionConfig config)
    {
        try
        {
            if (config.UseOdbcConsole)
            {
                if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                    return (false, "O console ODBC 32-bit só é suportado no Windows.");
                if (string.IsNullOrWhiteSpace(config.OdbcDefaultDir))
                    return (false, "O campo 'Diretório Paradox (DefaultDir)' é obrigatório para conexões via console ODBC.");
                if (!Directory.Exists(config.OdbcDefaultDir))
                    return (false, $"Diretório não encontrado: {config.OdbcDefaultDir}");
                if (string.IsNullOrWhiteSpace(config.OdbcDriverPath) || !File.Exists(config.OdbcDriverPath))
                    return (false, $"Driver ODBC não encontrado: {config.OdbcDriverPath}");
                return (true, string.Empty);
            }

            if (config.ConnectionType == "OleDb")
            {
                if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                    return (false, "OleDb connections are only supported on Windows.");
#pragma warning disable CA1416
                using var conn = new OleDbConnection(config.ConnectionString);
                conn.Open();
#pragma warning restore CA1416
            }
            else
            {
                using var conn = new OdbcConnection(config.ConnectionString);
                conn.Open();
            }
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public SqlExecutionResult ExecuteSql(string connectionName, string sql)
    {
        var config = GetConnection(connectionName);
        if (config == null)
            return new SqlExecutionResult
            {
                ConnectionName = connectionName,
                SqlCommand = sql,
                Success = false,
                ErrorMessage = $"Connection '{connectionName}' not found."
            };

        var result = new SqlExecutionResult
        {
            ConnectionName = connectionName,
            SqlCommand = sql
        };

        SaveSqlCommand(connectionName, sql);

        try
        {
            if (config.UseOdbcConsole)
            {
                return ExecuteSqlViaConsole(config, sql);
            }
            else if (config.ConnectionType == "OleDb")
            {
                if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                {
                    result.Success = false;
                    result.ErrorMessage = "OleDb connections are only supported on Windows.";
                    return result;
                }
#pragma warning disable CA1416
                using var conn = new OleDbConnection(config.ConnectionString);
                conn.Open();
                using var cmd = new OleDbCommand(sql, conn);
                ExecuteCommand(cmd, result);
#pragma warning restore CA1416
            }
            else
            {
                using var conn = new OdbcConnection(config.ConnectionString);
                conn.Open();
                using var cmd = new OdbcCommand(sql, conn);
                ExecuteCommand(cmd, result);
            }

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    private SqlExecutionResult ExecuteSqlViaConsole(ConnectionConfig config, string sql)
    {
        var result = new SqlExecutionResult
        {
            ConnectionName = config.Name,
            SqlCommand = sql
        };

        var sqlTempFile = Path.Combine(Path.GetTempPath(), $"odbc_sql_{Guid.NewGuid():N}.sql");
        try
        {
            File.WriteAllText(sqlTempFile, sql);

            var args = BuildConsoleArgs(config, sqlTempFile);

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _odbcConsoleExecutable,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            if (!process.WaitForExit(30000))
            {
                process.Kill();
                result.Success = false;
                result.ErrorMessage = "O console ODBC excedeu o tempo limite de 30 segundos.";
                return result;
            }

            if (string.IsNullOrWhiteSpace(output))
            {
                result.Success = false;
                result.ErrorMessage = string.IsNullOrWhiteSpace(error)
                    ? "O console ODBC não retornou dados."
                    : error;
                return result;
            }

            var consoleResult = JsonSerializer.Deserialize<ConsoleExecutionResult>(output,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (consoleResult == null)
            {
                result.Success = false;
                result.ErrorMessage = "Falha ao interpretar o resultado do console ODBC.";
                return result;
            }

            result.Success = consoleResult.Success;
            result.ErrorMessage = consoleResult.ErrorMessage;
            result.IsQuery = consoleResult.IsQuery;
            result.Columns = consoleResult.Columns ?? new List<string>();
            result.Rows = consoleResult.Rows ?? new List<List<string?>>();
            result.RowsAffected = consoleResult.RowsAffected;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Erro ao executar o console ODBC: {ex.Message}";
        }
        finally
        {
            if (File.Exists(sqlTempFile))
                File.Delete(sqlTempFile);
        }

        return result;
    }

    private static string BuildConsoleArgs(ConnectionConfig config, string sqlTempFile)
    {
        if (!string.IsNullOrWhiteSpace(config.ConnectionString))
        {
            return $"--connection-string \"{EscapeArg(config.ConnectionString)}\" --sqlfile \"{EscapeArg(sqlTempFile)}\"";
        }

        return $"--dsn \"{EscapeArg(config.Name)}\" " +
               $"--driver \"{EscapeArg(config.OdbcDriverPath)}\" " +
               $"--dir \"{EscapeArg(config.OdbcDefaultDir)}\" " +
               $"--fil \"{EscapeArg(config.OdbcFil)}\" " +
               $"--driverid \"{EscapeArg(config.OdbcDriverId)}\" " +
               $"--sqlfile \"{EscapeArg(sqlTempFile)}\"";
    }

    private static string EscapeArg(string arg) => arg.Replace("\"", "\\\"");

    private static void ExecuteCommand(System.Data.Common.DbCommand cmd, SqlExecutionResult result)
    {
        var trimmed = cmd.CommandText.TrimStart();
        var isQuery = trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
                   || trimmed.StartsWith("WITH", StringComparison.OrdinalIgnoreCase)
                   || trimmed.StartsWith("SHOW", StringComparison.OrdinalIgnoreCase)
                   || trimmed.StartsWith("DESCRIBE", StringComparison.OrdinalIgnoreCase)
                   || trimmed.StartsWith("EXEC", StringComparison.OrdinalIgnoreCase);

        result.IsQuery = isQuery;

        if (isQuery)
        {
            using var reader = cmd.ExecuteReader();
            for (int i = 0; i < reader.FieldCount; i++)
                result.Columns.Add(reader.GetName(i));

            while (reader.Read())
            {
                var row = new List<string?>();
                for (int i = 0; i < reader.FieldCount; i++)
                    row.Add(reader.IsDBNull(i) ? null : reader.GetValue(i)?.ToString());
                result.Rows.Add(row);
            }
        }
        else
        {
            result.RowsAffected = cmd.ExecuteNonQuery();
        }
    }

    private void SaveSqlCommand(string connectionName, string sql)
    {
        var safeName = string.Concat(connectionName.Split(Path.GetInvalidFileNameChars()));
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        var fileName = $"{safeName}_{timestamp}.sql";
        var filePath = Path.Combine(_sqlCommandsPath, fileName);
        File.WriteAllText(filePath, sql);
    }

    private string GetConnectionFilePath(string name)
    {
        var safeName = string.Concat(name.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_configPath, $"{safeName}.json");
    }
}

internal class ConsoleExecutionResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsQuery { get; set; }
    public List<string>? Columns { get; set; }
    public List<List<string?>>? Rows { get; set; }
    public int RowsAffected { get; set; }
}
