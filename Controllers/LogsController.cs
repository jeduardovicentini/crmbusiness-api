using Microsoft.AspNetCore.Mvc;

[Route("logs")]
public class LogsController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        var logDir = Path.Combine(Directory.GetCurrentDirectory(), "logs");

        if (!Directory.Exists(logDir))
            return Content("Nenhum log encontrado.");

        var latestFile = Directory
            .GetFiles(logDir)
            .OrderByDescending(f => f)
            .FirstOrDefault();

        if (latestFile == null)
            return Content("Nenhum log encontrado.");

        var content = System.IO.File.ReadAllText(latestFile);

        return Content(content, "text/plain");
    }
}