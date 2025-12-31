# AIPatterner

A production-ready, self-hosted middleware service in .NET 8 that implements an incremental learning → decision → reminder architecture for pattern-based action suggestions.

## Architecture

This service follows Clean Architecture principles with clear separation of concerns:

- **Domain**: Core entities, value objects, and domain service interfaces
- **Application**: Use cases, DTOs, MediatR handlers, validators, and mappings
- **Infrastructure**: EF Core persistence, external service adapters, background workers
- **API**: ASP.NET Core controllers, middleware, health checks

## Features

- **Incremental Learning**: Uses Exponential Moving Average (EMA) to learn action transitions from events
- **Context-Aware**: Buckets actions by time, day type, and location for better pattern matching
- **Policy-Based Decisions**: Transparent, testable reminder scheduling without black-box ML
- **Cooldowns & Suppression**: Respects user preferences and prevents reminder fatigue
- **Background Workers**: Automated cleanup, decay, and candidate processing
- **Optional LLM Integration**: Natural language phrasing (disabled by default)
- **Mirix Memory Integration**: Pushes human-readable summaries (not raw probabilities)

## Why Probabilities Are Kept Out of Mirix

The system explicitly avoids sending raw numeric transition matrices or probability tables to Mirix memory. Instead, it generates natural-language summaries like:

> "Alice often prepares tea after starting music in the evening (confidence: high). I suggested asking her at ~7 minutes."

This approach:
- Keeps memory human-readable and meaningful
- Avoids overwhelming the memory system with numeric data
- Makes debugging and understanding easier
- Aligns with how humans think about patterns

Raw probabilities are available via debug endpoints but are never pushed to Mirix by default.

## Quick Start

### Prerequisites

- Docker and Docker Compose
- .NET 8 SDK (for local development)

### Running with Docker Compose

1. Clone the repository
2. Set environment variables (optional):
   ```bash
   export API_KEY=your-api-key
   export ADMIN_API_KEY=your-admin-key
   ```
3. Start services:
   ```bash
   docker-compose up -d
   ```
4. The API will be available at `http://localhost:8080`
5. Swagger UI: `http://localhost:8080/swagger`

### Local Development

1. Restore dependencies:
   ```bash
   dotnet restore
   ```
2. Update connection string in `appsettings.json` or `appsettings.Development.json`
3. Run migrations:
   ```bash
   cd src/AIPatterner.Api
   dotnet ef database update
   ```
4. Run the API:
   ```bash
   dotnet run
   ```

## Configuration

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string | Required |
| `ApiKeys__Default` | API key for standard endpoints | `changeme-in-production` |
| `ApiKeys__Admin` | API key for admin endpoints | `admin-changeme-in-production` |
| `Learning__SessionWindowMinutes` | Window for considering events in same session | `30` |
| `Learning__ConfidenceAlpha` | EMA alpha for confidence updates | `0.1` |
| `Learning__DelayBeta` | EMA beta for delay updates | `0.2` |
| `Learning__DecayRate` | Daily decay rate for transitions | `0.01` |
| `Policy__MinimumOccurrences` | Min occurrences to schedule reminder | `3` |
| `Policy__MinimumConfidence` | Min confidence to schedule reminder | `0.4` |
| `Policy__MaxInterruptionCost` | Max interruption cost allowed | `0.7` |
| `Scheduler__PollIntervalSeconds` | How often to check for due candidates | `30` |
| `Scheduler__BatchSize` | Max candidates to process per cycle | `10` |
| `Cleanup__EventRetentionDays` | Days to keep raw events | `30` |
| `LLM__Enabled` | Enable LLM for natural language | `false` |
| `LLM__Endpoint` | LLM service endpoint | Empty |
| `Memory__Enabled` | Enable Mirix memory integration | `false` |
| `Memory__Endpoint` | Mirix memory endpoint | Empty |
| `Notifications__WebhookUrl` | Webhook URL for reminders | Empty |

## API Endpoints

### Ingest Event

```bash
curl -X POST http://localhost:8080/api/v1/events \
  -H "Content-Type: application/json" \
  -H "X-API-Key: your-api-key" \
  -d '{
    "personId": "alex",
    "actionType": "play_music",
    "timestampUtc": "2024-01-15T19:30:00Z",
    "context": {
      "timeBucket": "evening",
      "dayType": "weekday",
      "location": "living_room",
      "presentPeople": ["alex"],
      "stateSignals": {}
    }
  }'
```

### Get Reminder Candidates

```bash
curl -X GET "http://localhost:8080/api/v1/reminder-candidates?personId=alex&page=1&pageSize=20" \
  -H "X-API-Key: your-api-key"
```

### Get Transitions

```bash
curl -X GET "http://localhost:8080/api/v1/transitions/alex" \
  -H "X-API-Key: your-api-key"
```

### Submit Feedback

```bash
curl -X POST http://localhost:8080/api/v1/feedback \
  -H "Content-Type: application/json" \
  -H "X-API-Key: your-api-key" \
  -d '{
    "candidateId": "guid-here",
    "feedbackType": "yes",
    "comment": "Great suggestion!"
  }'
```

### Force Check (Admin)

```bash
curl -X POST "http://localhost:8080/api/v1/admin/force-check/guid-here" \
  -H "X-API-Key: your-admin-key"
```

### Webhook Check

```bash
curl -X POST "http://localhost:8080/api/v1/webhooks/check/guid-here" \
  -H "X-API-Key: your-api-key"
```

## Testing

### Run Unit Tests

```bash
dotnet test tests/AIPatterner.Tests.Unit/
```

### Run Integration Tests

```bash
dotnet test tests/AIPatterner.Tests.Integration/
```

## Database Migrations

### Create Migration

```bash
cd src/AIPatterner.Api
dotnet ef migrations add MigrationName --project ../AIPatterner.Infrastructure
```

### Apply Migrations

```bash
dotnet ef database update
```

Migrations are automatically applied on startup when running in Docker.

## Example Flow

1. **Event Ingestion**: POST event "play_music" for person "alex" at 19:30
2. **Learning**: System finds previous event "sit_on_couch" at 19:25, creates/updates transition
3. **Scheduling**: If transition has sufficient confidence and matches context, schedules ReminderCandidate
4. **Evaluation**: Background worker processes due candidate, checks cooldowns, preferences, interruption cost
5. **Notification**: If approved, sends webhook to configured endpoint with natural-language phrase
6. **Memory**: Pushes human-readable summary to Mirix (if enabled)
7. **Feedback**: User responds "yes"/"no"/"later", system updates transition confidence and cooldowns

## Architecture Decisions

- **No Raw Probabilities in Mirix**: Only human-readable summaries are pushed
- **Deterministic Policy**: All decision logic is transparent and testable
- **Optional LLM**: Used only for phrasing, not decision-making
- **Clean Architecture**: Clear boundaries between layers
- **SOLID Principles**: Small, focused classes with single responsibilities
- **Dependency Injection**: All external dependencies are injected via interfaces

## Production Considerations

- Use strong API keys (set via environment variables)
- Configure HTTPS via reverse proxy (nginx/traefik)
- Set up proper PostgreSQL backups
- Configure log aggregation (Serilog can be extended)
- Monitor health check endpoints
- Review and adjust learning parameters based on usage patterns
- Set up alerting for background worker failures

## License

[Specify your license here]
