import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '../api/client';
import { endpoints } from '../api/endpoints';
import { Instruments, Guid } from '../types/dtos';

export const instrumentsKey = (assetTypeId?: Guid) =>
  ['instruments', assetTypeId ?? null] as const;
export const assetTypesKey = ['assetTypes'] as const;

export function useInstruments(assetTypeId?: Guid) {
  return useQuery({
    queryKey: instrumentsKey(assetTypeId),
    queryFn: async () => {
      const res = await apiClient.get<Instruments.Response[]>(endpoints.instruments.list, {
        params: assetTypeId ? { assetTypeId } : undefined,
      });
      return res.data;
    },
  });
}

export function useCreateInstrument() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (req: Instruments.CreateInstrumentRequest) => {
      const res = await apiClient.post<Instruments.Response>(endpoints.instruments.create, req);
      return res.data;
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['instruments'] });
    },
  });
}

export function useUpdateInstrument() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, req }: { id: Guid; req: Instruments.UpdateInstrumentRequest }) => {
      const res = await apiClient.put<Instruments.Response>(
        endpoints.instruments.byId(id),
        req,
      );
      return res.data;
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['instruments'] });
    },
  });
}

export function useDeleteInstrument() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: Guid) => {
      await apiClient.delete(endpoints.instruments.byId(id));
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['instruments'] });
    },
  });
}

export function useAssetTypes() {
  return useQuery({
    queryKey: assetTypesKey,
    queryFn: async () => {
      const res = await apiClient.get<Instruments.AssetTypeResponse[]>(endpoints.assetTypes.list);
      return res.data;
    },
  });
}

export function useCreateAssetType() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (req: Instruments.CreateAssetType) => {
      const res = await apiClient.post<Instruments.AssetTypeResponse>(
        endpoints.assetTypes.create,
        req,
      );
      return res.data;
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: assetTypesKey });
    },
  });
}

export function useDeleteAssetType() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: Guid) => {
      await apiClient.delete(endpoints.assetTypes.byId(id));
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: assetTypesKey });
    },
  });
}
