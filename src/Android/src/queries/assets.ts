import { useMutation, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '../api/client';
import { endpoints } from '../api/endpoints';
import { Assets, Guid } from '../types/dtos';
import { homeKey } from './home';
import { txKey } from './transactions';

export function useUpdateAsset(profileId: Guid) {
  const qc = useQueryClient();

  return useMutation({
    mutationFn: async ({
      type,
      instrumentId,
      req,
    }: {
      type: string;
      instrumentId: Guid;
      req: any;
    }) => {
      let endpoint = '';
      const normalizedType = type.toLowerCase();

      if (normalizedType.includes('stock') || normalizedType.includes('equity')) {
        endpoint = endpoints.assets.stockById(profileId, instrumentId);
      } else if (normalizedType.includes('mutual') || normalizedType.includes('fund')) {
        endpoint = endpoints.assets.mutualFundById(profileId, instrumentId);
      } else if (normalizedType.includes('gold')) {
        endpoint = endpoints.assets.goldById(profileId, instrumentId);
      } else if (normalizedType.includes('ppf')) {
        endpoint = endpoints.assets.ppfById(profileId, instrumentId);
      } else if (normalizedType.includes('fixed') || normalizedType === 'fd') {
        endpoint = endpoints.assets.fixedDepositById(profileId, instrumentId);
      } else if (normalizedType.includes('recurring') || normalizedType === 'rd') {
        endpoint = endpoints.assets.recurringDepositById(profileId, instrumentId);
      } else {
        throw new Error(`Unsupported asset type for unified update: ${type}`);
      }

      const res = await apiClient.put<Assets.AssetIngestResponse>(endpoint, req);
      return res.data;
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: txKey(profileId) });
      void qc.invalidateQueries({ queryKey: homeKey });
    },
  });
}
