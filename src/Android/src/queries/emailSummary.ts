import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '../api/client';
import { endpoints } from '../api/endpoints';
import { EmailSummary } from '../types/dtos';

export const emailSummaryPreferenceKey = ['email-summary', 'preferences'] as const;

export function useEmailSummaryPreference() {
  return useQuery({
    queryKey: emailSummaryPreferenceKey,
    queryFn: async () => {
      const res = await apiClient.get<EmailSummary.PreferenceResponse>(
        endpoints.emailSummary.preferences,
      );
      return res.data;
    },
  });
}

export function useUpdateEmailSummaryPreference() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (req: EmailSummary.UpdatePreferenceRequest) => {
      const res = await apiClient.put<EmailSummary.PreferenceResponse>(
        endpoints.emailSummary.preferences,
        req,
      );
      return res.data;
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: emailSummaryPreferenceKey });
    },
  });
}

export function useSendEmailSummaryNow() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async () => {
      const res = await apiClient.post<EmailSummary.PreferenceResponse>(
        endpoints.emailSummary.sendNow,
      );
      return res.data;
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: emailSummaryPreferenceKey });
    },
  });
}

