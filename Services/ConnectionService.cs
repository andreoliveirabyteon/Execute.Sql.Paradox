using System.Data.Odbc;
using System.Data.OleDb;
using System.Text.Json;
using Execute.Sql.Paradox.Models;

namespace Execute.Sql.Paradox.Services;

public class ConnectionService
{
    private readonly string _configPath;
    private readonly string _sqlCommandsPath;

    public ConnectionService(IWebHostEnvironment env)
    {
        _configPath = Path.Combine(env.ContentRootPath, "config");
        _sqlCommandsPath = Path.Combine(env.ContentRootPath, "sqlComands");
        Directory.CreateDirectory(_configPath);
        Directory.CreateDirectory(_sqlCommandsPath);
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
            if (config.ConnectionType == "OleDb")
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
