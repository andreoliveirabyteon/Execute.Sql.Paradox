using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Execute.Sql.Paradox.Models;
using Execute.Sql.Paradox.Services;

namespace Execute.Sql.Paradox.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ConnectionService _connectionService;

    public HomeController(ILogger<HomeController> logger, ConnectionService connectionService)
    {
        _logger = logger;
        _connectionService = connectionService;
    }

    public IActionResult Index()
    {
        var connections = _connectionService.GetAllConnections();
        return View(connections);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
