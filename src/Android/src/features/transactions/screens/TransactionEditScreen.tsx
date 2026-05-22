import React, { useEffect, useState } from 'react';
import { ScrollView, StyleSheet, View } from 'react-native';
import { Button, Menu, SegmentedButtons, Text } from 'react-native-paper';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import Toast from 'react-native-toast-message';

import type { TransactionsScreenProps } from '../../../navigation/types';
import { ControlledTextInput } from '../../../components/forms/ControlledTextInput';
import {
  useCreateTransaction,
  useTransactions,
} from '../../../queries/transactions';
import { useUpdateAsset } from '../../../queries/assets';
import { useInstruments } from '../../../queries/instruments';
import { todayIso } from '../../../utils/dates';

const schema = z.object({
  instrumentId: z.string().min(1, 'Pick an instrument'),
  type: z.string().min(1),
  quantity: z.coerce.number().positive(),
  price: z.coerce.number().nonnegative(),
  amount: z.coerce.number().nonnegative(),
  transactionDate: z.string().min(1),
  addingDate: z.string().optional(),
  notes: z.string().optional().default(''),
  // Extra metadata for unified update
  name: z.string().optional(),
  symbol: z.string().optional(),
  exchange: z.string().optional().default('NSE'),
  isin: z.string().optional(),
  schemeName: z.string().optional(),
  schemeCode: z.string().optional(),
  form: z.string().optional().default('Digital'),
  purity: z.string().optional().default('24K'),
  accountNo: z.string().optional(),
  openedOn: z.string().optional(),
  currentRatePercent: z.coerce.number().optional().default(7.1),
  bank: z.string().optional(),
  compounding: z.string().optional().default('Quarterly'),
  maturityDate: z.string().optional(),
  tenureMonths: z.coerce.number().optional(),
});
type Form = z.infer<typeof schema>;

