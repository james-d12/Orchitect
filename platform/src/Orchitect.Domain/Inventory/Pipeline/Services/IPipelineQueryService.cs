using Orchitect.Domain.Inventory.Pipeline.Requests;

namespace Orchitect.Domain.Inventory.Pipeline.Services;

public interface IPipelineQueryService
{
    List<Pipeline> QueryPipelines(PipelineQueryRequest request);
}