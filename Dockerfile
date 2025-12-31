# Multi-stage build for AIPatterner API
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files (in dependency order for better layer caching)
COPY ["src/AIPatterner.Domain/AIPatterner.Domain.csproj", "src/AIPatterner.Domain/"]
COPY ["src/AIPatterner.Application/AIPatterner.Application.csproj", "src/AIPatterner.Application/"]
COPY ["src/AIPatterner.Infrastructure/AIPatterner.Infrastructure.csproj", "src/AIPatterner.Infrastructure/"]
COPY ["src/AIPatterner.Api/AIPatterner.Api.csproj", "src/AIPatterner.Api/"]

# Restore dependencies for API project (will restore all dependencies)
WORKDIR "/src/src/AIPatterner.Api"
RUN dotnet restore "AIPatterner.Api.csproj"

# Copy all source files
COPY ["src/", "/src/src/"]

# Build and publish
RUN dotnet build "AIPatterner.Api.csproj" -c Release -o /app/build
RUN dotnet publish "AIPatterner.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Create logs directory
RUN mkdir -p /app/logs

# Copy published app
COPY --from=build /app/publish .

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "AIPatterner.Api.dll"]

