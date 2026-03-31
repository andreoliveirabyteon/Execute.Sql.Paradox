namespace Execute.Sql.Paradox.Models;

public class SqlExecutionRequest
{
    public string ConnectionName { get; set; } = string.Empty;
    public string SqlCommand { get; set; } = string.Empty;
}

public class SqlExecutionResult
{
    public string ConnectionName { get; set; } = string.Empty;
    public string SqlCommand { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> Columns { get; set; } = new();
    public List<List<string?>> Rows { get; set; } = new();
    public int RowsAffected { get; set; }
    public bool IsQuery { get; set; }
}
