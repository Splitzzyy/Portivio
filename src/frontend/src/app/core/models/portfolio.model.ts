/**
 * User profile model for financial portfolio
 */
export interface UserProfile {
  id: string;
  userId: string;
  totalInvestment: number;
  totalValue: number;
  totalReturns: number;
  returnPercentage: number;
  currency: string;
  riskProfile: 'conservative' | 'moderate' | 'aggressive';
  numberOfSIPs: number;
  numberOfAssets: number;
  lastUpdated: Date;
}

/**
 * Portfolio overview
 */
export interface PortfolioOverview {
  totalInvestment: number;
  currentValue: number;
  totalReturns: number;
  returnPercentage: number;
  topPerformer: string;
  worstPerformer: string;
  diversification: DiversificationData[];
}

/**
 * Asset allocation data
 */
export interface DiversificationData {
  category: string;
  percentage: number;
  value: number;
}
