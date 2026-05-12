import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SharedModule } from '../../shared/shared.module';
import { HomeRoutingModule } from './home-routing.module';
import { HomeComponent } from './pages/home/home.component';
import { DashboardComponent } from './pages/dashboard/dashboard.component';
import { ProfilesComponent } from './pages/profiles/profiles.component';
import { HoldingsComponent } from './pages/holdings/holdings.component';
import { TransactionsComponent } from './pages/transactions/transactions.component';
import { SipPlansComponent } from './pages/sip-plans/sip-plans.component';
import { AddInvestmentComponent } from './pages/add-investment/add-investment.component';
import { MyProfileComponent } from './pages/my-profile/my-profile.component';

@NgModule({
  declarations: [
    HomeComponent,
    DashboardComponent,
    ProfilesComponent,
    HoldingsComponent,
    TransactionsComponent,
    SipPlansComponent,
    AddInvestmentComponent,
    MyProfileComponent
  ],
  imports: [CommonModule, SharedModule, HomeRoutingModule]
})
export class HomeModule {}
