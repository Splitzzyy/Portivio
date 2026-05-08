/**
 * Portfolio domain types — mirror backend DTOs in
 * src/backend/Portivio.Application/DTOs/. ASP.NET serializes PascalCase C#
 * properties as camelCase JSON, so keep these camelCase.
 */

// ---------- Paging ----------
export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
  hasMore: boolean;
}

// ---------- Profile ----------
export interface Profile {
  id: string;
  userId: string;
  name: string;
  baseCurrency: string;
  description: string;
  createdAt: string;
}

export interface CreateProfileRequest {
  name: string;
  baseCurrency: string;
  description: string;
}

export interface UpdateProfileRequest {
  name: string;
  baseCurrency: string;
  description: string;
}

// ---------- Asset Type ----------
export interface AssetType {
  id: string;
  name: string;
}

export interface CreateAssetTypeRequest {
  name: string;
}

// ---------- Instrument ----------
export interface Instrument {
  id: string;
  assetTypeId: string;
  assetTypeName: string;
  name: string;
  symbol: string;
  currency: string;
}

export interface CreateInstrumentRequest {
  assetTypeId: string;
  name: string;
  symbol: string;
  currency: string;
}

export interface UpdateInstrumentRequest {
  name: string;
  symbol: string;
  currency: string;
}

// ---------- Holding ----------
export interface Holding {
  id: string;
  profileId: string;
  instrumentId: string;
  instrumentName: string;
  instrumentSymbol: string;
  assetTypeName: string;
  currency: string;
  quantity: number;
  avgPrice: number;
  currentPrice: number;
  marketValue: number;
  unrealizedPnL: number;
  lastUpdated: string;
}

export interface UpsertHoldingRequest {
  instrumentId: string;
  quantity: number;
  avgPrice: number;
  currentPrice: number;
}

// ---------- Transaction ----------
export type TransactionType = 'BUY' | 'SELL' | 'SIP' | 'DIVIDEND';

export interface Transaction {
  id: string;
  profileId: string;
  instrumentId: string;
  instrumentName: string;
  instrumentSymbol: string;
  type: string;
  quantity: number;
  price: number;
  amount: number;
  transactionDate: string;
  notes: string;
  isDeleted: boolean;
}

export interface CreateTransactionRequest {
  instrumentId: string;
  type: string;
  quantity: number;
  price: number;
  amount: number;
  transactionDate: string;
  notes: string;
}

export interface UpdateTransactionRequest {
  quantity: number;
  price: number;
  amount: number;
  transactionDate: string;
  notes: string;
}

// ---------- SIP Plan ----------
export interface SIPPlan {
  id: string;
  profileId: string;
  instrumentId: string;
  instrumentName: string;
  instrumentSymbol: string;
  amount: number;
  sipDay: number;
  startDate: string;
  endDate: string;
  isActive: boolean;
  createdAt: string;
}

export interface CreateSIPPlanRequest {
  instrumentId: string;
  amount: number;
  sipDay: number;
  startDate: string;
  endDate: string;
}

export interface UpdateSIPPlanRequest {
  amount: number;
  sipDay: number;
  startDate: string;
  endDate: string;
}

// ---------- Home aggregate ----------
export interface HomeUserInfo {
  id: string;
  email: string;
  name: string;
  isVerified: boolean;
  isActive: boolean;
  createdAt: string;
  lastLoginAt: string | null;
}

export interface PortfolioSummary {
  profileCount: number;
  holdingCount: number;
  transactionCount: number;
  activeSIPCount: number;
  totalInvestment: number;
  totalMarketValue: number;
  totalUnrealizedPnL: number;
}

export interface PortfolioPerformance {
  date: string;
  totalInvestment: number;
  currentValue: number;
  dayChange: number;
  totalReturn: number;
  xirr: number;
}

export interface HomeHolding {
  id: string;
  instrumentId: string;
  instrumentName: string;
  instrumentSymbol: string;
  currency: string;
  assetType: string;
  quantity: number;
  avgPrice: number;
  currentPrice: number;
  marketValue: number;
  unrealizedPnL: number;
  lastUpdated: string;
}

export interface HomeTransaction {
  id: string;
  instrumentId: string;
  instrumentSymbol: string;
  type: string;
  quantity: number;
  price: number;
  amount: number;
  transactionDate: string;
  notes: string;
}

export interface HomeSIPPlan {
  id: string;
  instrumentId: string;
  instrumentSymbol: string;
  amount: number;
  sipDay: number;
  startDate: string;
  endDate: string;
  isActive: boolean;
  createdAt: string;
}

export interface HomeProfile {
  id: string;
  name: string;
  baseCurrency: string;
  description: string;
  createdAt: string;
  holdings: HomeHolding[];
  transactions: HomeTransaction[];
  sipPlans: HomeSIPPlan[];
  latestPerformance: PortfolioPerformance | null;
}

export interface HomeResponse {
  user: HomeUserInfo;
  summary: PortfolioSummary;
  profiles: HomeProfile[];
}

// ---------- Asset Ingestion ----------
export interface AddStockRequest {
  name: string;
  symbol: string;
  exchange: string;
  isin?: string;
  quantity: number;
  price: number;
  date: string;
  notes?: string;
}

export interface AddMutualFundRequest {
  schemeName: string;
  schemeCode: string;
  isin?: string;
  plan?: string;
  option?: string;
  units: number;
  navPerUnit: number;
  date: string;
  notes?: string;
}

export interface AddGoldRequest {
  form: string;
  purity: string;
  weightGrams: number;
  ratePerGram: number;
  makingChargesInr: number;
  date: string;
  notes?: string;
}

export interface AddPpfRequest {
  accountNo: string;
  openedOn: string;
  currentRatePercent: number;
  initialContribution: number;
  contributionDate: string;
  notes?: string;
}

export interface AddFixedDepositRequest {
  bank: string;
  accountNo: string;
  principal: number;
  ratePercent: number;
  compounding: string;
  payoutFrequency: string;
  startDate: string;
  maturityDate: string;
  prematurePenaltyPct: number;
  notes?: string;
}

export interface AddRecurringDepositRequest {
  bank: string;
  accountNo: string;
  monthlyAmount: number;
  ratePercent: number;
  startDate: string;
  tenureMonths: number;
  notes?: string;
}

export interface AssetIngestResponse {
  instrumentId: string;
  instrumentName: string;
  symbol: string;
  transactionId: string;
  message: string;
}