export function TransactionEditScreen({
  route,
  navigation,
}: TransactionsScreenProps<'TransactionEdit'>): React.JSX.Element {
  const { profileId, transactionId } = route.params;
  const { data: pages } = useTransactions(profileId);
  const existing = pages?.pages.flatMap((p) => p.items).find((t) => t.id === transactionId);
  const { data: instruments } = useInstruments();
  const create = useCreateTransaction(profileId);
  const updateAsset = useUpdateAsset(profileId);
  const [menuOpen, setMenuOpen] = useState(false);

  const { control, handleSubmit, reset, setValue, watch, formState: { isSubmitting } } =
    useForm<Form>({
      resolver: zodResolver(schema),
      defaultValues: {
        instrumentId: '',
        type: 'Buy',
        quantity: 0,
        price: 0,
        amount: 0,
        transactionDate: todayIso().slice(0, 10),
        addingDate: 'Today (Auto)',
        notes: '',
        exchange: 'NSE',
        form: 'Digital',
        purity: '24K',
        currentRatePercent: 7.1,
        compounding: 'Quarterly',
      },
    });

  const selectedId = watch('instrumentId');
  const selected = instruments?.find((i) => i.id === selectedId);
  const txType = watch('type');
  const assetType = (selected?.assetTypeName ?? '').toLowerCase();

  useEffect(() => {
    if (existing) {
      reset({
        instrumentId: existing.instrumentId,
        type: existing.type,
        quantity: existing.quantity,
        price: existing.price,
        amount: existing.amount,
        transactionDate: existing.transactionDate.slice(0, 10),
        addingDate: existing.createdAtUtc ? new Date(existing.createdAtUtc).toLocaleDateString() : 'Today (Auto)',
        notes: existing.notes,
        // Pre-fill metadata from existing transaction/instrument
        name: existing.instrumentName,
        symbol: existing.instrumentSymbol,
        schemeName: existing.instrumentName,
        schemeCode: existing.instrumentSymbol,
      });
    }
  }, [existing, reset]);

  const onSubmit = handleSubmit(async (data) => {
    const dateIso = new Date(`${data.transactionDate}T00:00:00Z`).toISOString();
    try {
      if (transactionId && selected) {
        // Use unified asset update for edits
        let req: any = {};
        if (assetType.includes('stock') || assetType.includes('equity')) {
          req = {
            name: data.name || selected.name,
            symbol: (data.symbol || selected.symbol).toUpperCase(),
            exchange: data.exchange || 'NSE',
            isin: data.isin || undefined,
            quantity: data.quantity,
            price: data.price,
            date: dateIso,
            notes: data.notes,
          };
        } else if (assetType.includes('mutual') || assetType.includes('fund')) {
          req = {
            schemeName: data.schemeName || selected.name,
            schemeCode: data.schemeCode || selected.symbol,
            isin: data.isin || undefined,
            units: data.quantity,
            navPerUnit: data.price,
            date: dateIso,
            notes: data.notes,
          };
        } else if (assetType.includes('gold')) {
          const form = data.form || 'Digital';
          req = {
            form,
            purity: form.toLowerCase() === 'digital' ? '24K' : (data.purity || '24K'),
            weightGrams: data.quantity,
            ratePerGram: data.price,
            makingChargesInr: 0,
            date: dateIso,
            notes: data.notes,
          };
        } else if (assetType.includes('ppf')) {
          req = {
            accountNo: data.accountNo || '',
            openedOn: data.openedOn || dateIso, // Fallback if unknown
            currentRatePercent: data.currentRatePercent || 7.1,
            initialContribution: data.quantity,
            contributionDate: dateIso,
            notes: data.notes,
          };
        } else if (assetType.includes('fixed') || assetType === 'fd') {
          req = {
            bank: data.bank || 'Other',
            accountNo: data.accountNo || '',
            principal: data.quantity,
            ratePercent: data.currentRatePercent || 0,
            compounding: data.compounding || 'Quarterly',
            payoutFrequency: 'OnMaturity',
            startDate: dateIso,
            maturityDate: data.maturityDate || dateIso,
            prematurePenaltyPct: 0,
            notes: data.notes,
          };
        } else if (assetType.includes('recurring') || assetType === 'rd') {
          req = {
            bank: data.bank || 'Other',
            accountNo: data.accountNo || '',
            monthlyAmount: data.quantity,
            ratePercent: data.currentRatePercent || 0,
            startDate: dateIso,
            tenureMonths: data.tenureMonths || 12,
            notes: data.notes,
          };
        }

        await updateAsset.mutateAsync({
          type: assetType,
          instrumentId: selected.id,
          req,
        });
      } else {
        await create.mutateAsync({
          instrumentId: data.instrumentId,
          type: data.type,
          quantity: data.quantity,
          price: data.price,
          amount: data.amount,
          transactionDate: dateIso,
          notes: data.notes ?? '',
        });
      }
      Toast.show({ type: 'success', text1: transactionId ? 'Updated' : 'Created' });
      navigation.goBack();
    } catch (e: unknown) {
      Toast.show({
        type: 'error',
        text1: 'Save failed',
        text2: e instanceof Error ? e.message : '',
      });
    }
  });

  const renderExtraFields = () => {
    if (!selected) return null;

    if (assetType.includes('stock') || assetType.includes('equity')) {
      return (
        <>
          <ControlledTextInput name="name" control={control} label="Stock Name" />
          <ControlledTextInput name="symbol" control={control} label="Symbol" autoCapitalize="characters" />
          <ControlledTextInput name="exchange" control={control} label="Exchange (NSE/BSE)" />
          <ControlledTextInput name="isin" control={control} label="ISIN" autoCapitalize="characters" />
        </>
      );
    }

    if (assetType.includes('mutual') || assetType.includes('fund')) {
      return (
        <>
          <ControlledTextInput name="schemeName" control={control} label="Scheme Name" />
          <ControlledTextInput name="schemeCode" control={control} label="Scheme Code" />
          <ControlledTextInput name="isin" control={control} label="ISIN" autoCapitalize="characters" />
        </>
      );
    }

    if (assetType.includes('gold')) {
      return (
        <>
          <ControlledTextInput name="form" control={control} label="Form (Digital/Physical/SGB)" />
          <ControlledTextInput name="purity" control={control} label="Purity (Digital is 24K)" />
        </>
      );
    }

    if (assetType.includes('ppf')) {
      return (
        <>
          <ControlledTextInput name="accountNo" control={control} label="Account No" />
          <ControlledTextInput name="openedOn" control={control} label="Opened On (YYYY-MM-DD)" />
          <ControlledTextInput name="currentRatePercent" control={control} label="Interest Rate (%)" keyboardType="decimal-pad" />
        </>
      );
    }

    if (assetType.includes('fixed') || assetType.includes('recurring') || assetType === 'fd' || assetType === 'rd') {
      return (
        <>
          <ControlledTextInput name="bank" control={control} label="Bank" />
          <ControlledTextInput name="accountNo" control={control} label="Account No" />
          <ControlledTextInput name="currentRatePercent" control={control} label="Interest Rate (%)" keyboardType="decimal-pad" />
          {assetType.includes('fixed') || assetType === 'fd' ? (
            <ControlledTextInput name="maturityDate" control={control} label="Maturity Date (YYYY-MM-DD)" />
          ) : (
            <ControlledTextInput name="tenureMonths" control={control} label="Tenure (Months)" keyboardType="numeric" />
          )}
        </>
      );
    }

    return null;
  };

  return (
    <ScrollView contentContainerStyle={styles.wrap}>
      <Text variant="bodySmall" style={styles.label}>Instrument</Text>
      <Menu
        visible={menuOpen}
        onDismiss={() => setMenuOpen(false)}
        anchor={
          <Button
            mode="outlined"
            onPress={() => setMenuOpen(true)}
            disabled={!!transactionId}
            style={styles.picker}
          >
            {selected ? `${selected.symbol} — ${selected.name}` : 'Select instrument'}
          </Button>
        }
      >
        {(instruments ?? []).map((i) => (
          <Menu.Item
            key={i.id}
            title={`${i.symbol} — ${i.name}`}
            onPress={() => {
              setValue('instrumentId', i.id, { shouldValidate: true });
              setMenuOpen(false);
            }}
          />
        ))}
      </Menu>

      <Text variant="bodySmall" style={[styles.label, { marginTop: 12 }]}>Type</Text>
      <SegmentedButtons
        value={txType}
        onValueChange={(v) => setValue('type', v, { shouldValidate: true })}
        buttons={[
          { value: 'Buy', label: 'Buy' },
          { value: 'Sell', label: 'Sell' },
        ]}
        style={{ marginBottom: 12 }}
      />

      <ControlledTextInput
        name="quantity"
        control={control}
        label={assetType.includes('mutual') ? 'Units' : assetType.includes('gold') ? 'Grams' : 'Quantity'}
        keyboardType="decimal-pad"
      />
      <ControlledTextInput
        name="price"
        control={control}
        label={assetType.includes('mutual') ? 'NAV' : 'Price'}
        keyboardType="decimal-pad"
      />
      <ControlledTextInput
        name="amount"
        control={control}
        label="Total Amount"
        keyboardType="decimal-pad"
      />
      <ControlledTextInput
        name="transactionDate"
        control={control}
        label="Transaction Date (YYYY-MM-DD)"
      />

      <View style={styles.divider} />
      <Text variant="titleMedium" style={{ marginBottom: 8 }}>Asset Details</Text>
      {renderExtraFields()}

      <View style={styles.divider} />
      <ControlledTextInput
        name="addingDate"
        control={control}
        label="Adding Date"
        editable={false}
        style={{ backgroundColor: '#f3f4f6' }}
      />
      <ControlledTextInput name="notes" control={control} label="Notes" multiline numberOfLines={2} />

      <Button
        mode="contained"
        onPress={onSubmit}
        loading={isSubmitting || create.isPending || updateAsset.isPending}
        disabled={isSubmitting || create.isPending || updateAsset.isPending}
        style={{ marginTop: 16 }}
      >
        {transactionId ? 'Update Investment' : 'Create Transaction'}
      </Button>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  wrap: { padding: 16 },
  label: { opacity: 0.7, marginBottom: 4 },
  picker: { alignSelf: 'flex-start' },
  divider: { height: 1, backgroundColor: '#e5e7eb', marginVertical: 16 },
});
