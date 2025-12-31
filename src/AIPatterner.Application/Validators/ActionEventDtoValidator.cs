// FluentValidation validator for ActionEventDto
namespace AIPatterner.Application.Validators;

using AIPatterner.Application.DTOs;
using FluentValidation;

public class ActionEventDtoValidator : AbstractValidator<ActionEventDto>
{
    public ActionEventDtoValidator()
    {
        RuleFor(x => x.PersonId).NotEmpty().WithMessage("PersonId is required");
        RuleFor(x => x.ActionType).NotEmpty().WithMessage("ActionType is required");
        RuleFor(x => x.TimestampUtc).NotEmpty().WithMessage("TimestampUtc is required");
        RuleFor(x => x.Context).NotNull().WithMessage("Context is required");
        RuleFor(x => x.Context.TimeBucket).NotEmpty().When(x => x.Context != null);
        RuleFor(x => x.Context.DayType).NotEmpty().When(x => x.Context != null);
    }
}

