using CRMBusiness.Models;
using CRMBusiness.Services.Interfaces;
using Google.Cloud.BigQuery.V2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;

namespace CRMBusiness.Services;

public sealed class FormsDataProcessingService : IFormsDataProcessingService
{
    private readonly ILogger<FormsDataProcessingService> _logger;
    private readonly BigQueryClient _bigQueryClient;
    private readonly IConfiguration _configuration;

    public FormsDataProcessingService(
        ILogger<FormsDataProcessingService> logger, 
        BigQueryClient bigQueryClient,
        IConfiguration configuration)
    {
        _logger = logger;
        _bigQueryClient = bigQueryClient;
        _configuration = configuration;
    }

    public async Task<ProcessingResult> SyncFormsDataAsync(string dataset, ProjetoInternoSettings projetoConfig)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ProcessingResult();

        try
        {
            var opportunities = await GetAllOpportunitiesWithFormsDataAsync(dataset, projetoConfig);

            if (opportunities.Count == 0)
            {
                result.Success = true;
                result.Message = "Nenhuma opportunity com formsdata para processar.";
                result.ExecutionTime = stopwatch.Elapsed;
                return result;
            }

            var (recordsToInsert, opportunityIdsToDelete) = ProcessOpportunitiesInMemory(opportunities);

            if (opportunityIdsToDelete.Count > 0)
                result.RecordsDeleted = await DeleteFormsDataInBatchAsync(dataset, projetoConfig, opportunityIdsToDelete);

            if (recordsToInsert.Count > 0)
                result.RecordsInserted = await InsertFormsDataInBatchAsync(dataset, projetoConfig, recordsToInsert);

            result.Success = true;
            result.OpportunitiesProcessed = opportunities.Count;
            result.Message = $"Sincronização concluída: {result.RecordsInserted} inseridos, {result.RecordsDeleted} deletados.";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add(ex.Message);
            _logger?.LogError(ex, "Erro ao sincronizar forms_data");
        }

        stopwatch.Stop();
        result.ExecutionTime = stopwatch.Elapsed;
        return result;
    }

    public async Task<List<Opportunity>> GetAllOpportunitiesWithFormsDataAsync(string dataset, ProjetoInternoSettings projetoConfig)
    {
        var query = $@"
            SELECT id, formsdata
            FROM `{dataset}.opportunities`
            WHERE formsdata IS NOT NULL
              AND ARRAY_LENGTH(JSON_KEYS(formsdata)) > 0";

        var opportunities = new List<Opportunity>();
        var rows = await _bigQueryClient.ExecuteQueryAsync(query, parameters: null);

        foreach (var row in rows)
        {
            var opportunity = new Opportunity { Id = Convert.ToInt32(row["id"]) };
            var formsDataRaw = row["formsdata"]?.ToString();

            if (!string.IsNullOrWhiteSpace(formsDataRaw))
            {
                try
                {
                    opportunity.FormsData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(formsDataRaw) ?? new();
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Erro ao desserializar formsdata da opportunity {OpportunityId}", opportunity.Id);
                    opportunity.FormsData = new();
                }
            }

            opportunities.Add(opportunity);
        }

        return opportunities;
    }

    public (List<FormsDataRecord> RecordsToInsert, List<int> OpportunityIdsToDelete) ProcessOpportunitiesInMemory(List<Opportunity> opportunities)
    {
        var recordsToInsert = new List<FormsDataRecord>();
        var opportunityIdsToDelete = new List<int>();

        foreach (var opportunity in opportunities)
        {
            if (opportunity.FormsData?.Count == 0) continue;

            opportunityIdsToDelete.Add(opportunity.Id);

            foreach (var kvp in opportunity.FormsData)
            {
                if (kvp.Value.ValueKind == JsonValueKind.Null) continue;

                var value = ExtractJsonValue(kvp.Value);
                if (string.IsNullOrWhiteSpace(value)) continue;

                recordsToInsert.Add(new FormsDataRecord
                {
                    OpportunityId = opportunity.Id,
                    FieldId = kvp.Key,  // ← DIRETO da chave JSON (STRING)
                    FormFieldValue = value
                });
            }
        }

        return (recordsToInsert, opportunityIdsToDelete.Distinct().ToList());
    }

    public async Task<int> DeleteFormsDataInBatchAsync(string dataset, ProjetoInternoSettings projetoConfig, List<int> opportunityIds)
    {
        if (opportunityIds.Count == 0) return 0;

        var ids = string.Join(",", opportunityIds);
        var query = $@"
            DELETE FROM `{dataset}.forms_data`
            WHERE opportunity_id IN ({ids})";

        await _bigQueryClient.ExecuteQueryAsync(query, parameters: null);
        return opportunityIds.Count;
    }

    public async Task<int> InsertFormsDataInBatchAsync(string dataset, ProjetoInternoSettings projetoConfig, List<FormsDataRecord> records)
    {
        if (records.Count == 0) return 0;

        var table = _bigQueryClient.GetTable(dataset, "forms_data");
        var rows = records.Select(r => new BigQueryInsertRow
        {
            { "opportunity_id", r.OpportunityId },
            { "field_id", r.FieldId },
            { "form_field_value", r.FormFieldValue }
        }).ToList();

        await table.InsertRowsAsync(rows);
        return records.Count;
    }

    private static string? ExtractJsonValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Array or JsonValueKind.Object => element.GetRawText(),
        _ => null
    };

    /// <summary>
    /// Obtém as configurações de um projeto específico carregadas do appsettings.json.
    /// </summary>
    public ProjetoInternoSettings ObterProjetoInternoSettings(string projeto)
    {
        var projetoConfig = _configuration
            .GetSection($"BigQuerySettings:ProjetosInternos:{projeto}")
            .Get<ProjetoInternoSettings>();

        if (projetoConfig == null)
        {
            throw new KeyNotFoundException($"As configurações para o projeto '{projeto}' não foram encontradas.");
        }

        return projetoConfig;
    }
}