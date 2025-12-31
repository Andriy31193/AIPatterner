// MediatR command for deleting execution history
namespace AIPatterner.Application.Commands;

using MediatR;

public class DeleteExecutionHistoryCommand : IRequest<bool>
{
    public Guid HistoryId { get; set; }
}

