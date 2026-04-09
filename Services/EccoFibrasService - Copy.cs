//using CRMBusiness.Models;
//using Microsoft.Extensions.Logging;
//using Newtonsoft.Json.Linq;
//using System.Net.Http;
//using System.Net.Http.Headers;
//using System.Text;
//using System.Text.Json;

//namespace CRMBusiness.Services;

//public class EccoFibrasService
//{
//    private readonly IHttpClientFactory _httpClientFactory;
//    private readonly ILogger<EccoFibrasService> _logger;

//    // Configurações locais (variáveis dinâmicas/locais, como você pediu)
//    // Ajuste aqui e pronto.
//    private const int QueueId = 10;
//    private const string ApiKey = "yT8kP3qR9sV2wX5z7B9D1G3H5J7M9Q1S3U5W7Y9Z";

//    // Você pode colocar quantos quiser
//    private static readonly int[] PipelineIds = new[] { 1 };
//    private static readonly int[] StageIds = new[] { 1 };

//    private const string AtenderBemUrl = "https://eccofibras.atenderbem.com/int/getPipeOpportunities";
//    private const string AtenderBemUpdateOpportunityUrl = "https://eccofibras.atenderbem.com/int/updateOpportunity";

//    private const string DeltaUrl = "https://eccofibras.sistemadelta.com.br/Ares/api/v1/crm/ares/oportunidade/alterarResponsavelIntermediador";

//    private const string DeltaBearerToken = "Y8KgwoFaw7bDuGnCn8OzVDjCviRrT394wrHDn1jDsMOAw5zDt1bDhABxDMOlGVIXWcOMw41KwprDnWMoHj/ClsO/BMKS";

//    private const int MaxParallelDeltaRequests = 5; // ajuste: 3, 5, 10...
//    private const int MaxUpdatesPerRun = 1000; // opcional: evita rodar "infinito"

//    private static readonly Dictionary<int, string> ResponsavelIdToNome = new()
//    {
//        { 504, "JESSICA FONSECA DA SILVA LIMA" },
//        { 507, "ALINE CAMARGO" },
//        { 512, "MARIANA SILVA BALDO" },
//        { 506, "MARIANA SILVA BALDO" },
//    };

//    public EccoFibrasService(IHttpClientFactory httpClientFactory, ILogger<EccoFibrasService> logger)
//    {
//        _httpClientFactory = httpClientFactory;
//        _logger = logger;
//    }

//    public async Task<Dictionary<string, object>> AlterarResponsavelAsync()
//    {
//        var failuresByStatus = new Dictionary<int, int>();
//        var failuresLock = new object();

//        int found = 0;
//        int updated = 0;
//        int skippedNoMapping = 0;
//        var countLock = new object();

//        var deltaErrors = new List<DeltaOpportunityError>();
//        var deltaErrorsLock = new object();

//        var http = _httpClientFactory.CreateClient();
//        http.Timeout = TimeSpan.FromSeconds(40);

//        // Client do Delta (reutilizado)
//        var deltaClient = _httpClientFactory.CreateClient();
//        deltaClient.Timeout = TimeSpan.FromSeconds(40);
//        deltaClient.DefaultRequestHeaders.Authorization =
//            new AuthenticationHeaderValue("Bearer", DeltaBearerToken);

//        // 1) Coletar tarefas de update (sem executar ainda)
//        var updates = new List<(int opportunityId, int responsableId, string nomeResponsavel)>();

//        foreach (var pipelineId in PipelineIds)
//        {
//            foreach (var stageId in StageIds)
//            {
//                var reqBody = new
//                {
//                    queueId = QueueId,
//                    apiKey = ApiKey,
//                    pipelineId,
//                    stageId
//                };

//                using var reqContent = new StringContent(
//                    JsonSerializer.Serialize(reqBody),
//                    Encoding.UTF8,
//                    "application/json"
//                );

