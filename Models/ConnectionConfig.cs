namespace Execute.Sql.Paradox.Models;

public class ConnectionConfig
{
    public static readonly IReadOnlyList<string> ParadoxVersions =
        new[] { "Paradox 3.X", "Paradox 4.X", "Paradox 5.X", "Paradox 7.X" };

    public string Name { get; set; } = string.Empty;
    public string ConnectionType { get; set; } = "ODBC";
    public string ConnectionString { get; set; } = string.Empty;
    public string Driver { get; set; } = string.Empty;
    public string Server { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Paradox ODBC console (32-bit) settings
    public bool UseOdbcConsole { get; set; } = false;
    public string OdbcDriverPath { get; set; } = @"C:\Windows\SysWOW64\odbcjt32.dll";
    public string OdbcDefaultDir { get; set; } = string.Empty;
    public string OdbcFil { get; set; } = "Paradox 5.X";
    public string OdbcDriverId { get; set; } = "538";
}
