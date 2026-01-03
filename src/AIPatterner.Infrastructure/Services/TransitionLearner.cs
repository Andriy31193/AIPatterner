// Domain service implementation for incremental learning of action transitions
namespace AIPatterner.Infrastructure.Services;

using AIPatterner.Application.Handlers;
using AIPatterner.Domain.Entities;
using AIPatterner.Domain.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public class TransitionLearner : ITransitionLearner
{
    private readonly AIPatterner.Application.Handlers.IEventRepository _eventRepository;
    private readonly AIPatterner.Application.Handlers.ITransitionRepository _transitionRepository;
    private readonly IContextBucketKeyBuilder _contextBucketBuilder;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TransitionLearner> _logger;

    public TransitionLearner(
        AIPatterner.Application.Handlers.IEventRepository eventRepository,
        AIPatterner.Application.Handlers.ITransitionRepository transitionRepository,
        IContextBucketKeyBuilder contextBucketBuilder,
        IConfiguration configuration,
        ILogger<TransitionLearner> logger)
    {
        _eventRepository = eventRepository;
        _transitionRepository = transitionRepository;
        _contextBucketBuilder = contextBucketBuilder;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task UpdateTransitionsAsync(ActionEvent actionEvent, CancellationToken cancellationToken = default)
    {
        var sessionWindow = TimeSpan.FromMinutes(
            _configuration.GetValue<int>("Learning:SessionWindowMinutes", 30));

        var lastEvent = await _eventRepository.GetLastEventForPersonAsync(
            actionEvent.PersonId,
            actionEvent.TimestampUtc,
            cancellationToken);

        if (lastEvent == null)
        {
            _logger.LogDebug("No previous event found for person {PersonId}, skipping transition update", actionEvent.PersonId);
            return;
        }

        var timeSinceLastEvent = actionEvent.TimestampUtc - lastEvent.TimestampUtc;
        if (timeSinceLastEvent > sessionWindow)
        {
            _logger.LogDebug("Last event for person {PersonId} is outside session window, skipping transition update", actionEvent.PersonId);
            return;
        }

        var contextBucket = _contextBucketBuilder.BuildKey(actionEvent.Context);
        var transition = await _transitionRepository.GetByKeyAsync(
            actionEvent.PersonId,
            lastEvent.ActionType,
            actionEvent.ActionType,
            contextBucket,
            cancellationToken);

        var alpha = _configuration.GetValue<double>("Learning:ConfidenceAlpha", 0.1);
        var beta = _configuration.GetValue<double>("Learning:DelayBeta", 0.2);

        if (transition == null)
        {
            transition = new ActionTransition(
                actionEvent.PersonId,
                lastEvent.ActionType,
                actionEvent.ActionType,
                contextBucket,
                actionEvent.UserId);

            transition.UpdateWithObservation(timeSinceLastEvent, alpha, beta);
            await _transitionRepository.AddAsync(transition, cancellationToken);
            _logger.LogInformation(
                "Created new transition for {PersonId}: {FromAction} -> {ToAction} in context {ContextBucket}",
                actionEvent.PersonId, lastEvent.ActionType, actionEvent.ActionType, contextBucket);
        }
        else
        {
            transition.UpdateWithObservation(timeSinceLastEvent, alpha, beta);
            await _transitionRepository.UpdateAsync(transition, cancellationToken);
            _logger.LogDebug(
                "Updated transition for {PersonId}: {FromAction} -> {ToAction}, confidence: {Confidence:F2}",
                actionEvent.PersonId, lastEvent.ActionType, actionEvent.ActionType, transition.Confidence);
        }
    }
}

