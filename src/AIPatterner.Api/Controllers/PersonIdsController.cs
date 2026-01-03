// API controller for getting unique person IDs
namespace AIPatterner.Api.Controllers;

using AIPatterner.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/v1/person-ids")]
[Authorize] // Requires authentication (JWT or API key)
public class PersonIdsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PersonIdsController> _logger;

    public PersonIdsController(ApplicationDbContext context, ILogger<PersonIdsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PersonIdDto>>> GetPersonIds()
    {
        // Get unique personIds from events, reminders, and routines
        var eventPersonIds = await _context.ActionEvents
            .Select(e => e.PersonId)
            .Distinct()
            .ToListAsync();

        var reminderPersonIds = await _context.ReminderCandidates
            .Select(r => r.PersonId)
            .Distinct()
            .ToListAsync();

        var routinePersonIds = await _context.Routines
            .Select(r => r.PersonId)
            .Distinct()
            .ToListAsync();

        // Get all users - personId can be username or user ID
        var users = await _context.Users
            .Select(u => new { Username = u.Username, Id = u.Id.ToString() })
            .ToListAsync();

        // Combine all personIds from entities
        var entityPersonIds = eventPersonIds
            .Union(reminderPersonIds)
            .Union(routinePersonIds)
            .Distinct()
            .ToList();

        // Create a dictionary to map personIds to display names
        var personIdMap = new Dictionary<string, string>();

        // First, process entity personIds and match them to users
        foreach (var personId in entityPersonIds)
        {
            if (!personIdMap.ContainsKey(personId))
            {
                // Check if it matches a user ID or username
                var matchingUser = users.FirstOrDefault(u => u.Id == personId || u.Username == personId);
                if (matchingUser != null)
                {
                    // If it's a username, show as "Username (User)"
                    if (personId == matchingUser.Username)
                    {
                        personIdMap[personId] = $"{matchingUser.Username} (User)";
                    }
                    // If it's a user ID, show as "Username (ID: ...)"
                    else
                    {
                        personIdMap[personId] = $"{matchingUser.Username} (ID: {personId})";
                    }
                }
                else
                {
                    personIdMap[personId] = personId;
                }
            }
        }
        
        // Then, add all users by username (only if not already added from entities)
        // This ensures all users appear in the dropdown, but we don't duplicate
        foreach (var user in users)
        {
            if (!personIdMap.ContainsKey(user.Username))
            {
                personIdMap[user.Username] = $"{user.Username} (User)";
            }
            
            // Only add user ID if it's actually used in entities (not just because user exists)
            // This prevents showing both username and ID for every user
            if (entityPersonIds.Contains(user.Id) && !personIdMap.ContainsKey(user.Id))
            {
                personIdMap[user.Id] = $"{user.Username} (ID: {user.Id})";
            }
        }

        // Create result list with all personIds
        var allPersonIds = personIdMap.Keys
            .OrderBy(p => p)
            .ToList();

        // Map to DTOs
        var result = allPersonIds.Select(p => new PersonIdDto
        {
            PersonId = p,
            DisplayName = personIdMap[p]
        }).ToList();

        return Ok(result);
    }
}

public class PersonIdDto
{
    public string PersonId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

