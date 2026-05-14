/**
 * Email summary scheduling types — mirror backend DTOs in
 * src/backend/Portivio.Application/DTOs/EmailSummary/.
 */

export type EmailSummaryFrequency = 'Daily' | 'Weekly' | 'Monthly';

export type MonthlyDayMode = 'DayOfMonth' | 'LastDay';

export type EmailSummarySendStatus = 'Queued' | 'Succeeded' | 'Failed' | 'Skipped';

export type DayOfWeek =
  | 'Sunday'
  | 'Monday'
  | 'Tuesday'
  | 'Wednesday'
  | 'Thursday'
  | 'Friday'
  | 'Saturday';

export type TimeOfDayHHmm = string; // HH:mm

export interface UpdateEmailSummaryPreferenceRequest {
  isEnabled: boolean;

  frequency?: EmailSummaryFrequency | null;
  timeOfDay?: TimeOfDayHHmm | null;
  weeklyDayOfWeek?: DayOfWeek | null;
  monthlyDayMode?: MonthlyDayMode | null;
  monthlyDayOfMonth?: number | null;

  timeZoneId?: string | null;
}

export interface EmailSummaryPreferenceResponse {
  id: string;
  userId: string;

  isEnabled: boolean;

  frequency: EmailSummaryFrequency | null;
  timeOfDay: TimeOfDayHHmm | null;
  weeklyDayOfWeek: DayOfWeek | null;
  monthlyDayMode: MonthlyDayMode | null;
  monthlyDayOfMonth: number | null;

  timeZoneId: string;

  lastSendStatus: EmailSummarySendStatus | null;
  lastSendAttemptAtUtc: string | null;
  lastSendSucceededAtUtc: string | null;
  lastSendError: string | null;
  lastManualQueuedAtUtc: string | null;

  nextRunAtUtc: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

