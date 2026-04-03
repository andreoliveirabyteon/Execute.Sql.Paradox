using System;
using System.Collections.Generic;
using System.Data.Odbc;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32;

namespace OdbcConsole
{
    class Program
    {
        static int Main(string[] args)
        {
            string? dsnName = null;
            string? driverPath = null;
            string? defaultDir = null;
            string? fil = "Paradox 5.X";
            string? driverId = "538";
            string? sqlFile = null;
            string? connectionString = null;

            for (int i = 0; i < args.Length - 1; i++)
            {
                switch (args[i])
                {
                    case "--dsn":           dsnName          = args[++i]; break;
                    case "--driver":        driverPath       = args[++i]; break;
                    case "--dir":           defaultDir       = args[++i]; break;
                    case "--fil":           fil              = args[++i]; break;
                    case "--driverid":      driverId         = args[++i]; break;
                    case "--sqlfile":       sqlFile          = args[++i]; break;
                    case "--connection-string": connectionString = args[++i]; break;
                }
            }

            if (sqlFile == null)
            {
                WriteError("Parâmetro obrigatório ausente: --sqlfile <caminho>");
                return 1;
            }

            if (!File.Exists(sqlFile))
            {
                WriteError($"Arquivo SQL não encontrado: {sqlFile}");
                return 1;
            }

            string sql = File.ReadAllText(sqlFile).Trim();
            if (string.IsNullOrWhiteSpace(sql))
            {
                WriteError("O arquivo SQL está vazio.");
                return 1;
            }

            // Build connection string
            string odbcConnectionString;
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                odbcConnectionString = connectionString;
            }
            else if (!string.IsNullOrWhiteSpace(dsnName) && !string.IsNullOrWhiteSpace(driverPath) && !string.IsNullOrWhiteSpace(defaultDir))
            {
                SetupOdbcDsn(dsnName, driverPath, defaultDir, fil ?? "Paradox 5.X", driverId ?? "538");
                odbcConnectionString = $"DSN={dsnName}";
            }
            else
            {
                WriteError("Informe --connection-string ou os parâmetros --dsn, --driver e --dir.");
                return 1;
            }

            var result = ExecuteSql(odbcConnectionString, sql);
            Console.WriteLine(JsonSerializer.Serialize(result));
            return result.Success ? 0 : 2;
        }

        /// <summary>
        /// Registra um DSN ODBC para Paradox no registro do Windows (HKLM).
        /// </summary>
        static void SetupOdbcDsn(string dsnName, string driverPath, string defaultDir, string fil, string driverId)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                throw new PlatformNotSupportedException("O registro ODBC só é suportado no Windows.");

            using RegistryKey key = Registry.LocalMachine.CreateSubKey(
                $@"SOFTWARE\ODBC\ODBC.INI\{dsnName}");

            key.SetValue("Driver",     driverPath);
            key.SetValue("DefaultDir", defaultDir);
            key.SetValue("DriverID",   driverId);
            key.SetValue("Fil",        fil);
            key.SetValue("DBQ",        defaultDir);

            // Registra o DSN na lista de data sources ODBC
            using RegistryKey sources = Registry.LocalMachine.CreateSubKey(
                @"SOFTWARE\ODBC\ODBC.INI\ODBC Data Sources");
            sources.SetValue(dsnName, "Microsoft Paradox Driver (*.db )");
        }

        static ExecutionResult ExecuteSql(string connectionString, string sql)
        {
            var result = new ExecutionResult();
            try
            {
                using var conn = new OdbcConnection(connectionString);
                conn.Open();
                using var cmd = new OdbcCommand(sql, conn);

                var trimmed = sql.TrimStart();
                bool isQuery = trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
                            || trimmed.StartsWith("WITH",   StringComparison.OrdinalIgnoreCase)
                            || trimmed.StartsWith("SHOW",   StringComparison.OrdinalIgnoreCase)
                            || trimmed.StartsWith("DESCRIBE", StringComparison.OrdinalIgnoreCase)
                            || trimmed.StartsWith("EXEC",   StringComparison.OrdinalIgnoreCase);

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

                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        static void WriteError(string message)
        {
            var result = new ExecutionResult
            {
                Success = false,
                ErrorMessage = message
            };
            Console.WriteLine(JsonSerializer.Serialize(result));
        }
    }

    class ExecutionResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public bool IsQuery { get; set; }
        public List<string> Columns { get; set; } = new List<string>();
        public List<List<string?>> Rows { get; set; } = new List<List<string?>>();
        public int RowsAffected { get; set; }
    }
}
