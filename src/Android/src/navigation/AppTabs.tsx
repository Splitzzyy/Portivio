import React from 'react';
import { createBottomTabNavigator } from '@react-navigation/bottom-tabs';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import { Text } from 'react-native-paper';

import type {
  AppTabsParamList,
  DashboardStackParamList,
  ProfilesStackParamList,
  HoldingsStackParamList,
  TransactionsStackParamList,
  MoreStackParamList,
} from './types';

import { DashboardScreen } from '../features/home/screens/DashboardScreen';
import { ProfilesListScreen } from '../features/profiles/screens/ProfilesListScreen';
import { ProfileEditScreen } from '../features/profiles/screens/ProfileEditScreen';
import { ProfilePickerScreen } from '../features/holdings/screens/ProfilePickerScreen';
import { HoldingsListScreen } from '../features/holdings/screens/HoldingsListScreen';
import { HoldingEditScreen } from '../features/holdings/screens/HoldingEditScreen';
import { TransactionsProfilePicker } from '../features/transactions/screens/TransactionsProfilePicker';
import { TransactionsListScreen } from '../features/transactions/screens/TransactionsListScreen';
import { TransactionEditScreen } from '../features/transactions/screens/TransactionEditScreen';
import { MoreScreen } from '../features/instruments/screens/MoreScreen';
import { SipPlansListScreen } from '../features/sip-plans/screens/SipPlansListScreen';
import { SipPlanEditScreen } from '../features/sip-plans/screens/SipPlanEditScreen';
import { InstrumentsListScreen } from '../features/instruments/screens/InstrumentsListScreen';
import { InstrumentEditScreen } from '../features/instruments/screens/InstrumentEditScreen';
import { AssetTypesListScreen } from '../features/instruments/screens/AssetTypesListScreen';
import { PerformanceScreen } from '../features/performance/screens/PerformanceScreen';

const Tabs = createBottomTabNavigator<AppTabsParamList>();
const DashStack = createNativeStackNavigator<DashboardStackParamList>();
const ProfilesNav = createNativeStackNavigator<ProfilesStackParamList>();
const HoldingsNav = createNativeStackNavigator<HoldingsStackParamList>();
const TxNav = createNativeStackNavigator<TransactionsStackParamList>();
const MoreNav = createNativeStackNavigator<MoreStackParamList>();

function DashboardStack(): React.JSX.Element {
  return (
    <DashStack.Navigator>
      <DashStack.Screen
        name="DashboardHome"
        component={DashboardScreen}
        options={{ title: 'Portivio' }}
      />
    </DashStack.Navigator>
  );
}

function ProfilesStack(): React.JSX.Element {
  return (
    <ProfilesNav.Navigator>
      <ProfilesNav.Screen
        name="ProfilesList"
        component={ProfilesListScreen}
        options={{ title: 'Profiles' }}
      />
      <ProfilesNav.Screen
        name="ProfileEdit"
        component={ProfileEditScreen}
        options={({ route }) => ({
          title: route.params?.profileId ? 'Edit Profile' : 'New Profile',
        })}
      />
    </ProfilesNav.Navigator>
  );
}

function HoldingsStack(): React.JSX.Element {
  return (
    <HoldingsNav.Navigator>
      <HoldingsNav.Screen
        name="ProfilePicker"
        component={ProfilePickerScreen}
        options={{ title: 'Holdings' }}
      />
      <HoldingsNav.Screen
        name="HoldingsList"
        component={HoldingsListScreen}
        options={{ title: 'Holdings' }}
      />
      <HoldingsNav.Screen
        name="HoldingEdit"
        component={HoldingEditScreen}
        options={{ title: 'Holding' }}
      />
    </HoldingsNav.Navigator>
  );
}

function TransactionsStack(): React.JSX.Element {
  return (
    <TxNav.Navigator>
      <TxNav.Screen
        name="ProfilePicker"
        component={TransactionsProfilePicker}
        options={{ title: 'Transactions' }}
      />
      <TxNav.Screen
        name="TransactionsList"
        component={TransactionsListScreen}
        options={{ title: 'Transactions' }}
      />
      <TxNav.Screen
        name="TransactionEdit"
        component={TransactionEditScreen}
        options={{ title: 'Transaction' }}
      />
    </TxNav.Navigator>
  );
}

function MoreStack(): React.JSX.Element {
  return (
    <MoreNav.Navigator>
      <MoreNav.Screen name="MoreHome" component={MoreScreen} options={{ title: 'More' }} />
      <MoreNav.Screen
        name="SipPlansList"
        component={SipPlansListScreen}
        options={{ title: 'SIP Plans' }}
      />
      <MoreNav.Screen
        name="SipPlanEdit"
        component={SipPlanEditScreen}
        options={{ title: 'SIP Plan' }}
      />
      <MoreNav.Screen
        name="InstrumentsList"
        component={InstrumentsListScreen}
        options={{ title: 'Instruments' }}
      />
      <MoreNav.Screen
        name="InstrumentEdit"
        component={InstrumentEditScreen}
        options={{ title: 'Instrument' }}
      />
      <MoreNav.Screen
        name="AssetTypesList"
        component={AssetTypesListScreen}
        options={{ title: 'Asset Types' }}
      />
      <MoreNav.Screen
        name="PerformanceScreen"
        component={PerformanceScreen}
        options={{ title: 'Performance' }}
      />
    </MoreNav.Navigator>
  );
}

const tabIcon = (label: string) => () => <Text style={{ fontSize: 11 }}>{label}</Text>;

export function AppTabs(): React.JSX.Element {
  return (
    <Tabs.Navigator screenOptions={{ headerShown: false }}>
      <Tabs.Screen
        name="Dashboard"
        component={DashboardStack}
        options={{ tabBarIcon: tabIcon('🏠'), tabBarLabel: 'Home' }}
      />
      <Tabs.Screen
        name="Profiles"
        component={ProfilesStack}
        options={{ tabBarIcon: tabIcon('👤'), tabBarLabel: 'Profiles' }}
      />
      <Tabs.Screen
        name="Holdings"
        component={HoldingsStack}
        options={{ tabBarIcon: tabIcon('📊'), tabBarLabel: 'Holdings' }}
      />
      <Tabs.Screen
        name="Transactions"
        component={TransactionsStack}
        options={{ tabBarIcon: tabIcon('💸'), tabBarLabel: 'Txns' }}
      />
      <Tabs.Screen
        name="More"
        component={MoreStack}
        options={{ tabBarIcon: tabIcon('⋯'), tabBarLabel: 'More' }}
      />
    </Tabs.Navigator>
  );
}
