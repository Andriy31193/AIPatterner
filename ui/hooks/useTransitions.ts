// Custom hook for fetching transitions for a person
import { useQuery } from '@tanstack/react-query';
import { apiService } from '@/services/api';

export function useTransitions(personId: string) {
  return useQuery({
    queryKey: ['transitions', personId],
    queryFn: () => apiService.getTransitions(personId),
    enabled: !!personId,
  });
}

