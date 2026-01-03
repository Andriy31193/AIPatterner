// MediatR command for creating configurations
namespace AIPatterner.Application.Commands;

using AIPatterner.Application.DTOs;
using MediatR;

public class CreateConfigurationCommand : IRequest<ConfigurationDto>
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
}


