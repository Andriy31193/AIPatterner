// Centralized API service layer for all backend communication
import axios, { AxiosInstance, AxiosError } from 'axios';
import type {
  ActionEventDto,
  ActionEventListResponse,
  IngestEventResponse,
  ReminderCandidateListResponse,
  TransitionListResponse,
  FeedbackDto,
  ProcessReminderCandidateResponse,
  LoginRequest,
  LoginResponse,
  ApiKey,
  CreateApiKeyRequest,
  CreateApiKeyResponse,
  Configuration,
  CreateConfigurationRequest,
  UpdateConfigurationRequest,
  CreateManualReminderRequest,
  User,
  CreateUserRequest,
  ExecutionHistoryDto,
  ExecutionHistoryListResponse,
  UserReminderPreferences,
  ReminderStyle,
  RoutineListResponse,
  RoutineDetailDto,
  RoutineReminderDto,
  UpdateRoutineRequest,
  RoutineDto,
} from '@/types';
import { ProbabilityAction } from '@/types';

class ApiService {
  private client: AxiosInstance;

  constructor() {
    // Get API URL dynamically at runtime
    // In browser: use current hostname with API port (works when accessing from different machines)
    // In SSR: use environment variable or default
    let apiUrl: string;
    if (typeof window !== 'undefined') {
      // Browser environment - construct URL from current location
      const protocol = window.location.protocol;
      const hostname = window.location.hostname;
      const apiPort = process.env.NEXT_PUBLIC_API_PORT || '8080';
      apiUrl = `${protocol}//${hostname}:${apiPort}`;
    } else {
      // Server-side rendering - use environment variable or default
      apiUrl = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080';
    }
    
    this.client = axios.create({
      baseURL: apiUrl,
      headers: {
        'Content-Type': 'application/json',
      },
    });

    // Add request interceptor to include auth token
    this.client.interceptors.request.use((config) => {
      const token = this.getToken();
      if (token) {
        config.headers.Authorization = `Bearer ${token}`;
      }
      const apiKey = this.getApiKey();
      if (apiKey) {
        config.headers['X-API-Key'] = apiKey;
      }
      return config;
    });

    // Add response interceptor for error handling
    this.client.interceptors.response.use(
      (response) => response,
      (error: AxiosError) => {
        if (error.response?.status === 401) {
          this.clearAuth();
          if (typeof window !== 'undefined') {
            window.location.href = '/login';
          }
        }
        return Promise.reject(error);
      }
    );
  }

  private getToken(): string | null {
    if (typeof window === 'undefined') return null;
    return localStorage.getItem('auth_token');
  }

  private getApiKey(): string | null {
    if (typeof window === 'undefined') return null;
    return localStorage.getItem('api_key');
  }

  private clearAuth(): void {
    if (typeof window !== 'undefined') {
      localStorage.removeItem('auth_token');
      localStorage.removeItem('api_key');
    }
  }

  setToken(token: string): void {
    if (typeof window !== 'undefined') {
      localStorage.setItem('auth_token', token);
    }
  }

  setApiKey(apiKey: string): void {
    if (typeof window !== 'undefined') {
      localStorage.setItem('api_key', apiKey);
    }
  }

  // Auth endpoints
  async login(credentials: LoginRequest): Promise<LoginResponse> {
    const response = await this.client.post<LoginResponse>('/api/v1/auth/login', credentials);
    if (response.data.token) {
      this.setToken(response.data.token);
    }
    return response.data;
  }

  async register(data: { username: string; email: string; password: string }): Promise<LoginResponse> {
    const response = await this.client.post<LoginResponse>('/api/v1/auth/register', data);
    if (response.data.token) {
      this.setToken(response.data.token);
    }
    return response.data;
  }

  logout(): void {
    this.clearAuth();
  }

  // Event endpoints
  async getEvents(params: {
    personId?: string;
    actionType?: string;
    fromUtc?: string;
    toUtc?: string;
    page?: number;
    pageSize?: number;
  }): Promise<ActionEventListResponse> {
    const response = await this.client.get<ActionEventListResponse>('/api/v1/events', { params });
    return response.data;
  }

  async ingestEvent(event: ActionEventDto): Promise<IngestEventResponse> {
    const response = await this.client.post<IngestEventResponse>('/api/v1/events', event);
    return response.data;
  }

