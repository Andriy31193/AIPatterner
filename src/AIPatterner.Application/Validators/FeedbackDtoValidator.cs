// FluentValidation validator for FeedbackDto
namespace AIPatterner.Application.Validators;

using AIPatterner.Application.DTOs;
using FluentValidation;

public class FeedbackDtoValidator : AbstractValidator<FeedbackDto>
{
    public FeedbackDtoValidator()
    {
        RuleFor(x => x.CandidateId).NotEmpty().WithMessage("CandidateId is required");
        RuleFor(x => x.FeedbackType)
            .NotEmpty()
            .Must(x => x == "yes" || x == "no" || x == "later")
            .WithMessage("FeedbackType must be 'yes', 'no', or 'later'");
    }
}

