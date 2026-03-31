using Microsoft.AspNetCore.Mvc;
using Execute.Sql.Paradox.Models;
using Execute.Sql.Paradox.Services;

namespace Execute.Sql.Paradox.Controllers;

public class SqlController : Controller
{
    private readonly ConnectionService _connectionService;

    public SqlController(ConnectionService connectionService)
    {
        _connectionService = connectionService;
    }

    // GET /Sql/Execute?connectionName=...
    public IActionResult Execute(string connectionName)
    {
        var connections = _connectionService.GetAllConnections();
        ViewBag.Connections = connections;
        ViewBag.SelectedConnection = connectionName;
        return View(new SqlExecutionResult { ConnectionName = connectionName });
    }

    // POST /Sql/Execute
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Execute(SqlExecutionRequest request)
    {
        var connections = _connectionService.GetAllConnections();
        ViewBag.Connections = connections;
        ViewBag.SelectedConnection = request.ConnectionName;

        if (string.IsNullOrWhiteSpace(request.SqlCommand))
        {
            var empty = new SqlExecutionResult
            {
                ConnectionName = request.ConnectionName,
                SqlCommand = request.SqlCommand,
                Success = false,
                ErrorMessage = "O comando SQL não pode estar vazio."
            };
            return View(empty);
        }

        var result = _connectionService.ExecuteSql(request.ConnectionName, request.SqlCommand);
        return View(result);
    }
}
