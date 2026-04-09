public class BigQuerySettings
{
    public string ProjectIdGoogle { get; set; }
    public Dictionary<string, ProjetoInternoSettings> ProjetosInternos { get; set; }
}

public class ProjetoInternoSettings
{
    public string Dataset { get; set; }
    public ApiTerceirosSettings ApiTerceiros { get; set; }

}

public class ApiTerceirosSettings
{
    public string BaseUrl { get; set; }
    public string EndpointOportunidade { get; set; }
    public string EndpointFunis { get; set; }
    public string EndpointGetOpByFunis { get; set; }
    public string ApiKey { get; set; }
    public int QueueId { get; set; }

    public List<PipelineConfig> Pipelines { get; set; }
    public int LimiteConcorrencia { get; set; }
    public int TimeoutSegundos { get; set; }

    public RetrySettings RetrySettings { get; set; }
}

public class PipelineConfig
{
    public int PipelineId { get; set; }
    public List<int> StageIds { get; set; }
}

public class RetrySettings
{
    public int MaxTentativas { get; set; }
    public int BaseDelaySegundos { get; set; }
    public int MaxFalhasPermitidas { get; set; }
}

public class OpportunityDto
{
    public int Id { get; set; }
}