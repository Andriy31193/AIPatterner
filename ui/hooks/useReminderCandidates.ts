// Custom hook for fetching reminder candidates with filters
import { useQuery } from '@tanstack/react-query';
import { apiService } from '@/services/api';

interface UseReminderCandidatesParams {
  personId?: string;
  status?: string;
  fromUtc?: string;
  toUtc?: string;
  page?: number;
  pageSize?: number;
}

export function useReminderCandidates(params: UseReminderCandidatesParams = {}) {
  return useQuery({
    queryKey: ['reminderCandidates', params],
    queryFn: () => apiService.getReminderCandidates(params),
  });
}

