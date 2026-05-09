// Mirrors Portivio.Application/DTOs — keep in sync manually until openapi-typescript is wired.
// Backend serializes camelCase via System.Text.Json defaults.

export type Guid = string;
export type Iso = string;

export namespace Auth {
  export interface UserDto {
    id: Guid;
    email: string;
    name: string;
    isVerified: boolean;
    isActive: boolean;
  }

  export interface AuthResponse {
    success: boolean;
    message?: string | null;
    accessToken?: string | null;
    refreshToken?: string | null;
    user?: UserDto | null;
    accessTokenExpiry?: Iso | null;
    refreshTokenExpiry?: Iso | null;
  }

  export interface LoginRequest {
    email: string;
    password: string;
  }

  export interface SignupRequest {
    email: string;
    name: string;
    password: string;
    confirmPassword: string;
  }

  export interface RefreshTokenRequest {
    refreshToken?: string | null;
  }

  export interface ForgotPasswordRequest {
    email: string;
  }

  export interface ResetPasswordRequest {
    email: string;
    resetToken: string;
    newPassword: string;
    confirmPassword: string;
  }

  export interface VerifyEmailRequest {
    email: string;
    verificationToken: string;
  }

  export interface GoogleLoginRequest {
    token: string;
  }
}

export namespace Home {
  export interface UserInfo {
    id: Guid;
    email: string;
    name: string;
    isVerified: boolean;
    isActive: boolean;
    createdAt: Iso;
    lastLoginAt?: Iso | null;
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

  export interface ProfileBundle {
    id: Guid;
    name: string;
    baseCurrency: string;
    description: string;
    createdAt: Iso;
    holdings: HoldingBundle[];
    transactions: TransactionBundle[];
    sipPlans: SipPlanBundle[];
    latestPerformance?: PerformanceBundle | null;
  }

  export interface HoldingBundle {
    id: Guid;
    instrumentId: Guid;
    instrumentName: string;
    instrumentSymbol: string;
    currency: string;
    assetType: string;
    quantity: number;
    avgPrice: number;
    currentPrice: number;
    marketValue: number;
    unrealizedPnL: number;
    lastUpdated: Iso;
  }

  export interface TransactionBundle {
    id: Guid;
    instrumentId: Guid;
    instrumentSymbol: string;
    type: string;
    quantity: number;
    price: number;
    amount: number;
    transactionDate: Iso;
    notes: string;
  }

  export interface SipPlanBundle {
    id: Guid;
    instrumentId: Guid;
    instrumentSymbol: string;
    amount: number;
    sipDay: number;
    startDate: Iso;
    endDate: Iso;
    isActive: boolean;
    createdAt: Iso;
  }

  export interface PerformanceBundle {
    date: Iso;
    totalInvestment: number;
    currentValue: number;
    dayChange: number;
    totalReturn: number;
    xirr: number;
  }

  export interface HomeResponse {
    user: UserInfo;
    summary: PortfolioSummary;
    profiles: ProfileBundle[];
  }
}

export namespace Profiles {
  export interface CreateRequest {
    name: string;
    baseCurrency: string;
    description: string;
  }
  export type UpdateRequest = CreateRequest;

  export interface Response {
    id: Guid;
    userId: Guid;
    name: string;
    baseCurrency: string;
    description: string;
    createdAt: Iso;
  }
}

export namespace Holdings {
  export interface UpsertRequest {
    instrumentId: Guid;
    quantity: number;
    avgPrice: number;
    currentPrice: number;
  }

  export interface Response {
    id: Guid;
    profileId: Guid;
    instrumentId: Guid;
    instrumentName: string;
    instrumentSymbol: string;
    assetTypeName: string;
    currency: string;
    quantity: number;
    avgPrice: number;
    currentPrice: number;
    marketValue: number;
    unrealizedPnL: number;
    lastUpdated: Iso;
  }
}

export namespace Transactions {
  export type TxType = 'Buy' | 'Sell' | string;

  export interface CreateRequest {
    instrumentId: Guid;
    type: TxType;
    quantity: number;
    price: number;
    amount: number;
    transactionDate: Iso;
    notes: string;
  }

  export interface UpdateRequest {
    quantity: number;
    price: number;
    amount: number;
    transactionDate: Iso;
    notes: string;
  }

  export interface Response {
    id: Guid;
    profileId: Guid;
    instrumentId: Guid;
    instrumentName: string;
    instrumentSymbol: string;
    type: TxType;
    quantity: number;
    price: number;
    amount: number;
    transactionDate: Iso;
    notes: string;
    createdAtUtc: Iso;
  }
}

export namespace Instruments {
  export interface CreateAssetType {
    name: string;
  }
  export interface AssetTypeResponse {
    id: Guid;
    name: string;
  }
  export interface CreateInstrumentRequest {
    assetTypeId: Guid;
    name: string;
    symbol: string;
    currency: string;
  }
  export interface UpdateInstrumentRequest {
    name: string;
    symbol: string;
    currency: string;
  }
  export interface Response {
    id: Guid;
    assetTypeId: Guid;
    assetTypeName: string;
    name: string;
    symbol: string;
    currency: string;
  }
}

export namespace SipPlans {
  export interface CreateRequest {
    instrumentId: Guid;
    amount: number;
    sipDay: number;
    startDate: Iso;
    endDate: Iso;
  }
  export interface UpdateRequest {
    amount: number;
    sipDay: number;
    startDate: Iso;
    endDate: Iso;
  }
  export interface Response {
    id: Guid;
    profileId: Guid;
    instrumentId: Guid;
    instrumentName: string;
    instrumentSymbol: string;
    amount: number;
    sipDay: number;
    startDate: Iso;
    endDate: Iso;
    isActive: boolean;
    createdAt: Iso;
  }
}

export namespace Performance {
  export interface RecordSnapshotRequest {
    date?: Iso | null;
  }
  export interface Response {
    id: Guid;
    profileId: Guid;
    date: Iso;
    totalInvestment: number;
    currentValue: number;
    dayChange: number;
    totalReturn: number;
    xirr: number;
    createdAt: Iso;
  }
  export interface HistoryResponse {
    history: Response[];
    latest?: Response | null;
  }
}

export namespace PriceHistory {
  export interface AddPriceRequest {
    price: number;
    date: Iso;
    source: string;
  }
  export interface BulkAddPriceRequest {
    prices: AddPriceRequest[];
  }
  export interface Response {
    id: Guid;
    instrumentId: Guid;
    price: number;
    date: Iso;
    source: string;
    createdAt: Iso;
  }
  export interface BulkAddPriceResponse {
    inserted: number;
    skipped: number;
    errors: string[];
  }
}

export interface ApiErrorBody {
  message?: string;
  errors?: string[] | Record<string, string[]>;
  statusCode?: number;
}
