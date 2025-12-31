// MediatR handler for deleting execution history
namespace AIPatterner.Application.Handlers;

using AIPatterner.Application.Commands;
using MediatR;

public class DeleteExecutionHistoryCommandHandler : IRequestHandler<DeleteExecutionHistoryCommand, bool>
{
    private readonly IExecutionHistoryRepository _repository;

    public DeleteExecutionHistoryCommandHandler(IExecutionHistoryRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(DeleteExecutionHistoryCommand request, CancellationToken cancellationToken)
    {
        var history = await _repository.GetByIdAsync(request.HistoryId, cancellationToken);

        if (history == null)
            return false;

        await _repository.DeleteAsync(history, cancellationToken);

        return true;
    }
}

