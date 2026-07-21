export interface AdminCalculationManualRun {
  id: string;
  scheduleKey: string;
  status: string;
  requestedByUserId: string;
  requestedAtUtc: string;
  startedAtUtc: string | null;
  completedAtUtc: string | null;
  error: string | null;
}

export interface AdminCalculationScenario {
  id: string;
  title: string;
  description: string;
}

export interface AdminCalculation {
  scheduleKey: string;
  executionMode: 'scheduled' | 'on_demand' | string;
  name: string;
  description: string;
  details: string;
  canRunManually: boolean;
  manualRunDisabledReason: string | null;
  timezoneId: string;
  localTime: string;
  nextRunAtUtc: string;
  lastScheduledStatus: string | null;
  lastScheduledStartedAtUtc: string | null;
  lastScheduledCompletedAtUtc: string | null;
  lastScheduledError: string | null;
  scenarios: AdminCalculationScenario[];
  lastManualRun: AdminCalculationManualRun | null;
}
