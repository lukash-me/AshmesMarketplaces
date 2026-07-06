namespace AshmesMarketplaces.Application.AdminCalculations.Dtos;

public sealed record AdminCalculationDto(
    string ScheduleKey,
    string ExecutionMode,
    string Name,
    string Description,
    string Details,
    bool CanRunManually,
    string? ManualRunDisabledReason,
    string TimezoneId,
    string LocalTime,
    DateTime NextRunAtUtc,
    string? LastScheduledStatus,
    DateTime? LastScheduledStartedAtUtc,
    DateTime? LastScheduledCompletedAtUtc,
    string? LastScheduledError,
    IReadOnlyList<AdminCalculationScenarioDto> Scenarios,
    AdminCalculationManualRunDto? LastManualRun);

public sealed record AdminCalculationScenarioDto(
    string Id,
    string Title,
    string Description);

public sealed record AdminCalculationManualRunDto(
    Guid Id,
    string ScheduleKey,
    string Status,
    Guid RequestedByUserId,
    DateTime RequestedAtUtc,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    string? Error);
