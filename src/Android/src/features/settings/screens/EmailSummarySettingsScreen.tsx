import React, { useEffect, useMemo, useState } from 'react';
import { ScrollView, StyleSheet, View } from 'react-native';
import { Button, Divider, HelperText, List, SegmentedButtons, Switch, Text, TextInput } from 'react-native-paper';
import Toast from 'react-native-toast-message';
import { format } from 'date-fns';

import type { MoreScreenProps } from '../../../navigation/types';
import { useAuthStore } from '../../../store/authStore';
import { useEmailSummaryPreference, useSendEmailSummaryNow, useUpdateEmailSummaryPreference } from '../../../queries/emailSummary';
import { ErrorView } from '../../../components/feedback/ErrorView';
import { LoadingOverlay } from '../../../components/feedback/LoadingOverlay';
import type { EmailSummary } from '../../../types/dtos';

const dayOptions: { label: string; value: EmailSummary.DayOfWeek }[] = [
  { label: 'Sun', value: 'Sunday' },
  { label: 'Mon', value: 'Monday' },
  { label: 'Tue', value: 'Tuesday' },
  { label: 'Wed', value: 'Wednesday' },
  { label: 'Thu', value: 'Thursday' },
  { label: 'Fri', value: 'Friday' },
  { label: 'Sat', value: 'Saturday' },
];

function fmt(iso?: string | null): string {
  if (!iso) return '—';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return format(d, 'PPpp');
}

function isTimeOfDay(value: string): boolean {
  return /^([01]\d|2[0-3]):([0-5]\d)$/.test(value);
}

