// AutoMapper profile for DTO ↔ Domain mappings
namespace AIPatterner.Application.Mappings;

using AIPatterner.Application.DTOs;
using AIPatterner.Domain.Entities;
using AutoMapper;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Map ActionContextDto to ActionContext
        CreateMap<ActionContextDto, ActionContext>()
            .ConstructUsing(dto => new ActionContext(
                dto.TimeBucket,
                dto.DayType,
                dto.Location,
                dto.PresentPeople,
                dto.StateSignals));

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
                    dto.Context.StateSignals),
                dto.ProbabilityValue,
                dto.ProbabilityAction,
                dto.CustomData))
            .ForMember(dest => dest.Context, opt => opt.Ignore()) // Ignore since we're constructing it manually
            .ForMember(dest => dest.RelatedReminderId, opt => opt.Ignore()); // Set separately

        CreateMap<ReminderCandidate, ReminderCandidateDto>();

        CreateMap<ActionEvent, ActionEventListDto>()
            .ForMember(dest => dest.Context, opt => opt.MapFrom(src => new ActionContextDto
            {
                TimeBucket = src.Context.TimeBucket,
                DayType = src.Context.DayType,
                Location = src.Context.Location,
                PresentPeople = src.Context.PresentPeople,
                StateSignals = src.Context.StateSignals
            }));

        CreateMap<ExecutionHistory, ExecutionHistoryDto>();

        CreateMap<Domain.Entities.UserReminderPreferences, DTOs.UserReminderPreferencesDto>()
            .ForMember(dest => dest.MinimumInterval, opt => opt.MapFrom(src => 
                System.Xml.XmlConvert.ToString(src.MinimumInterval)));

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

