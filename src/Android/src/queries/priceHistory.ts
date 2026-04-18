import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '../api/client';
import { endpoints } from '../api/endpoints';
import { PriceHistory, Guid } from '../types/dtos';

export const pricesKey = (instrumentId: Guid) => ['prices', instrumentId] as const;

export function usePrices(instrumentId: Guid) {
  return useQuery({
    queryKey: pricesKey(instrumentId),
    queryFn: async () => {
      const res = await apiClient.get<PriceHistory.Response[]>(
        endpoints.prices.list(instrumentId),
      );
      return res.data;
    },
    enabled: !!instrumentId,
  });
}

export function useAddPrice(instrumentId: Guid) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (req: PriceHistory.AddPriceRequest) => {
      const res = await apiClient.post<PriceHistory.Response>(
        endpoints.prices.create(instrumentId),
        req,
      );
      return res.data;
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: pricesKey(instrumentId) });
    },
  });
}

export function useDeletePrice(instrumentId: Guid) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (priceId: Guid) => {
      await apiClient.delete(endpoints.prices.byId(instrumentId, priceId));
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: pricesKey(instrumentId) });
    },
  });
}
