import {
  useInfiniteQuery,
  useMutation,
  useQueryClient,
} from '@tanstack/react-query';
import { apiClient } from '../api/client';
import { endpoints } from '../api/endpoints';
import { Transactions, Guid } from '../types/dtos';
import { homeKey } from './home';

export const txKey = (profileId: Guid) => ['transactions', profileId] as const;

const PAGE_SIZE = 20;

export function useTransactions(profileId: Guid) {
  return useInfiniteQuery({
    queryKey: txKey(profileId),
    initialPageParam: 1,
    queryFn: async ({ pageParam }) => {
      const res = await apiClient.get<Transactions.Response[]>(
        endpoints.transactions.list(profileId),
        { params: { page: pageParam, pageSize: PAGE_SIZE } },
      );
      return { items: res.data, page: pageParam as number };
    },
    getNextPageParam: (last) => (last.items.length === PAGE_SIZE ? last.page + 1 : undefined),
    enabled: !!profileId,
  });
}

export function useCreateTransaction(profileId: Guid) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (req: Transactions.CreateRequest) => {
      const res = await apiClient.post<Transactions.Response>(
        endpoints.transactions.create(profileId),
        req,
      );
      return res.data;
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: txKey(profileId) });
      void qc.invalidateQueries({ queryKey: homeKey });
    },
  });
}

export function useUpdateTransaction(profileId: Guid) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, req }: { id: Guid; req: Transactions.UpdateRequest }) => {
      const res = await apiClient.put<Transactions.Response>(
        endpoints.transactions.byId(profileId, id),
        req,
      );
      return res.data;
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: txKey(profileId) });
      void qc.invalidateQueries({ queryKey: homeKey });
    },
  });
}

export function useDeleteTransaction(profileId: Guid) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: Guid) => {
      await apiClient.delete(endpoints.transactions.byId(profileId, id));
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: txKey(profileId) });
      void qc.invalidateQueries({ queryKey: homeKey });
    },
  });
}
