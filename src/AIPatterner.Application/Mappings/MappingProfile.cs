// AutoMapper profile for DTO ↔ Domain mappings
namespace AIPatterner.Application.Mappings;

using AIPatterner.Application.DTOs;
using AIPatterner.Domain.Entities;
using AutoMapper;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<ActionEventDto, ActionEvent>()
            .ConstructUsing(dto => new ActionEvent(
                dto.PersonId,
                dto.ActionType,
                dto.TimestampUtc,
                new ActionContext(
                    dto.Context.TimeBucket,
                    dto.Context.DayType,
                    dto.Context.Location,
                    dto.Context.PresentPeople,
                    dto.Context.StateSignals)));

        CreateMap<ReminderCandidate, ReminderCandidateDto>();

        CreateMap<ExecutionHistory, ExecutionHistoryDto>();

        CreateMap<ActionTransition, TransitionDto>()
            .ForMember(dest => dest.ConfidenceLabel, opt => opt.MapFrom(src => GetConfidenceLabel(src.Confidence)))
            .ForMember(dest => dest.ConfidencePercent, opt => opt.MapFrom(src => src.Confidence * 100));
    }

    private static string GetConfidenceLabel(double confidence)
    {
        return confidence switch
        {
            >= 0.7 => "high",
            >= 0.4 => "medium",
            _ => "low"
        };
    }
}

