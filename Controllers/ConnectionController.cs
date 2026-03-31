using Microsoft.AspNetCore.Mvc;
using Execute.Sql.Paradox.Models;
using Execute.Sql.Paradox.Services;

namespace Execute.Sql.Paradox.Controllers;

public class ConnectionController : Controller
{
    private readonly ConnectionService _connectionService;

    public ConnectionController(ConnectionService connectionService)
    {
        _connectionService = connectionService;
    }

    // GET /Connection/New
    public IActionResult New()
    {
        return View(new ConnectionConfig());
    }

    // POST /Connection/Test
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Test(ConnectionConfig model)
    {
        var connectionString = BuildConnectionString(model);
        model.ConnectionString = connectionString;

        var (success, error) = _connectionService.TestConnection(connectionString);

        if (!success)
        {
            ViewBag.TestError = error;
            return View("New", model);
        }

        bool exists = _connectionService.ConnectionExists(model.Name);
        ViewBag.TestSuccess = true;
        ViewBag.AlreadyExists = exists;
        return View("New", model);
    }

    // POST /Connection/Save
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Save(ConnectionConfig model)
    {
        model.ConnectionString = BuildConnectionString(model);
        model.CreatedAt = DateTime.UtcNow;
        _connectionService.SaveConnection(model);
        TempData["Message"] = $"Conexão '{model.Name}' salva com sucesso.";
        return RedirectToAction("Index", "Home");
    }

    // POST /Connection/Delete
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(string name)
    {
        _connectionService.DeleteConnection(name);
        TempData["Message"] = $"Conexão '{name}' removida.";
        return RedirectToAction("Index", "Home");
    }

    private static string BuildConnectionString(ConnectionConfig model)
    {
        if (!string.IsNullOrWhiteSpace(model.ConnectionString))
            return model.ConnectionString;

        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(model.Driver))
            parts.Add($"Driver={{{model.Driver}}}");
        if (!string.IsNullOrWhiteSpace(model.Server))
            parts.Add($"Server={model.Server}");
        if (!string.IsNullOrWhiteSpace(model.Database))
            parts.Add($"Database={model.Database}");
        if (!string.IsNullOrWhiteSpace(model.Username))
            parts.Add($"Uid={model.Username}");
        if (!string.IsNullOrWhiteSpace(model.Password))
            parts.Add($"Pwd={model.Password}");

        return string.Join(";", parts);
    }
}
