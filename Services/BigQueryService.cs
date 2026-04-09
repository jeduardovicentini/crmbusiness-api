using System.Text;
using System.Text.Json;
using System.Diagnostics;
using Google.Cloud.BigQuery.V2;

namespace CRMBusiness.Services;

public class BigQueryService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly BigQueryClient _bigQueryClient;
    private readonly IConfiguration _configuration;

    // Limite de chamadas paralelas
    private const int LIMITE_CONCORRENCIA = 5;

    public BigQueryService(
        IHttpClientFactory httpClientFactory,
        BigQueryClient bigQueryClient,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _bigQueryClient = bigQueryClient;
        _configuration = configuration;
    }

    #region Sincronizar Status
    public async Task SincronizarStatusAsync(string dataset, ProjetoInternoSettings projeto)
    {
        Console.WriteLine("Iniciando sincronização...");

        var idsApi = await ObterIdsAtivosApiParaleloComControleFalhas(projeto.ApiTerceiros, projeto.ApiTerceiros.RetrySettings);

        if (idsApi.Count == 0)
            throw new Exception("API retornou vazio. Abortando para evitar limpeza total.");

        await AtualizarStatusUltraAsync(dataset, idsApi);

        Console.WriteLine("Sincronização finalizada.");
    }

    private async Task<HashSet<int>> ObterIdsAtivosApiParaleloComControleFalhas(ApiTerceirosSettings settings, RetrySettings retrySettings)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromSeconds(settings.TimeoutSegundos);

        var semaphore = new SemaphoreSlim(LIMITE_CONCORRENCIA);

        var tasks = new List<Task<List<int>>>();

        int contadorFalhas = 0;

        // Token para cancelar todas tarefas se limite de falhas for atingido
        var cts = new CancellationTokenSource();

        foreach (var pipeline in settings.Pipelines)
        {
            tasks.Add(Task.Run(async () =>
            {
                await semaphore.WaitAsync(cts.Token);

                try
                {
                    if (cts.Token.IsCancellationRequested)
                        return new List<int>();

                    var body = new
                    {
                        queueId = settings.QueueId,
                        apiKey = settings.ApiKey,
                        pipelineId = pipeline.PipelineId
                    };

                    var content = new StringContent(
                        JsonSerializer.Serialize(body),
                        Encoding.UTF8,
                        "application/json");

                    var response = await PostComRetryAsync(
                        httpClient,
                        settings.BaseUrl + settings.EndpointGetOpByFunis,
                        content,
                        pipeline.PipelineId,
                        cts.Token,
                        retrySettings);

                    if (!response.IsSuccessStatusCode)
                    {
                        Interlocked.Increment(ref contadorFalhas);

                        Console.WriteLine($"Pipeline {pipeline.PipelineId} falhou após retries.");

                        // Se exceder limite de falhas → cancelar tudo
                        if (contadorFalhas >= retrySettings.MaxFalhasPermitidas)
                        {
                            Console.WriteLine("Limite de falhas atingido. Cancelando sincronização.");
                            cts.Cancel();
                        }

                        return new List<int>();
                    }

                    var json = await response.Content.ReadAsStringAsync(cts.Token);

                    var oportunidades = JsonSerializer.Deserialize<List<OpportunityDto>>(
                        json,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                    return oportunidades?.Select(o => o.Id).ToList() ?? new List<int>();
                }
                catch (OperationCanceledException)
                {
                    return new List<int>();
                }
                finally
                {
                    semaphore.Release();
                }
            }, cts.Token));
        }

        await Task.WhenAll(tasks);

        var ids = new HashSet<int>();

        foreach (var t in tasks)
        {
            foreach (var id in t.Result)
                ids.Add(id);
        }

        return ids;
    }

    /// <summary>
    /// Método responsável por executar POST com retry automático,
    /// backoff exponencial e log do tempo de cada tentativa.
    /// </summary>
    private async Task<HttpResponseMessage> PostComRetryAsync(
        HttpClient httpClient,
        string url,
        HttpContent content,
        int pipelineId,
        CancellationToken token,
        RetrySettings retrySettings)
    {
        for (int tentativa = 1; tentativa <= retrySettings.MaxTentativas; tentativa++)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                Console.WriteLine($"Pipeline {pipelineId} - Tentativa {tentativa}");

                var response = await httpClient.PostAsync(url, content, token);

                stopwatch.Stop();

                Console.WriteLine(
                    $"Pipeline {pipelineId} - Tentativa {tentativa} levou {stopwatch.ElapsedMilliseconds} ms");

                // Se for 503 (fila indisponível), aplicar retry
                if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
                {
                    if (tentativa == retrySettings.MaxTentativas)
                        return response;

                    var delay = Math.Pow(retrySettings.BaseDelaySegundos, tentativa);

                    Console.WriteLine(
                        $"Pipeline {pipelineId} - 503 recebido. Aguardando {delay}s para retry.");

                    await Task.Delay(TimeSpan.FromSeconds(delay), token);
                    continue;
                }

                return response;
            }
            catch (Exception ex) when (
                ex is HttpRequestException ||
                ex is TaskCanceledException)
            {
                stopwatch.Stop();

                Console.WriteLine(
                    $"Pipeline {pipelineId} - Erro na tentativa {tentativa}. Tempo: {stopwatch.ElapsedMilliseconds} ms");

                if (tentativa == retrySettings.MaxTentativas)
                    throw;

                var delay = Math.Pow(retrySettings.BaseDelaySegundos, tentativa);

                Console.WriteLine(
                    $"Pipeline {pipelineId} - Aguardando {delay}s antes de nova tentativa.");

                await Task.Delay(TimeSpan.FromSeconds(delay), token);
            }
        }

        throw new Exception("Erro inesperado no retry.");
    }

    /// <summary>
    /// Update ultra otimizado no BigQuery.
    /// Marca como 99 tudo que não estiver mais presente na API.
    /// </summary>
    private async Task AtualizarStatusUltraAsync(
        string dataset,
        HashSet<int> idsApi)
    {
        var parameters = new[]
        {
            new BigQueryParameter(
                "idsApi",
                BigQueryDbType.Array,
                idsApi.ToList())
        };

        var query = $@"
            UPDATE `{dataset}.opportunities`
            SET status = 99
            WHERE status <> 99
            AND id NOT IN UNNEST(@idsApi)";

        await _bigQueryClient.ExecuteQueryAsync(query, parameters);
    }

    #endregion

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
