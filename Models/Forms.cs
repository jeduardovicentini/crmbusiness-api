//namespace CRMBusiness.Models;

//public sealed class Form
//{
//    public int Id { get; set; }
//    public string? FieldId { get; set; }
//}

//public sealed class FormsDataRecord
//{
//    public int OpportunityId { get; set; }
//    public int FormsId { get; set; }
//    public string FormFieldValue { get; set; } = string.Empty;
//}

public sealed class ProcessingResult
{
    public bool Success { get; set; }
    public int RecordsInserted { get; set; }
    public int RecordsDeleted { get; set; }
    public int OpportunitiesProcessed { get; set; }
    public List<string> Errors { get; set; } = new();
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
    public TimeSpan ExecutionTime { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class FormsDataRecord
{
    public int OpportunityId { get; set; }
    public string FieldId { get; set; } = "";  // ← STRING do JSON key
    public string FormFieldValue { get; set; } = "";
}