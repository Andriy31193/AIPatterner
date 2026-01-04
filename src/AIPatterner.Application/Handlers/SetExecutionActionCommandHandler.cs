// MediatR handler for setting execution action on a reminder candidate
namespace AIPatterner.Application.Handlers;

using AIPatterner.Application.Commands;
using AIPatterner.Application.Services;
using AIPatterner.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Configuration;

public class SetExecutionActionCommandHandler : IRequestHandler<SetExecutionActionCommand, SetExecutionActionResponse>
{
    private readonly IReminderCandidateRepository _reminderRepository;
    private readonly IExecutionHistoryService _executionHistoryService;
    private readonly IMediator _mediator;
    private readonly IConfiguration _configuration;

    public SetExecutionActionCommandHandler(
        IReminderCandidateRepository reminderRepository,
        IExecutionHistoryService executionHistoryService,
        IMediator mediator,
        IConfiguration configuration)
    {
        _reminderRepository = reminderRepository;
        _executionHistoryService = executionHistoryService;
        _mediator = mediator;
        _configuration = configuration;
    }

    public async Task<SetExecutionActionResponse> Handle(SetExecutionActionCommand request, CancellationToken cancellationToken)
    {
        var reminder = await _reminderRepository.GetByIdAsync(request.ReminderCandidateId, cancellationToken);
        if (reminder == null)
        {
            return new SetExecutionActionResponse
            {
                Success = false,
                Message = "Reminder candidate not found"
            };
        }

        // Note: PersonId access validation is done in the controller
        // This handler assumes the controller has already validated access

        // If Execute action is requested, process the reminder
        if (request.ExecutionAction == ExecutionAction.Execute)
        {

            // Use ProcessReminderCandidateCommand to execute
            var processCommand = new ProcessReminderCandidateCommand
            {
                CandidateId = request.ReminderCandidateId,
                BypassDateCheck = true // Manual override bypasses date check
            };

            // Process the reminder (this will execute it)
            var processResult = await _mediator.Send(processCommand, cancellationToken);

            // Record audit event
            var requestPayload = System.Text.Json.JsonSerializer.Serialize(new
            {
                reminderCandidateId = reminder.Id,
                personId = reminder.PersonId,
                suggestedAction = reminder.SuggestedAction,
                confidence = reminder.Confidence,
                executionAction = request.ExecutionAction.ToString(),
                manualOverride = true
            });

            var responsePayload = System.Text.Json.JsonSerializer.Serialize(new
            {
                success = true,
                executed = true,
                manualOverride = true
            });

            await _executionHistoryService.RecordExecutionAsync(
                $"/api/v1/reminder-candidates/{reminder.Id}/execution",
                requestPayload,
                responsePayload,
                DateTime.UtcNow,
                reminder.PersonId,
                null,
                reminder.SuggestedAction,
                reminder.Id,
                null,
                cancellationToken);
        }
        else if (request.ExecutionAction == ExecutionAction.Ask || request.ExecutionAction == ExecutionAction.Suggest)
        {
            // For Ask/Suggest, we just record the action (no execution)

            // Record audit event
            var requestPayload = System.Text.Json.JsonSerializer.Serialize(new
            {
                reminderCandidateId = reminder.Id,
                personId = reminder.PersonId,
                suggestedAction = reminder.SuggestedAction,
                confidence = reminder.Confidence,
                executionAction = request.ExecutionAction.ToString(),
                manualOverride = true
            });

            var responsePayload = System.Text.Json.JsonSerializer.Serialize(new
            {
                success = true,
                executionAction = request.ExecutionAction.ToString(),
                manualOverride = true
            });

            await _executionHistoryService.RecordExecutionAsync(
                $"/api/v1/reminder-candidates/{reminder.Id}/execution",
                requestPayload,
                responsePayload,
                DateTime.UtcNow,
                reminder.PersonId,
                null,
                reminder.SuggestedAction,
                reminder.Id,
                null,
                cancellationToken);
        }

        return new SetExecutionActionResponse
        {
            Success = true,
            Message = $"Execution action set to {request.ExecutionAction}"
        };
    }
}

