using CRMBusiness.Services;
using CRMBusiness.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CRMBusiness.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BigQueryController : ControllerBase
{
    private readonly BigQueryService _syncService;
    private readonly IFormsDataProcessingService _formsDataProcessingService;
    private readonly IConfiguration _configuration;

    public BigQueryController(
        BigQueryService syncService,
        IFormsDataProcessingService formsDataProcessingService,
        IConfiguration configuration)
    {
        _syncService = syncService;
        _formsDataProcessingService = formsDataProcessingService;
        _configuration = configuration;
    }

    [HttpGet("opportunitysync")]
    public async Task<IActionResult> OpportunitySync([FromQuery] string projeto)
    {
        if (string.IsNullOrWhiteSpace(projeto))
            return BadRequest("Projeto é obrigatório.");

        // 🔹 Busca config completa do projeto
        var projetoConfig = _configuration
          .GetSection($"BigQuerySettings:ProjetosInternos:{projeto}")
          .Get<ProjetoInternoSettings>();

        if (projetoConfig == null)
            return NotFound($"Projeto {projeto} não encontrado.");

        await _syncService.SincronizarStatusAsync(
            projetoConfig.Dataset,
            projetoConfig);

        return Ok("Sincronização executada com sucesso.");
    }

    [HttpGet("formsdatasync")]
    public async Task<IActionResult> FormsDataSync([FromQuery] string projeto)
    {
        if (string.IsNullOrWhiteSpace(projeto))
            return BadRequest("Projeto é obrigatório.");

        var projetoConfig = _configuration
            .GetSection($"BigQuerySettings:ProjetosInternos:{projeto}")
            .Get<ProjetoInternoSettings>();

        if (projetoConfig == null)
            return NotFound($"Projeto {projeto} não encontrado.");

        var result = await _formsDataProcessingService.SyncFormsDataAsync(
            projetoConfig.Dataset,
            projetoConfig);

        if (!result.Success)
            return StatusCode(500, result);

        return Ok(result);
    }

}