  // Reminder candidate endpoints
  async getReminderCandidates(params: {
    personId?: string;
    actionType?: string;
    status?: string;
    fromUtc?: string;
    toUtc?: string;
    page?: number;
    pageSize?: number;
  }): Promise<ReminderCandidateListResponse> {
    const response = await this.client.get<ReminderCandidateListResponse>('/api/v1/reminder-candidates', { params });
    return response.data;
  }

  async processReminderCandidate(candidateId: string): Promise<ProcessReminderCandidateResponse> {
    const response = await this.client.post<ProcessReminderCandidateResponse>(
      `/api/v1/admin/force-check/${candidateId}`
    );
    return response.data;
  }

  // Transition endpoints
  async getTransitions(personId: string): Promise<TransitionListResponse> {
    const response = await this.client.get<TransitionListResponse>(`/api/v1/transitions/${personId}`);
    return response.data;
  }

  // Feedback endpoints
  async submitFeedback(feedback: FeedbackDto): Promise<void> {
    await this.client.post('/api/v1/feedback', feedback);
  }

  // Webhook endpoints
  async checkCandidate(candidateId: string): Promise<ProcessReminderCandidateResponse> {
    const response = await this.client.post<ProcessReminderCandidateResponse>(
      `/api/v1/webhooks/check/${candidateId}`
    );
    return response.data;
  }

  // API Key endpoints
  async getApiKeys(userId?: string): Promise<ApiKey[]> {
    const params = userId ? { userId } : {};
    const response = await this.client.get<ApiKey[]>('/api/v1/api-keys', { params });
    return response.data;
  }

  async createApiKey(request: CreateApiKeyRequest): Promise<CreateApiKeyResponse> {
    const response = await this.client.post<CreateApiKeyResponse>('/api/v1/api-keys', request);
    return response.data;
  }

  async deleteApiKey(id: string): Promise<void> {
    await this.client.delete(`/api/v1/api-keys/${id}`);
  }

  // Configuration endpoints
  async getConfigurations(category?: string): Promise<Configuration[]> {
    const params = category ? { category } : {};
    const response = await this.client.get<Configuration[]>('/api/v1/configurations', { params });
    return response.data;
  }

  async createConfiguration(request: CreateConfigurationRequest): Promise<Configuration> {
    const response = await this.client.post<Configuration>('/api/v1/configurations', request);
    return response.data;
  }

  async updateConfiguration(category: string, key: string, request: UpdateConfigurationRequest): Promise<Configuration> {
    const response = await this.client.put<Configuration>(`/api/v1/configurations/${category}/${key}`, request);
    return response.data;
  }

  // Manual reminder endpoint
  async createManualReminder(request: CreateManualReminderRequest): Promise<{ id: string }> {
    const response = await this.client.post<{ id: string }>('/api/v1/admin/reminders', request);
    return response.data;
  }

  // User management endpoints
  async getUsers(): Promise<User[]> {
    const response = await this.client.get<User[]>('/api/v1/users');
    return response.data;
  }

  async createUser(request: CreateUserRequest): Promise<User> {
    const response = await this.client.post<User>('/api/v1/users', request);
    return response.data;
  }

  async deleteUser(id: string): Promise<void> {
    await this.client.delete(`/api/v1/users/${id}`);
  }

  // Execution history endpoints
  async getExecutionHistory(params: {
    personId?: string;
    actionType?: string;
    fromUtc?: string;
    toUtc?: string;
    page?: number;
    pageSize?: number;
  }): Promise<ExecutionHistoryListResponse> {
    const response = await this.client.get<ExecutionHistoryListResponse>('/api/v1/execution-history', { params });
    return response.data;
  }

  async deleteExecutionHistory(id: string): Promise<void> {
    await this.client.delete(`/api/v1/execution-history/${id}`);
  }

  // Delete event endpoint
  async deleteEvent(id: string): Promise<void> {
    await this.client.delete(`/api/v1/events/${id}`);
  }

  // Delete reminder candidate endpoint
  async deleteReminderCandidate(id: string): Promise<void> {
    await this.client.delete(`/api/v1/reminder-candidates/${id}`);
  }

