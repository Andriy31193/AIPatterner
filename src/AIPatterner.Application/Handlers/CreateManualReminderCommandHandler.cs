// MediatR handler for creating manual reminders
namespace AIPatterner.Application.Handlers;

using AIPatterner.Application.Commands;
using AIPatterner.Application.Services;
using AIPatterner.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Configuration;

public class CreateManualReminderCommandHandler : IRequestHandler<CreateManualReminderCommand, Guid>
{
    private readonly IReminderCandidateRepository _repository;
    private readonly IConfiguration _configuration;
    private readonly IOccurrencePatternParser _patternParser;

    public CreateManualReminderCommandHandler(
        IReminderCandidateRepository repository,
        IConfiguration configuration,
        IOccurrencePatternParser patternParser)
    {
        _repository = repository;
        _configuration = configuration;
        _patternParser = patternParser;
    }

    public async Task<Guid> Handle(CreateManualReminderCommand request, CancellationToken cancellationToken)
    {
        var style = Enum.TryParse<ReminderStyle>(request.Style, out var parsedStyle) 
            ? parsedStyle 
            : ReminderStyle.Suggest;

        // Manually created reminders should have confidence < 0.7 to appear in "Low Probability Reminders" list
        var defaultConfidence = _configuration.GetValue<double>("Policy:DefaultReminderConfidence", 0.5);
        // Ensure confidence is always < 0.7 for manually created reminders
        var confidence = Math.Min(defaultConfidence, 0.69);

        // If occurrence pattern is provided, calculate CheckAtUtc based on pattern
        // Otherwise use the provided CheckAtUtc
        var checkAtUtc = request.CheckAtUtc;
        if (!string.IsNullOrWhiteSpace(request.Occurrence))
        {
            var now = DateTime.UtcNow;
            var nextExecutionTime = _patternParser.CalculateNextExecutionTime(request.Occurrence, now, null);
            if (nextExecutionTime.HasValue)
            {
                checkAtUtc = nextExecutionTime.Value;
            }
        }

        var reminder = new ReminderCandidate(
            request.PersonId,
            request.SuggestedAction,
            checkAtUtc,
            style,
            null,
            confidence,
            request.Occurrence);

        await _repository.AddAsync(reminder, cancellationToken);

        return reminder.Id;
    }
}
