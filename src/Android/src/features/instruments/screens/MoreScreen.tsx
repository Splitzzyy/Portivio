import React from 'react';
import { ScrollView } from 'react-native';
import { List, Divider } from 'react-native-paper';
import Toast from 'react-native-toast-message';
import Constants from 'expo-constants';

import type { MoreScreenProps } from '../../../navigation/types';
import { useProfiles } from '../../../queries/profiles';

export function MoreScreen({ navigation }: MoreScreenProps<'MoreHome'>): React.JSX.Element {
  const { data: profiles } = useProfiles();
  const firstProfileId = profiles?.[0]?.id;
  const showSip = Constants.expoConfig?.extra?.showSip ?? true;

  const requireProfile = (
    fn: (id: string) => void,
  ): (() => void) => () => {
    if (!firstProfileId) {
      Toast.show({ type: 'info', text1: 'Create a profile first' });
      return;
    }
    fn(firstProfileId);
  };

  return (
    <ScrollView>
      <List.Section>
        <List.Subheader>Catalog</List.Subheader>
        <List.Item
          title="Instruments"
          description="Stocks, funds, etc."
          left={(p) => <List.Icon {...p} icon="finance" />}
          onPress={() => navigation.navigate('InstrumentsList')}
        />
        <List.Item
          title="Asset Types"
          description="Equity, MF, ETF…"
          left={(p) => <List.Icon {...p} icon="shape" />}
          onPress={() => navigation.navigate('AssetTypesList')}
        />
        <Divider />
        <List.Subheader>Per Profile</List.Subheader>
        {showSip && (
          <List.Item
            title="SIP Plans"
            description={firstProfileId ? `Profile: ${profiles?.[0]?.name}` : 'Pick first profile'}
            left={(p) => <List.Icon {...p} icon="calendar-clock" />}
            onPress={requireProfile((id) => navigation.navigate('SipPlansList', { profileId: id }))}
          />
        )}
        <List.Item
          title="Performance"
          description={firstProfileId ? `Profile: ${profiles?.[0]?.name}` : 'Pick first profile'}
          left={(p) => <List.Icon {...p} icon="chart-line" />}
          onPress={requireProfile((id) => navigation.navigate('PerformanceScreen', { profileId: id }))}
        />
      </List.Section>
    </ScrollView>
  );
}
