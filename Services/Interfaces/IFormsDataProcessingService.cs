//using CRMBusiness.Models;

//namespace CRMBusiness.Services.Interfaces;

//public interface IFormsDataProcessingService
//{
//    Task<ProcessingResult> SyncFormsDataAsync(
//        string dataset,
//        ProjetoInternoSettings projetoConfig);

//    Task<List<Opportunity>> GetAllOpportunitiesWithFormsDataAsync(
//        string dataset,
//        ProjetoInternoSettings projetoConfig);

//    Task<List<Form>> GetAllFormsAsync(
//        string dataset,
//        ProjetoInternoSettings projetoConfig);

//    Dictionary<string, int> BuildFieldIdCache(List<Form> forms);

//    (List<FormsDataRecord> RecordsToInsert, List<int> OpportunityIdsToDelete)
//        ProcessOpportunitiesInMemory(
//            List<Opportunity> opportunities,
//            Dictionary<string, int> fieldIdMapping);

//    Task<int> DeleteFormsDataInBatchAsync(
//        string dataset,
//        ProjetoInternoSettings projetoConfig,
//        List<int> opportunityIds);

//    Task<int> InsertFormsDataInBatchAsync(
//        string dataset,
//        ProjetoInternoSettings projetoConfig,
//        List<FormsDataRecord> records);
//}

using CRMBusiness.Models;

namespace CRMBusiness.Services.Interfaces;

public interface IFormsDataProcessingService
{
    Task<ProcessingResult> SyncFormsDataAsync(string dataset, ProjetoInternoSettings projetoConfig);
    Task<List<Opportunity>> GetAllOpportunitiesWithFormsDataAsync(string dataset, ProjetoInternoSettings projetoConfig);
    (List<FormsDataRecord> RecordsToInsert, List<int> OpportunityIdsToDelete) ProcessOpportunitiesInMemory(List<Opportunity> opportunities);
    Task<int> DeleteFormsDataInBatchAsync(string dataset, ProjetoInternoSettings projetoConfig, List<int> opportunityIds);
    Task<int> InsertFormsDataInBatchAsync(string dataset, ProjetoInternoSettings projetoConfig, List<FormsDataRecord> records);
}