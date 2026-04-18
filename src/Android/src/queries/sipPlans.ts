import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '../api/client';
import { endpoints } from '../api/endpoints';
import { SipPlans, Guid } from '../types/dtos';
import { homeKey } from './home';

export const sipKey = (profileId: Guid, activeOnly?: boolean) =>
  ['sipPlans', profileId, activeOnly ?? null] as const;

export function useSipPlans(profileId: Guid, activeOnly?: boolean) {
  return useQuery({
    queryKey: sipKey(profileId, activeOnly),
    queryFn: async () => {
      const res = await apiClient.get<SipPlans.Response[]>(
        endpoints.sipPlans.list(profileId),
        { params: activeOnly !== undefined ? { activeOnly } : undefined },
      );
      return res.data;
    },
    enabled: !!profileId,
  });
}

export function useCreateSipPlan(profileId: Guid) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (req: SipPlans.CreateRequest) => {
      const res = await apiClient.post<SipPlans.Response>(
        endpoints.sipPlans.create(profileId),
        req,
      );
      return res.data;
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['sipPlans', profileId] });
      void qc.invalidateQueries({ queryKey: homeKey });
    },
  });
}

export function useUpdateSipPlan(profileId: Guid) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, req }: { id: Guid; req: SipPlans.UpdateRequest }) => {
      const res = await apiClient.put<SipPlans.Response>(
        endpoints.sipPlans.byId(profileId, id),
        req,
      );
      return res.data;
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['sipPlans', profileId] });
      void qc.invalidateQueries({ queryKey: homeKey });
    },
  });
}

export function useDeleteSipPlan(profileId: Guid) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: Guid) => {
      await apiClient.delete(endpoints.sipPlans.byId(profileId, id));
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['sipPlans', profileId] });
      void qc.invalidateQueries({ queryKey: homeKey });
    },
  });
}

export function useToggleSipPlan(profileId: Guid) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, active }: { id: Guid; active: boolean }) => {
      const url = active
        ? endpoints.sipPlans.activate(profileId, id)
        : endpoints.sipPlans.deactivate(profileId, id);
      const res = await apiClient.post<SipPlans.Response>(url);
      return res.data;
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['sipPlans', profileId] });
      void qc.invalidateQueries({ queryKey: homeKey });
    },
  });
}
