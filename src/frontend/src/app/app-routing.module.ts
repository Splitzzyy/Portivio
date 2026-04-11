import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { LandingComponent } from './features/landing/pages/landing/landing.component';

/**
 * Root routing table.
 *   - `/`           → public LandingComponent (from LandingModule, not lazy)
 *   - `/auth/*`     → AuthModule (login/signup/forgot/reset)
 *   - `/dashboard`  → HomeModule (protected authenticated shell)
 *   - `**`          → fallback to landing, not dashboard, so unauthenticated
 *                     deep-links don't bounce through AuthGuard → login.
 */
const routes: Routes = [
  {
    path: '',
    component: LandingComponent,
    pathMatch: 'full',
    data: { title: 'Portivio - Smart Portfolio Management' }
  },
  {
    path: 'auth',
    loadChildren: () =>
      import('./features/auth/auth.module').then(m => m.AuthModule)
  },
  {
    path: 'dashboard',
    loadChildren: () =>
      import('./features/home/home.module').then(m => m.HomeModule)
  },
  {
    path: '**',
    redirectTo: ''
  }
];

@NgModule({
  imports: [RouterModule.forRoot(routes, {
    enableTracing: false,
    scrollPositionRestoration: 'enabled'
  })],
  exports: [RouterModule]
})
export class AppRoutingModule {}
