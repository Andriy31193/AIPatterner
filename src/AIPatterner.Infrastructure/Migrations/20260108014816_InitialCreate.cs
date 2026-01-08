using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIPatterner.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "actionevents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ActionType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Context_TimeBucket = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Context_DayType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Context_Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Context_PresentPeople = table.Column<string>(type: "jsonb", nullable: false),
                    Context_StateSignals = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProbabilityValue = table.Column<double>(type: "double precision", precision: 18, scale: 4, nullable: true),
                    ProbabilityAction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    RelatedReminderId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomData = table.Column<string>(type: "jsonb", nullable: true),
                    EventType = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_actionevents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "actiontransitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FromAction = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ToAction = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ContextBucket = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OccurrenceCount = table.Column<int>(type: "integer", nullable: false),
                    Confidence = table.Column<double>(type: "double precision", precision: 18, scale: 4, nullable: false),
                    AverageDelay = table.Column<TimeSpan>(type: "interval", nullable: true),
                    LastObservedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_actiontransitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "apikeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    KeyHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    KeyPrefix = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PersonId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastUsedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_apikeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "configurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Value = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_configurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "executionhistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Endpoint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RequestPayload = table.Column<string>(type: "text", nullable: false),
                    ResponsePayload = table.Column<string>(type: "text", nullable: false),
                    ExecutedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PersonId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ActionType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReminderCandidateId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_executionhistories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "remindercandidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SuggestedAction = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CheckAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TransitionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Style = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Decision_ShouldSpeak = table.Column<bool>(type: "boolean", nullable: true),
                    Decision_SpeechTemplateKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Decision_Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Decision_ConfidenceLevel = table.Column<double>(type: "double precision", precision: 18, scale: 4, nullable: true),
                    Decision_NaturalLanguagePhrase = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Confidence = table.Column<double>(type: "double precision", precision: 18, scale: 4, nullable: false),
                    Occurrence = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExecutedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SourceEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomData = table.Column<string>(type: "jsonb", nullable: true),
                    TimeWindowCenter = table.Column<long>(type: "bigint", nullable: true),
                    TimeWindowSizeMinutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 45),
                    EvidenceCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ObservedDaysJson = table.Column<string>(type: "text", nullable: true),
                    ObservedDayOfWeekHistogramJson = table.Column<string>(type: "text", nullable: true),
                    PatternInferenceStatus = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    InferredWeekday = table.Column<int>(type: "integer", nullable: true),
                    ObservedTimeBucketHistogramJson = table.Column<string>(type: "text", nullable: true),
                    ObservedDayTypeHistogramJson = table.Column<string>(type: "text", nullable: true),
                    MostCommonTimeBucket = table.Column<string>(type: "text", nullable: true),
                    MostCommonDayType = table.Column<string>(type: "text", nullable: true),
                    UserPromptsListJson = table.Column<string>(type: "jsonb", nullable: true),
                    IsSafeToAutoExecute = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    SignalProfileJson = table.Column<string>(type: "jsonb", nullable: true),
                    SignalProfileUpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SignalProfileSamplesCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_remindercandidates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "remindercooldowns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ActionType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SuppressedUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_remindercooldowns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "routinereminders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoutineId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SuggestedAction = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TimeContextBucket = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "evening"),
                    Confidence = table.Column<double>(type: "double precision", precision: 18, scale: 4, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastObservedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ObservationCount = table.Column<int>(type: "integer", nullable: false),
                    CustomData = table.Column<string>(type: "jsonb", nullable: true),
                    DelaySampleCount = table.Column<double>(type: "double precision", nullable: false, defaultValue: 0.0),
                    EmaDelaySeconds = table.Column<double>(type: "double precision", nullable: true),
                    EmaVarianceSeconds = table.Column<double>(type: "double precision", nullable: true),
                    DelayHistogramJson = table.Column<string>(type: "jsonb", nullable: true),
                    MedianDelayApproxSeconds = table.Column<double>(type: "double precision", nullable: true),
                    P90DelayApproxSeconds = table.Column<double>(type: "double precision", nullable: true),
                    DelayStatsLastUpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DelayStatsLastDecayUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DelayEvidenceJson = table.Column<string>(type: "jsonb", nullable: true),
                    UserPromptsListJson = table.Column<string>(type: "jsonb", nullable: true),
                    IsSafeToAutoExecute = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    SignalProfileJson = table.Column<string>(type: "jsonb", nullable: true),
                    SignalProfileUpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SignalProfileSamplesCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_routinereminders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "routines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IntentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastIntentOccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ObservationWindowStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ObservationWindowEndsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ObservationWindowMinutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 60),
                    ActiveTimeContextBucket = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_routines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "userreminderpreferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DefaultStyle = table.Column<int>(type: "integer", nullable: false),
                    DailyLimit = table.Column<int>(type: "integer", nullable: false),
                    MinimumInterval = table.Column<TimeSpan>(type: "interval", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    AllowAutoExecute = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_userreminderpreferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_actionevents_PersonId_TimestampUtc",
                table: "actionevents",
                columns: new[] { "PersonId", "TimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_actionevents_RelatedReminderId",
                table: "actionevents",
                column: "RelatedReminderId");

            migrationBuilder.CreateIndex(
                name: "IX_actiontransitions_PersonId_FromAction_ContextBucket",
                table: "actiontransitions",
                columns: new[] { "PersonId", "FromAction", "ContextBucket" });

            migrationBuilder.CreateIndex(
                name: "IX_actiontransitions_PersonId_ToAction",
                table: "actiontransitions",
                columns: new[] { "PersonId", "ToAction" });

            migrationBuilder.CreateIndex(
                name: "IX_apikeys_KeyHash",
                table: "apikeys",
                column: "KeyHash");

            migrationBuilder.CreateIndex(
                name: "IX_apikeys_UserId",
                table: "apikeys",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_configurations_Key_Category",
                table: "configurations",
                columns: new[] { "Key", "Category" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_executionhistories_EventId",
                table: "executionhistories",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_executionhistories_ExecutedAtUtc",
                table: "executionhistories",
                column: "ExecutedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_executionhistories_PersonId_ExecutedAtUtc",
                table: "executionhistories",
                columns: new[] { "PersonId", "ExecutedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_executionhistories_ReminderCandidateId",
                table: "executionhistories",
                column: "ReminderCandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_remindercandidates_CheckAtUtc",
                table: "remindercandidates",
                column: "CheckAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_remindercandidates_PersonId_Status",
                table: "remindercandidates",
                columns: new[] { "PersonId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_remindercandidates_PersonId_SuggestedAction_CheckAtUtc",
                table: "remindercandidates",
                columns: new[] { "PersonId", "SuggestedAction", "CheckAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_remindercandidates_SourceEventId",
                table: "remindercandidates",
                column: "SourceEventId");

            migrationBuilder.CreateIndex(
                name: "IX_remindercooldowns_PersonId_ActionType_SuppressedUntilUtc",
                table: "remindercooldowns",
                columns: new[] { "PersonId", "ActionType", "SuppressedUntilUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_routinereminders_PersonId",
                table: "routinereminders",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_routinereminders_RoutineId",
                table: "routinereminders",
                column: "RoutineId");

            migrationBuilder.CreateIndex(
                name: "IX_routinereminders_RoutineId_TimeContextBucket_SuggestedAction",
                table: "routinereminders",
                columns: new[] { "RoutineId", "TimeContextBucket", "SuggestedAction" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_routines_PersonId",
                table: "routines",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_routines_PersonId_IntentType",
                table: "routines",
                columns: new[] { "PersonId", "IntentType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_userreminderpreferences_PersonId",
                table: "userreminderpreferences",
                column: "PersonId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                table: "users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_Username",
                table: "users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "actionevents");

            migrationBuilder.DropTable(
                name: "actiontransitions");

            migrationBuilder.DropTable(
                name: "apikeys");

            migrationBuilder.DropTable(
                name: "configurations");

            migrationBuilder.DropTable(
                name: "executionhistories");

            migrationBuilder.DropTable(
                name: "remindercandidates");

            migrationBuilder.DropTable(
                name: "remindercooldowns");

            migrationBuilder.DropTable(
                name: "routinereminders");

            migrationBuilder.DropTable(
                name: "routines");

            migrationBuilder.DropTable(
                name: "userreminderpreferences");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
