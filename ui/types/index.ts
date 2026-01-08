// TypeScript interfaces matching backend DTOs and domain models

export enum ReminderStyle {
  Ask = 'Ask',
  Suggest = 'Suggest',
  Silent = 'Silent',
}

export enum ReminderCandidateStatus {
  Scheduled = 'Scheduled',
  Executed = 'Executed',
  Skipped = 'Skipped',
  Expired = 'Expired',
}

export enum ConfidenceLevel {
  Low = 'low',
  Medium = 'medium',
  High = 'high',
}

export enum ProbabilityAction {
  Increase = 'Increase',
  Decrease = 'Decrease',
}

export enum EventType {
  Action = 'Action',
  StateChange = 'StateChange',
}

export interface SignalStateDto {
  sensorId: string;
  value: string | number | boolean;
  rawImportance?: number;
}

export interface SignalProfileEntryDto {
  weight: number;
  normalizedValue: number;
}

export interface SignalProfileDto {
  signals: Record<string, SignalProfileEntryDto>;
}

export interface ActionEventDto {
  personId: string;
  actionType: string;
  eventType?: EventType;
  timestampUtc: string;
  context: ActionContextDto;
  probabilityValue?: number;
  probabilityAction?: ProbabilityAction;
  customData?: Record<string, string>;
  userPrompt?: string;
  signalStates?: SignalStateDto[];
}

export interface ActionContextDto {
  timeBucket: string;
  dayType: string;
  location?: string;
  presentPeople?: string[];
  stateSignals?: Record<string, string>;
}

export interface ReminderCandidateDto {
  id: string;
  personId: string;
  suggestedAction: string;
  checkAtUtc: string;
  style: ReminderStyle;
  status: ReminderCandidateStatus;
  transitionId?: string;
  confidence: number;
  occurrence?: string;
  sourceEventId?: string;
  customData?: Record<string, string>;
  signalProfile?: SignalProfileDto;
  signalProfileUpdatedAtUtc?: string;
  signalProfileSamplesCount?: number;
}

export interface ReminderCandidateListResponse {
  items: ReminderCandidateDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface TransitionDto {
  id: string;
  fromAction: string;
  toAction: string;
  contextBucket: string;
  occurrenceCount: number;
  confidenceLabel: ConfidenceLevel;
  confidencePercent: number;
  averageDelay?: string;
  lastObservedUtc: string;
}

export interface TransitionListResponse {
  transitions: TransitionDto[];
}

export interface FeedbackDto {
  candidateId: string;
  feedbackType: 'yes' | 'no' | 'later';
  comment?: string;
}

export interface IngestEventResponse {
  eventId: string;
  scheduledCandidateIds: string[];
  relatedReminderId?: string;
}

export interface ActionEventListDto {
  id: string;
  personId: string;
  actionType: string;
  eventType?: EventType;
  timestampUtc: string;
  context: ActionContextDto;
  createdAtUtc: string;
  probabilityValue?: number;
  probabilityAction?: ProbabilityAction;
  relatedReminderId?: string;
  customData?: Record<string, string>;
}

export interface ActionEventListResponse {
  items: ActionEventListDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface ProcessReminderCandidateResponse {
  executed: boolean;
  shouldSpeak: boolean;
  naturalLanguagePhrase?: string;
  reason: string;
}

export interface User {
  id: string;
  username: string;
  email: string;
  role: 'admin' | 'user';
  createdAt: string;
}

export interface ApiKey {
  id: string;
  name: string;
  keyPrefix: string;
  role: 'admin' | 'user';
  userId?: string;
  createdAtUtc: string;
  lastUsedAtUtc?: string;
  expiresAtUtc?: string;
  isActive: boolean;
}

export interface UserReminderPreferences {
  personId: string;
  defaultStyle: ReminderStyle;
  dailyLimit: number;
  minimumInterval: string;
  enabled: boolean;
}

export interface LoginRequest {
  username: string;
  password: string;
}

export interface LoginResponse {
  token: string;
  user: User;
}

export interface CreateApiKeyRequest {
  name: string;
  role?: 'admin' | 'user';
  expiresAtUtc?: string;
}

export interface CreateApiKeyResponse {
  apiKey: ApiKey;
  fullKey: string;
}

export interface Configuration {
  id: string;
  key: string;
  value: string;
  category: string;
  description?: string;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface CreateConfigurationRequest {
  key: string;
  value: string;
  category: string;
  description?: string;
}

export interface UpdateConfigurationRequest {
  value: string;
  description?: string;
}

export interface CreateManualReminderRequest {
  personId: string;
  suggestedAction: string;
  checkAtUtc: string;
  style?: ReminderStyle;
  occurrence?: string;
}

export interface CreateUserRequest {
  username: string;
  email: string;
  password: string;
  role?: 'admin' | 'user';
}

export interface ExecutionHistoryDto {
  id: string;
  endpoint: string;
  requestPayload: string;
  responsePayload: string;
  executedAtUtc: string;
  personId?: string;
  userId?: string;
  actionType?: string;
  reminderCandidateId?: string;
  eventId?: string;
}

export interface ExecutionHistoryListResponse {
  items: ExecutionHistoryDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

// Routine types
export interface RoutineDto {
  id: string;
  personId: string;
  intentType: string;
  createdAtUtc: string;
  lastActivatedUtc?: string;
  observationWindowStartUtc?: string;
  observationWindowEndsUtc?: string;
  observationWindowMinutes: number;
  activeTimeContextBucket?: string | null;
}

export interface RoutineReminderDto {
  id: string;
  routineId: string;
  suggestedAction: string;
  timeContextBucket: string;
  confidence: number;
  createdAtUtc: string;
  lastObservedAtUtc?: string;
  observationCount: number;
  customData?: Record<string, string>;
  signalProfile?: SignalProfileDto;
  signalProfileUpdatedAtUtc?: string;
  signalProfileSamplesCount?: number;
  // Delay learning statistics
  delaySampleCount: number;
  emaDelaySeconds?: number | null;
  emaVarianceSeconds?: number | null;
  medianDelayApproxSeconds?: number | null;
  p90DelayApproxSeconds?: number | null;
  delayStatsLastUpdatedUtc?: string | null;
  delayStatsLastDecayUtc?: string | null;
  delayEvidenceCount: number;
}

export interface RoutineListResponse {
  items: RoutineDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface RoutineDetailDto extends RoutineDto {
  reminders: RoutineReminderDto[];
}

export interface UpdateRoutineRequest {
  observationWindowMinutes?: number;
}

