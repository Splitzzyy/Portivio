import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import type { BottomTabScreenProps } from '@react-navigation/bottom-tabs';

export type AuthStackParamList = {
  Login: undefined;
  Signup: undefined;
  ForgotPassword: undefined;
  ResetPassword: { token?: string; email?: string };
  VerifyEmail: { token?: string; email?: string };
};

export type DashboardStackParamList = {
  DashboardHome: undefined;
};

export type ProfilesStackParamList = {
  ProfilesList: undefined;
  ProfileEdit: { profileId?: string };
};

export type HoldingsStackParamList = {
  ProfilePicker: undefined;
  HoldingsList: { profileId: string };
  HoldingEdit: { profileId: string; holdingId?: string };
};

export type TransactionsStackParamList = {
  ProfilePicker: undefined;
  TransactionsList: { profileId: string };
  TransactionEdit: { profileId: string; transactionId?: string };
};

export type MoreStackParamList = {
  MoreHome: undefined;
  SipPlansList: { profileId: string };
  SipPlanEdit: { profileId: string; sipId?: string };
  InstrumentsList: undefined;
  InstrumentEdit: { instrumentId?: string };
  AssetTypesList: undefined;
  PerformanceScreen: { profileId: string };
};

export type AppTabsParamList = {
  Dashboard: undefined;
  Profiles: undefined;
  Holdings: undefined;
  Transactions: undefined;
  More: undefined;
};

export type AuthScreenProps<T extends keyof AuthStackParamList> = NativeStackScreenProps<
  AuthStackParamList,
  T
>;
export type ProfilesScreenProps<T extends keyof ProfilesStackParamList> = NativeStackScreenProps<
  ProfilesStackParamList,
  T
>;
export type HoldingsScreenProps<T extends keyof HoldingsStackParamList> = NativeStackScreenProps<
  HoldingsStackParamList,
  T
>;
export type TransactionsScreenProps<T extends keyof TransactionsStackParamList> =
  NativeStackScreenProps<TransactionsStackParamList, T>;
export type MoreScreenProps<T extends keyof MoreStackParamList> = NativeStackScreenProps<
  MoreStackParamList,
  T
>;
export type TabScreenProps<T extends keyof AppTabsParamList> = BottomTabScreenProps<
  AppTabsParamList,
  T
>;
