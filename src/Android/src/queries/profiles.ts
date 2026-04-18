import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '../api/client';
import { endpoints } from '../api/endpoints';
import { Profiles, Guid } from '../types/dtos';
import { homeKey } from './home';

export const profilesKey = ['profiles'] as const;

export function useProfiles() {
  return useQuery({
    queryKey: profilesKey,
    queryFn: async () => {
      const res = await apiClient.get<Profiles.Response[]>(endpoints.profiles.list);
      return res.data;
    },
  });
}

export function useCreateProfile() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (req: Profiles.CreateRequest) => {
      const res = await apiClient.post<Profiles.Response>(endpoints.profiles.create, req);
      return res.data;
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: profilesKey });
      void qc.invalidateQueries({ queryKey: homeKey });
    },
  });
}

export function useUpdateProfile() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, req }: { id: Guid; req: Profiles.UpdateRequest }) => {
      const res = await apiClient.put<Profiles.Response>(endpoints.profiles.byId(id), req);
      return res.data;
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: profilesKey });
      void qc.invalidateQueries({ queryKey: homeKey });
    },
  });
}

export function useDeleteProfile() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: Guid) => {
      await apiClient.delete(endpoints.profiles.byId(id));
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: profilesKey });
      void qc.invalidateQueries({ queryKey: homeKey });
    },
  });
}
