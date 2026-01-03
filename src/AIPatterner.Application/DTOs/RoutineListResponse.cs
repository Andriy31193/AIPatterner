// Response DTO for routine list
namespace AIPatterner.Application.DTOs;

public class RoutineListResponse
{
    public List<RoutineDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

