using Microsoft.AspNetCore.Mvc;
using CRMBusiness.Services;
using System.Diagnostics;

namespace CRMBusiness.Controllers;

[ApiController]
[Route("api/eccofibras")]
public class EccoFibrasController : ControllerBase
{
    private readonly EccoFibrasService _service;

    public EccoFibrasController(EccoFibrasService service)
    {
        _service = service;
    }

    [HttpGet("alteraresponsavel")]
    public async Task<IActionResult> AlterarResponsavel()
    {
        var sw = Stopwatch.StartNew();
        var result = await _service.AlterarResponsavelAsync();
        sw.Stop();

        // adiciona tempo de execução no mesmo retorno
        result["executionTimeSeconds"] = sw.Elapsed.TotalSeconds;

        return Ok(result);
    }
}