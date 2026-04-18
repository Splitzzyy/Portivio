import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '../api/client';
import { endpoints } from '../api/endpoints';
import { Performance, Guid } from '../types/dtos';
import { homeKey } from './home';

export const perfHistoryKey = (profileId: Guid, days: number) =>
  ['perf', 'history', profileId, days] as const;
export const perfLatestKey = (profileId: Guid) => ['perf', 'latest', profileId] as const;

export function usePerformanceHistory(profileId: Guid, days = 90) {
  return useQuery({
    queryKey: perfHistoryKey(profileId, days),
    queryFn: async () => {
      const res = await apiClient.get<Performance.HistoryResponse>(
        endpoints.performance.history(profileId),
        { params: { days } },
      );
      return res.data;
    },
    enabled: !!profileId,
  });
}

export function usePerformanceLatest(profileId: Guid) {
  return useQuery({
    queryKey: perfLatestKey(profileId),
    queryFn: async () => {
      const res = await apiClient.get<Performance.Response>(
        endpoints.performance.latest(profileId),
      );
      return res.data;
    },
    enabled: !!profileId,
  });
}

export function useRecordSnapshot(profileId: Guid) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (req?: Performance.RecordSnapshotRequest) => {
      const res = await apiClient.post<Performance.Response>(
        endpoints.performance.snapshot(profileId),
        req ?? {},
      );
      return res.data;
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['perf'] });
      void qc.invalidateQueries({ queryKey: homeKey });
    },
  });
}
