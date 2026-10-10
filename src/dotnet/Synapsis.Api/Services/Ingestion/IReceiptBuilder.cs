using Synapsis.Api.Models.Ingestion;
namespace Synapsis.Api.Services.Ingestion;

/// <summary>Summarises a batch's outcome: how many records were new, changed, unchanged, duplicated or refused, and its status.</summary>
public interface IReceiptBuilder
{
    /// <summary>The receipt of the batch.</summary>
    Receipt Build(ReceiptRequest request);
}
