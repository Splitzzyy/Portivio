import { useQuery } from '@tanstack/react-query';
import { apiClient } from '../api/client';
import { endpoints } from '../api/endpoints';
import { Home } from '../types/dtos';

export const homeKey = ['home'] as const;

export function useHome() {
  return useQuery({
    queryKey: homeKey,
    queryFn: async () => {
      const res = await apiClient.get<Home.HomeResponse>(endpoints.home);
      return res.data;
    },
  });
}
