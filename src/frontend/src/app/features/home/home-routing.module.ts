import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { AuthGuard } from '../../core/guards/auth.guard';
import { HomeComponent } from './pages/home/home.component';
import { DashboardComponent } from './pages/dashboard/dashboard.component';
import { ProfilesComponent } from './pages/profiles/profiles.component';
import { HoldingsComponent } from './pages/holdings/holdings.component';
import { TransactionsComponent } from './pages/transactions/transactions.component';
import { SipPlansComponent } from './pages/sip-plans/sip-plans.component';
import { InstrumentsComponent } from './pages/instruments/instruments.component';

const routes: Routes = [
  {
    path: '',
    component: HomeComponent,
    canActivate: [AuthGuard],
    children: [
      { path: '', component: DashboardComponent, data: { title: 'Dashboard - Portivio' } },
      { path: 'profiles', component: ProfilesComponent, data: { title: 'Profiles - Portivio' } },
      { path: 'holdings', component: HoldingsComponent, data: { title: 'Holdings - Portivio' } },
      { path: 'transactions', component: TransactionsComponent, data: { title: 'Transactions - Portivio' } },
      { path: 'sip-plans', component: SipPlansComponent, data: { title: 'SIP Plans - Portivio' } },
      { path: 'instruments', component: InstrumentsComponent, data: { title: 'Instruments - Portivio' } }
    ]
  }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class HomeRoutingModule {}