  // Get matching reminders for an event (using matching criteria)
  async getMatchingReminders(
    eventId: string,
    criteria: {
      matchByActionType?: boolean;
      matchByDayType?: boolean;
      matchByPeoplePresent?: boolean;
      matchByStateSignals?: boolean;
      matchByTimeBucket?: boolean;
      matchByLocation?: boolean;
      timeOffsetMinutes?: number;
    }
  ): Promise<ReminderCandidateListResponse> {
    const params: Record<string, string | number> = {};
    if (criteria.matchByActionType !== undefined) params.matchByActionType = criteria.matchByActionType.toString();
    if (criteria.matchByDayType !== undefined) params.matchByDayType = criteria.matchByDayType.toString();
    if (criteria.matchByPeoplePresent !== undefined) params.matchByPeoplePresent = criteria.matchByPeoplePresent.toString();
    if (criteria.matchByStateSignals !== undefined) params.matchByStateSignals = criteria.matchByStateSignals.toString();
    if (criteria.matchByTimeBucket !== undefined) params.matchByTimeBucket = criteria.matchByTimeBucket.toString();
    if (criteria.matchByLocation !== undefined) params.matchByLocation = criteria.matchByLocation.toString();
    if (criteria.timeOffsetMinutes !== undefined) params.timeOffsetMinutes = criteria.timeOffsetMinutes.toString();

    const response = await this.client.get<ReminderCandidateListResponse>(
      `/api/v1/events/${eventId}/matching-reminders`,
      { params }
    );
    return response.data;
  }

  // Get related reminders for an event (by SourceEventId)
  async getRelatedReminders(eventId: string): Promise<ReminderCandidateListResponse> {
    const response = await this.client.get<ReminderCandidateListResponse>(
      `/api/v1/events/${eventId}/related-reminders`
    );
    return response.data;
  }

  // Execute reminder now (bypass date check)
  async executeReminderNow(candidateId: string): Promise<ProcessReminderCandidateResponse> {
    const response = await this.client.post<ProcessReminderCandidateResponse>(
      `/api/v1/admin/force-check/${candidateId}?bypassDateCheck=true`
    );
    return response.data;
  }

  // Update reminder occurrence
  async updateReminderOccurrence(candidateId: string, occurrence: string | null): Promise<void> {
    await this.client.put(`/api/v1/reminder-candidates/${candidateId}/occurrence`, {
      occurrence: occurrence || null,
    });
  }

  // User preferences endpoints
  async getUserPreferences(personId: string): Promise<UserReminderPreferences> {
    const response = await this.client.get<UserReminderPreferences>(`/api/v1/user-preferences/${personId}`);
    return response.data;
  }

  async updateUserPreferences(personId: string, preferences: {
    defaultStyle?: ReminderStyle;
    dailyLimit?: number;
    minimumInterval?: string;
    enabled?: boolean;
  }): Promise<void> {
    await this.client.put(`/api/v1/user-preferences/${personId}`, preferences);
  }

  // Routine endpoints
  async getRoutines(params: {
    personId?: string;
    page?: number;
    pageSize?: number;
  }): Promise<RoutineListResponse> {
    const response = await this.client.get<RoutineListResponse>('/api/v1/routines', { params });
    return response.data;
  }

  async getRoutine(id: string): Promise<RoutineDetailDto> {
    const response = await this.client.get<RoutineDetailDto>(`/api/v1/routines/${id}`);
    return response.data;
  }

  async getRoutineReminders(routineId: string): Promise<RoutineReminderDto[]> {
    const response = await this.client.get<RoutineReminderDto[]>(`/api/v1/routines/${routineId}/reminders`);
    return response.data;
  }

  async submitRoutineReminderFeedback(
    routineId: string,
    reminderId: string,
    action: ProbabilityAction,
    value: number
  ): Promise<void> {
    await this.client.post(`/api/v1/routines/${routineId}/reminders/${reminderId}/feedback`, {
      action,
      value,
    });
  }

  async getActiveRoutines(personId: string): Promise<RoutineListResponse> {
    const response = await this.client.get<RoutineListResponse>(`/api/v1/routines/active`, {
      params: { personId }
    });
    return response.data;
  }

  async updateRoutine(id: string, request: UpdateRoutineRequest): Promise<RoutineDto> {
    const response = await this.client.put<RoutineDto>(`/api/v1/routines/${id}`, request);
    return response.data;
  }

  // Get all unique personIds
  async getPersonIds(): Promise<{ personId: string; displayName: string }[]> {
    const response = await this.client.get<{ personId: string; displayName: string }[]>('/api/v1/person-ids');
    return response.data;
  }
}

export const apiService = new ApiService();

