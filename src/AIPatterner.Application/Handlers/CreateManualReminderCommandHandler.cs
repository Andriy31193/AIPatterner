// MediatR handler for creating manual reminders
namespace AIPatterner.Application.Handlers;

using AIPatterner.Application.Commands;
using AIPatterner.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Configuration;

public class CreateManualReminderCommandHandler : IRequestHandler<CreateManualReminderCommand, Guid>
{
    private readonly IReminderCandidateRepository _repository;
    private readonly IConfiguration _configuration;

    public CreateManualReminderCommandHandler(
        IReminderCandidateRepository repository,
        IConfiguration configuration)
    {
        _repository = repository;
        _configuration = configuration;
    }

    public async Task<Guid> Handle(CreateManualReminderCommand request, CancellationToken cancellationToken)
    {
        var style = Enum.TryParse<ReminderStyle>(request.Style, out var parsedStyle) 
            ? parsedStyle 
            : ReminderStyle.Suggest;

        var defaultConfidence = _configuration.GetValue<double>("Policy:DefaultReminderConfidence", 0.5);

        var reminder = new ReminderCandidate(
            request.PersonId,
            request.SuggestedAction,
            request.CheckAtUtc,
            style,
            null,
            defaultConfidence);

        await _repository.AddAsync(reminder, cancellationToken);

        return reminder.Id;
    }
}

