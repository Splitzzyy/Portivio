import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '../api/client';
import { endpoints } from '../api/endpoints';
import { Holdings, Guid } from '../types/dtos';
import { homeKey } from './home';

export const holdingsKey = (profileId: Guid) => ['holdings', profileId] as const;

export function useHoldings(profileId: Guid) {
  return useQuery({
    queryKey: holdingsKey(profileId),
    queryFn: async () => {
      const res = await apiClient.get<Holdings.Response[]>(endpoints.holdings.list(profileId));
      return res.data;
    },
    enabled: !!profileId,
  });
}

export function useUpsertHolding(profileId: Guid) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (req: Holdings.UpsertRequest) => {
      const res = await apiClient.post<Holdings.Response>(
        endpoints.holdings.create(profileId),
        req,
      );
      return res.data;
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: holdingsKey(profileId) });
      void qc.invalidateQueries({ queryKey: homeKey });
    },
  });
}

export function useDeleteHolding(profileId: Guid) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: Guid) => {
      await apiClient.delete(endpoints.holdings.byId(profileId, id));
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: holdingsKey(profileId) });
      void qc.invalidateQueries({ queryKey: homeKey });
    },
  });
}
