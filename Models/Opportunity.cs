using System.Text.Json;
using System.Text.Json.Serialization;
using CRMBusiness.Converter;

namespace CRMBusiness.Models;

public sealed class Opportunity
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("clientid")]
    public string? ClientId { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    // valores monetários podem vir como 0/float/int
    [JsonPropertyName("value")]
    public decimal Value { get; set; }

    [JsonPropertyName("recurrentvalue")]
    public decimal RecurrentValue { get; set; }

    [JsonPropertyName("closevalue")]
    public decimal CloseValue { get; set; }

    [JsonPropertyName("closerecurrentvalue")]
    public decimal CloseRecurrentValue { get; set; }

    [JsonPropertyName("order")]
    public int Order { get; set; }

    [JsonPropertyName("origin")]
    public int Origin { get; set; }

    // no JSON real vem 0/1
    [JsonPropertyName("stagnated")]
    [JsonConverter(typeof(BoolIntJsonConverter))]
    public bool Stagnated { get; set; }

    [JsonPropertyName("formattedlocation")]
    public string? FormattedLocation { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("country")]
    public string? Country { get; set; }

    [JsonPropertyName("countrycode")]
    public string? CountryCode { get; set; }

    [JsonPropertyName("postalcode")]
    public string? PostalCode { get; set; }

    [JsonPropertyName("locationtype")]
    public string? LocationType { get; set; }

    [JsonPropertyName("lat")]
    public double? Lat { get; set; }

    [JsonPropertyName("lon")]
    public double? Lon { get; set; }

    [JsonPropertyName("address1")]
    public string? Address1 { get; set; }

    [JsonPropertyName("address2")]
    public string? Address2 { get; set; }

    [JsonPropertyName("probability")]
    public int Probability { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    // aparece no seu JSON real
    [JsonPropertyName("fkCompany")]
    public int? FkCompany { get; set; }

    [JsonPropertyName("fkPipeline")]
    public int FkPipeline { get; set; }

    [JsonPropertyName("fkStage")]
    public int FkStage { get; set; }

    [JsonPropertyName("mainphone")]
    public string? MainPhone { get; set; }

    [JsonPropertyName("mainmail")]
    public string? MainMail { get; set; }

    // no JSON real vem null
    [JsonPropertyName("expectedclosedate")]
    public DateTimeOffset? ExpectedCloseDate { get; set; }

    [JsonPropertyName("status")]
    public int Status { get; set; }

    // no JSON real vem número grande (epoch)
    [JsonPropertyName("stagebegintime")]
    public long StageBeginTime { get; set; }

    [JsonPropertyName("responsableid")]
    public int ResponsableId { get; set; }

    // arrays vazios aparecem como []
    [JsonPropertyName("followers")]
    public List<int> Followers { get; set; } = new();

    // aparece no seu JSON real
    [JsonPropertyName("visibility")]
    public int Visibility { get; set; }

    [JsonPropertyName("createdby")]
    public int CreatedBy { get; set; }

    [JsonPropertyName("closedby")]
    public int ClosedBy { get; set; }

    // no seu primeiro exemplo vinha string, no real não vi; vamos tolerante
    [JsonPropertyName("closedat")]
    public DateTimeOffset? ClosedAt { get; set; }

    // formsdata tem chaves dinâmicas e valores variáveis
    [JsonPropertyName("formsdata")]
    public Dictionary<string, JsonElement> FormsData { get; set; } = new();

    [JsonPropertyName("filesCount")]
    public int FilesCount { get; set; }

    [JsonPropertyName("contactsCount")]
    public int ContactsCount { get; set; }

    [JsonPropertyName("tasksCount")]
    public int TasksCount { get; set; }

    [JsonPropertyName("files")]
    public List<int> Files { get; set; } = new();

    // no seu JSON veio contacts: [2092]
    [JsonPropertyName("contacts")]
    public List<int> Contacts { get; set; } = new();

    // aparece no JSON real
    [JsonPropertyName("products")]
    public List<JsonElement> Products { get; set; } = new();

    [JsonPropertyName("createdAt")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("tags")]
    public List<int> Tags { get; set; } = new();

    // aparece no JSON real como []
    [JsonPropertyName("parentopportunity")]
    public List<JsonElement> ParentOpportunity { get; set; } = new();
}

