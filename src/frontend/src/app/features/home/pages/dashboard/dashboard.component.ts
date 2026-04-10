import { Component, OnInit } from '@angular/core';
import { User } from '../../../../core/models/auth.model';
import { AuthService } from '../../../../core/services/auth.service';

/**
 * Dashboard component - Main portfolio overview
 */
@Component({
  selector: 'app-dashboard',
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.scss']
})
export class DashboardComponent implements OnInit {
  user: User | null = null;
  
  // Portfolio statistics (mock data)
  portfolioStats = {
    totalInvestment: 500000,
    currentValue: 625000,
    totalReturns: 125000,
    returnPercentage: 25
  };

  // Asset allocation
  assetAllocation = [
    { name: 'Stocks', percentage: 45, value: 281250 },
    { name: 'Mutual Funds', percentage: 30, value: 187500 },
    { name: 'Bonds', percentage: 15, value: 93750 },
    { name: 'Cash', percentage: 10, value: 62500 }
  ];

  // Recent transactions
  recentTransactions = [
    {
      id: 1,
      type: 'BUY',
      security: 'TCS Stock',
      amount: 50000,
      date: new Date('2024-04-10'),
      status: 'Completed'
    },
    {
      id: 2,
      type: 'SIP',
      security: 'Axis Growth Fund',
      amount: 5000,
      date: new Date('2024-04-09'),
      status: 'Completed'
    },
    {
      id: 3,
      type: 'SELL',
      security: 'Infosys Stock',
      amount: 30000,
      date: new Date('2024-04-08'),
      status: 'Completed'
    },
    {
      id: 4,
      type: 'DIVIDEND',
      security: 'HDFC Bank',
      amount: 2500,
      date: new Date('2024-04-07'),
      status: 'Received'
    }
  ];

  // Portfolio performance (mock data)
  portfolioPerformance = [
    { date: 'Jan', value: 500000 },
    { date: 'Feb', value: 510000 },
    { date: 'Mar', value: 540000 },
    { date: 'Apr', value: 625000 }
  ];

  constructor(private authService: AuthService) {}

  ngOnInit(): void {
    this.user = this.authService.getCurrentUser();
  }

  /** First token of the single `name` field; falls back to "there". */
  getFirstName(): string {
    const first = (this.user?.name || '').trim().split(/\s+/)[0];
    return first || 'there';
  }

  /**
   * Get transaction type badge color
   */
  getTransactionTypeColor(type: string): string {
    switch (type) {
      case 'BUY':
        return 'success';
      case 'SELL':
        return 'danger';
      case 'SIP':
        return 'primary';
      case 'DIVIDEND':
        return 'info';
      default:
        return 'secondary';
    }
  }

  /**
   * Get transaction type icon
   */
  getTransactionTypeIcon(type: string): string {
    switch (type) {
      case 'BUY':
        return 'fa-arrow-down';
      case 'SELL':
        return 'fa-arrow-up';
      case 'SIP':
        return 'fa-repeat';
      case 'DIVIDEND':
        return 'fa-coins';
      default:
        return 'fa-exchange-alt';
    }
  }

  /**
   * Format currency
   */
  formatCurrency(value: number): string {
    return '₹' + value.toLocaleString('en-IN');
  }

  /**
   * Format date
   */
  formatDate(date: Date): string {
    return new Date(date).toLocaleDateString('en-IN', {
      year: 'numeric',
      month: 'short',
      day: 'numeric'
    });
  }
}
