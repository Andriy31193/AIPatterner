// MediatR handler for updating reminder occurrence
namespace AIPatterner.Application.Handlers;

using AIPatterner.Application.Commands;
using AIPatterner.Application.Services;
using MediatR;

public class UpdateReminderOccurrenceCommandHandler : IRequestHandler<UpdateReminderOccurrenceCommand, bool>
{
    private readonly IReminderCandidateRepository _repository;
    private readonly IOccurrencePatternParser _patternParser;

    public UpdateReminderOccurrenceCommandHandler(
        IReminderCandidateRepository repository,
        IOccurrencePatternParser patternParser)
    {
        _repository = repository;
        _patternParser = patternParser;
    }

    public async Task<bool> Handle(UpdateReminderOccurrenceCommand request, CancellationToken cancellationToken)
    {
        var candidate = await _repository.GetByIdAsync(request.ReminderCandidateId, cancellationToken);
        if (candidate == null)
        {
            return false;
        }

        candidate.SetOccurrence(request.Occurrence);
        
        // If pattern is set, calculate and update CheckAtUtc based on the pattern
        if (!string.IsNullOrWhiteSpace(request.Occurrence))
        {
            var now = DateTime.UtcNow;
            var nextExecutionTime = _patternParser.CalculateNextExecutionTime(
                request.Occurrence, 
                now, 
                candidate.ExecutedAtUtc ?? candidate.CheckAtUtc);
            
            if (nextExecutionTime.HasValue)
            {
                candidate.UpdateCheckAtUtc(nextExecutionTime.Value);
            }
        }
        
        await _repository.UpdateAsync(candidate, cancellationToken);
        return true;
    }
}


