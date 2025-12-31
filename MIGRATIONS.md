# Database Migrations Guide

## Creating Migrations

To create a new migration:

```bash
cd src/AIPatterner.Api
dotnet ef migrations add MigrationName --project ../AIPatterner.Infrastructure
```

## Applying Migrations

### Local Development

```bash
cd src/AIPatterner.Api
dotnet ef database update --project ../AIPatterner.Infrastructure
```

Or use the script:

```bash
./scripts/run-migrations.sh
```

### Docker

Migrations are automatically applied on startup when the API container starts (see `Program.cs`).

## Initial Migration

To create the initial migration for the first time:

```bash
cd src/AIPatterner.Api
dotnet ef migrations add InitialCreate --project ../AIPatterner.Infrastructure
```

This will create the migration files in `src/AIPatterner.Infrastructure/Persistence/Migrations/`.

## Migration Files

- Migrations are stored in `src/AIPatterner.Infrastructure/Persistence/Migrations/`
- Each migration includes an `Up()` method (applies changes) and a `Down()` method (rolls back changes)
- The `ApplicationDbContextModelSnapshot.cs` file tracks the current model state

## Troubleshooting

If you encounter issues:

1. Ensure PostgreSQL is running and accessible
2. Check the connection string in `appsettings.json` or environment variables
3. Verify the EF Core tools are installed: `dotnet tool install --global dotnet-ef`
4. For Docker, ensure the database container is healthy before the API starts