export function EmailSummarySettingsScreen({
  navigation,
}: MoreScreenProps<'EmailSummarySettings'>): React.JSX.Element {
  const email = useAuthStore((s) => s.user?.email) ?? '';
  const prefQ = useEmailSummaryPreference();
  const update = useUpdateEmailSummaryPreference();
  const sendNow = useSendEmailSummaryNow();

  const pref = prefQ.data;

  const [isEnabled, setIsEnabled] = useState(false);
  const [frequency, setFrequency] = useState<EmailSummary.Frequency>('Daily');
  const [timeZoneId, setTimeZoneId] = useState('Asia/Kolkata');
  const [timeOfDay, setTimeOfDay] = useState<EmailSummary.TimeOfDay>('09:00');
  const [weeklyDayOfWeek, setWeeklyDayOfWeek] = useState<EmailSummary.DayOfWeek>('Monday');
  const [monthlyDayMode, setMonthlyDayMode] = useState<EmailSummary.MonthlyDayMode>('DayOfMonth');
  const [monthlyDayOfMonth, setMonthlyDayOfMonth] = useState<string>('1');

  useEffect(() => {
    navigation.setOptions({ title: 'Email Summary' });
  }, [navigation]);

  useEffect(() => {
    if (!pref) return;
    setIsEnabled(pref.isEnabled);
    setFrequency(pref.frequency ?? 'Daily');
    const isUnconfiguredUtcDefault = !pref.isEnabled && pref.timeZoneId === 'UTC';
    setTimeZoneId(isUnconfiguredUtcDefault ? 'Asia/Kolkata' : pref.timeZoneId || 'Asia/Kolkata');
    setTimeOfDay(pref.timeOfDay ?? '09:00');
    setWeeklyDayOfWeek(pref.weeklyDayOfWeek ?? 'Monday');
    setMonthlyDayMode(pref.monthlyDayMode ?? 'DayOfMonth');
    setMonthlyDayOfMonth(pref.monthlyDayOfMonth != null ? String(pref.monthlyDayOfMonth) : '1');
  }, [pref]);

  const validation = useMemo(() => {
    if (!isEnabled) return { ok: true, message: '' };
    if (!timeZoneId.trim()) return { ok: false, message: 'Time zone required' };
    if (!timeOfDay.trim() || !isTimeOfDay(timeOfDay.trim())) {
      return { ok: false, message: 'Time must be HH:mm' };
    }

    if (frequency === 'Weekly') {
      if (!weeklyDayOfWeek) return { ok: false, message: 'Pick a weekly day' };
    }

    if (frequency === 'Monthly') {
      if (!monthlyDayMode) return { ok: false, message: 'Pick a monthly mode' };
      if (monthlyDayMode === 'DayOfMonth') {
        const n = Number(monthlyDayOfMonth);
        if (!Number.isInteger(n) || n < 1 || n > 28) {
          return { ok: false, message: 'Day of month must be 1–28' };
        }
      }
    }

    return { ok: true, message: '' };
  }, [frequency, isEnabled, monthlyDayMode, monthlyDayOfMonth, timeOfDay, timeZoneId, weeklyDayOfWeek]);

  if (prefQ.isLoading) return <LoadingOverlay />;
  if (prefQ.isError) {
    const msg = prefQ.error instanceof Error ? prefQ.error.message : 'Failed to load preferences';
    return <ErrorView message={msg} onRetry={() => void prefQ.refetch()} />;
  }

  const isBusy = update.isPending || sendNow.isPending || prefQ.isRefetching;

  const onSave = async (): Promise<void> => {
    if (!validation.ok) {
      Toast.show({ type: 'error', text1: 'Fix validation', text2: validation.message });
      return;
    }

    const req: EmailSummary.UpdatePreferenceRequest = {
      isEnabled,
      frequency: isEnabled ? frequency : null,
      timeOfDay: isEnabled ? timeOfDay.trim() : null,
      timeZoneId: isEnabled ? (timeZoneId.trim() || 'Asia/Kolkata') : null,
      weeklyDayOfWeek: isEnabled && frequency === 'Weekly' ? weeklyDayOfWeek : null,
      monthlyDayMode: isEnabled && frequency === 'Monthly' ? monthlyDayMode : null,
      monthlyDayOfMonth:
        isEnabled && frequency === 'Monthly' && monthlyDayMode === 'DayOfMonth' ? Number(monthlyDayOfMonth) : null,
    };

    try {
      await update.mutateAsync(req);
      Toast.show({ type: 'success', text1: 'Saved' });
    } catch (e: unknown) {
      Toast.show({ type: 'error', text1: 'Save failed', text2: e instanceof Error ? e.message : '' });
    }
  };

  const onSendNow = async (): Promise<void> => {
    try {
      await sendNow.mutateAsync();
      Toast.show({ type: 'success', text1: 'Queued' });
    } catch (e: unknown) {
      Toast.show({ type: 'error', text1: 'Send failed', text2: e instanceof Error ? e.message : '' });
    }
  };

  return (
    <ScrollView contentContainerStyle={styles.wrap}>
      <List.Section>
        <List.Subheader>Registered Email</List.Subheader>
        <Text variant="bodyMedium">{email || '—'}</Text>
      </List.Section>

      <Divider style={styles.divider} />

      <View style={styles.row}>
        <Text variant="titleMedium">Enabled</Text>
        <Switch value={isEnabled} onValueChange={setIsEnabled} />
      </View>

      <SegmentedButtons
        value={String(frequency)}
        onValueChange={(v) => setFrequency(v)}
        buttons={[
          { value: 'Daily', label: 'Daily' },
          { value: 'Weekly', label: 'Weekly' },
          { value: 'Monthly', label: 'Monthly' },
        ]}
        style={styles.segmented}
      />

      <TextInput
        mode="outlined"
        label="Time (HH:mm)"
        value={timeOfDay}
        onChangeText={setTimeOfDay}
        autoCapitalize="none"
        keyboardType="numbers-and-punctuation"
        style={styles.field}
      />
      <HelperText type="error" visible={isEnabled && !!timeOfDay && !isTimeOfDay(timeOfDay.trim())}>
        {isEnabled && !!timeOfDay && !isTimeOfDay(timeOfDay.trim()) ? 'Use HH:mm (e.g. 09:30)' : ' '}
      </HelperText>

      <TextInput
        mode="outlined"
        label="Time Zone ID"
        value={timeZoneId}
        onChangeText={setTimeZoneId}
        autoCapitalize="none"
        style={styles.field}
      />

      {frequency === 'Weekly' ? (
        <>
          <Text variant="titleMedium" style={styles.sectionTitle}>
            Weekly Day
          </Text>
          <SegmentedButtons
            value={String(weeklyDayOfWeek)}
            onValueChange={(v) => setWeeklyDayOfWeek(v)}
            buttons={dayOptions}
            style={styles.segmented}
          />
        </>
      ) : null}

      {frequency === 'Monthly' ? (
        <>
          <Text variant="titleMedium" style={styles.sectionTitle}>
            Monthly Mode
          </Text>
          <SegmentedButtons
            value={String(monthlyDayMode)}
            onValueChange={(v) => setMonthlyDayMode(v)}
            buttons={[
              { value: 'DayOfMonth', label: 'Day' },
              { value: 'LastDay', label: 'Last day' },
            ]}
            style={styles.segmented}
          />
          {monthlyDayMode === 'DayOfMonth' ? (
            <>
              <TextInput
                mode="outlined"
                label="Day of Month (1–28)"
                value={monthlyDayOfMonth}
                onChangeText={setMonthlyDayOfMonth}
                keyboardType="number-pad"
                style={styles.field}
              />
              <HelperText type="error" visible={isEnabled && (!!monthlyDayOfMonth && (Number(monthlyDayOfMonth) < 1 || Number(monthlyDayOfMonth) > 28))}>
                {isEnabled ? 'Must be 1–28' : ' '}
              </HelperText>
            </>
          ) : null}
        </>
      ) : null}

      <Button mode="contained" onPress={() => void onSave()} loading={update.isPending} disabled={isBusy}>
        Save
      </Button>

      <Button
        mode="outlined"
        onPress={() => void onSendNow()}
        loading={sendNow.isPending}
        disabled={isBusy}
        style={styles.sendNow}
      >
        Send Now
      </Button>

      <Divider style={styles.divider} />

      <List.Section>
        <List.Subheader>Status</List.Subheader>
        <List.Item title="Last status" description={pref?.lastSendStatus ?? '—'} />
        <List.Item title="Last attempt" description={fmt(pref?.lastSendAttemptAtUtc)} />
        <List.Item title="Last success" description={fmt(pref?.lastSendSucceededAtUtc)} />
        <List.Item title="Last manual queued" description={fmt(pref?.lastManualQueuedAtUtc)} />
        <List.Item title="Next run" description={fmt(pref?.nextRunAtUtc)} />
        {pref?.lastSendError ? (
          <List.Item title="Last error" description={pref.lastSendError} />
        ) : null}
      </List.Section>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  wrap: { padding: 16 },
  divider: { marginVertical: 16 },
  row: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 12,
  },
  field: { marginBottom: 8 },
  segmented: { marginBottom: 12 },
  sectionTitle: { marginTop: 8, marginBottom: 8 },
  sendNow: { marginTop: 8 },
});