//                using var resp = await http.PostAsync(AtenderBemUrl, reqContent);

//                if (!resp.IsSuccessStatusCode)
//                {
//                    var sc = (int)resp.StatusCode;
//                    lock (failuresLock)
//                        failuresByStatus[sc] = failuresByStatus.TryGetValue(sc, out var c) ? c + 1 : 1;

//                    continue;
//                }

//                var json = await resp.Content.ReadAsStringAsync();

//                var options = new JsonSerializerOptions
//                {
//                    PropertyNameCaseInsensitive = true
//                };

//                var opportunities = JsonSerializer.Deserialize<List<Opportunity>>(json, options);
            
//                if (opportunities == null || opportunities.Count == 0)
//                    continue;

//                foreach (var opp in opportunities.OrderByDescending(o => o.Id))
//                {
//                    if (!DeveProcessar(opp.FormsData))
//                    {
//                        continue;
//                    }

//                    var opportunityId = opp.Id;
//                    var responsableId = opp.ResponsableId;

//                    lock (countLock) found++;

//                    if (!ResponsavelIdToNome.TryGetValue(responsableId, out var nomeResponsavel))
//                    {
//                        lock (countLock) skippedNoMapping++;
//                        continue;
//                    }

//                    updates.Add((opportunityId, responsableId, nomeResponsavel));

//                    if (updates.Count >= MaxUpdatesPerRun)
//                        break;
//                }

//                if (updates.Count >= MaxUpdatesPerRun)
//                    break;
//            }

//            if (updates.Count >= MaxUpdatesPerRun)
//                break;
//        }

//        // 2) Executar updates no Delta em paralelo com limite

//        using var cts = new CancellationTokenSource();
//        var token = cts.Token;

//        var abort = false;
//        string? abortReason = null;
//        var abortLock = new object();

//        var sem = new SemaphoreSlim(MaxParallelDeltaRequests);
//        var tasks = new List<Task>();

//        foreach (var u in updates)
//        {
//            lock (abortLock)
//            {
//                if (abort) break;
//            }

//            tasks.Add(Task.Run(async () =>
//            {
//                // se já cancelou, não faz nada
//                if (token.IsCancellationRequested) return;

//                await sem.WaitAsync(token);
//                try
//                {
//                    if (token.IsCancellationRequested) return;

//                    var deltaPayload = new
//                    {
//                        idIntegracao = u.opportunityId.ToString(),
//                        responsavel = u.nomeResponsavel
//                    };

//                    using var deltaContent = new StringContent(
//                        JsonSerializer.Serialize(deltaPayload),
//                        Encoding.UTF8,
//                        "application/json"
//                    );

//                    string? msg = null;

//                    using var deltaResp = await deltaClient.PostAsync(DeltaUrl, deltaContent, token);
//                    var body = await deltaResp.Content.ReadAsStringAsync(token);

//                    if (deltaResp.IsSuccessStatusCode)
//                    {
//                        var atenderBemSuccess = await AtualizarFormsDataAtenderBemAsync(http, u.opportunityId, u.formsData, token);


//                        lock (countLock) updated++;
//                        return;
//                    }

//                    var sc = (int)deltaResp.StatusCode;

//                    // se for grave: aborta
//                    if (IsGrave(sc))
//                    {
//                        lock (abortLock)
//                        {
//                            if (!abort)
//                            {
//                                abort = true;
//                                abortReason = $"Delta retornou status grave {sc}";
//                                cts.Cancel();
//                            }
//                        }
//                        return;
//                    }

//                    // não-grave: conta falha e segue
//                    lock (failuresLock)
//                        failuresByStatus[sc] = failuresByStatus.TryGetValue(sc, out var c) ? c + 1 : 1;
     
