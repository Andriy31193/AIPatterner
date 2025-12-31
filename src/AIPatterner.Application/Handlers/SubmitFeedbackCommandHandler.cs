// MediatR handler for submitting user feedback
namespace AIPatterner.Application.Handlers;

using AIPatterner.Application.Commands;
using AIPatterner.Domain.Entities;
using MediatR;

public class SubmitFeedbackCommandHandler : IRequestHandler<SubmitFeedbackCommand, Unit>
{
    private readonly IReminderCandidateRepository _candidateRepository;
    private readonly ITransitionRepository _transitionRepository;
    private readonly ICooldownService _cooldownService;

    public SubmitFeedbackCommandHandler(
        IReminderCandidateRepository candidateRepository,
        ITransitionRepository transitionRepository,
        ICooldownService cooldownService)
    {
        _candidateRepository = candidateRepository;
        _transitionRepository = transitionRepository;
        _cooldownService = cooldownService;
    }

    public async Task<Unit> Handle(SubmitFeedbackCommand request, CancellationToken cancellationToken)
    {
        var candidate = await _candidateRepository.GetByIdAsync(request.Feedback.CandidateId, cancellationToken);
        if (candidate == null)
        {
            throw new InvalidOperationException($"ReminderCandidate {request.Feedback.CandidateId} not found");
        }

        if (request.Feedback.FeedbackType == "no")
        {
            if (candidate.TransitionId.HasValue)
            {
                var transition = await _transitionRepository.GetByIdAsync(candidate.TransitionId.Value, cancellationToken);
                if (transition != null)
                {
                    transition.ReduceConfidence(0.2); // Reduce confidence by 20%
                    await _transitionRepository.UpdateAsync(transition, cancellationToken);
                }
            }

            await _cooldownService.AddCooldownAsync(
                candidate.PersonId,
                candidate.SuggestedAction,
                TimeSpan.FromHours(24),
                "User declined reminder",
                cancellationToken);
        }
        else if (request.Feedback.FeedbackType == "yes")
        {
            if (candidate.TransitionId.HasValue)
            {
                var transition = await _transitionRepository.GetByIdAsync(candidate.TransitionId.Value, cancellationToken);
                if (transition != null)
                {
                    // Slightly increase confidence for positive feedback
                    transition.UpdateWithObservation(TimeSpan.Zero, 0.1, 0.1);
                    await _transitionRepository.UpdateAsync(transition, cancellationToken);
                }
            }
        }

        return Unit.Value;
    }
}

// Interfaces to be implemented in Infrastructure
public interface ICooldownService
{
    Task AddCooldownAsync(string personId, string actionType, TimeSpan duration, string? reason, CancellationToken cancellationToken);
    Task<bool> IsCooldownActiveAsync(string personId, string actionType, CancellationToken cancellationToken);
}

