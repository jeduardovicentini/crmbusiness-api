namespace CRMBusiness.Models
{
    public sealed class DeltaOpportunityError
    {
        public int OpportunityId { get; init; }
        public int StatusCode { get; init; }
        public string? Msg { get; init; }
    }
}