//                    try
//                    {
//                        using var errDoc = JsonDocument.Parse(body);
//                        if (errDoc.RootElement.ValueKind == JsonValueKind.Object &&
//                                errDoc.RootElement.TryGetProperty("msg", out var msgProp) &&
//                                msgProp.ValueKind == JsonValueKind.String)
//                        {
//                            lock (deltaErrorsLock) 
//                                deltaErrors.Add(new DeltaOpportunityError
//                                {
//                                    OpportunityId = u.opportunityId, // ou opportunityId, dependendo do seu escopo
//                                    StatusCode = sc,
//                                    Msg = msgProp.GetString()
//                                });
//                        }
//                    }
//                    catch { }

//                }
//                catch (OperationCanceledException)
//                {
//                    // esperado quando aborta
//                }
//                finally
//                {
//                    // só libera se realmente adquiriu
//                    // (WaitAsync(token) só retorna quando adquiriu ou cancelou)
//                    if (sem.CurrentCount < MaxParallelDeltaRequests)
//                        sem.Release();
//                }
//            }, token));
//        }

//        await Task.WhenAll(tasks);

//        return new Dictionary<string, object>
//        {
//            ["aborted"] = abort,
//            ["abortReason"] = abortReason,
//            ["found"] = found,
//            ["updatesQueued"] = updates.Count,
//            ["updated"] = updated,
//            ["skippedNoMapping"] = skippedNoMapping,
//            ["failuresByStatusCode"] = failuresByStatus,
//            ["deltaErrorsOpportunityCount"] = deltaErrors.Count,
//            ["deltaErrorsOpportunityIds"] = deltaErrors
//        };
//    }

//    private async Task<bool> AtualizarFormsDataAtenderBemAsync(HttpClient http, int opportunityId, Dictionary<string, JsonElement> formsData, CancellationToken token)
//    {
//        try
//        {
//            // Clona o formsdata e atualiza a flag
//            var formsDataAtualizado = new Dictionary<string, JsonElement>(formsData);

//            // Atualiza a flag "a7354d50" para "1"
//            formsDataAtualizado["a7354d50"] = JsonDocument.Parse("\"1\"").RootElement;

//            var payload = new
//            {
//                queueId = QueueId,
//                apiKey = ApiKey,
//                id = opportunityId,
//                formsdata = formsDataAtualizado
//            };

//            var content = new StringContent(
//                JsonSerializer.Serialize(payload, new JsonSerializerOptions
//                {
//                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
//                    DictionaryKeyPolicy = JsonNamingPolicy.CamelCase
//                }),
//                Encoding.UTF8,
//                "application/json"
//            );

//            var response = await http.PostAsync(AtenderBemUpdateOpportunityUrl, content, token);

//            if (response.IsSuccessStatusCode)
//            {
//                _logger.LogInformation("Oportunidade {OpportunityId} marcada como processada no AtenderBem", opportunityId);
//                return true;
//            }
//            else
//            {
//                var errorBody = await response.Content.ReadAsStringAsync(token);
//                _logger.LogWarning("Falha ao marcar oportunidade {OpportunityId} no AtenderBem: {StatusCode} - {ErrorBody}",
//                    opportunityId, (int)response.StatusCode, errorBody);
//                return false;
//            }
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, "Erro ao marcar oportunidade {OpportunityId} no AtenderBem", opportunityId);
//            return false;
//        }
//    }

//    static bool IsGrave(int statusCode)
//    => statusCode == 401 || (statusCode >= 500 && statusCode <= 599);

//    private static bool DeveProcessar(Dictionary<string, JsonElement>? formsData)
//    {
//        if (formsData == null || formsData.Count == 0)
//            return true;

//        if (!formsData.TryGetValue("a7354d50", out var flagElement))
//            return true;

//        if (flagElement.ValueKind == JsonValueKind.String)
//            return flagElement.GetString() != "1";

//        if (flagElement.ValueKind == JsonValueKind.Number)
//            return flagElement.GetInt32() != 1;

//        if (flagElement.ValueKind == JsonValueKind.Null)
//            return true;

//        return true;
//    }

//}