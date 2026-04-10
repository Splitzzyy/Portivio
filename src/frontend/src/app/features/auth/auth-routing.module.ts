import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { NoAuthGuard } from '../../core/guards/auth.guard';
import { LoginComponent } from './pages/login/login.component';
import { SignupComponent } from './pages/signup/signup.component';
import { ForgotPasswordComponent } from './pages/forgot-password/forgot-password.component';
import { ResetPasswordComponent } from './pages/reset-password/reset-password.component';

const routes: Routes = [
  {
    path: '',
    children: [
      {
        path: 'login',
        component: LoginComponent,
        canActivate: [NoAuthGuard],
        data: { title: 'Login - Portivio' }
      },
      {
        path: 'signup',
        component: SignupComponent,
        canActivate: [NoAuthGuard],
        data: { title: 'Sign Up - Portivio' }
      },
      {
        path: 'forgot-password',
        component: ForgotPasswordComponent,
        canActivate: [NoAuthGuard],
        data: { title: 'Forgot Password - Portivio' }
      },
      {
        path: 'reset-password/:token',
        component: ResetPasswordComponent,
        canActivate: [NoAuthGuard],
        data: { title: 'Reset Password - Portivio' }
      }
    ]
  }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class AuthRoutingModule {}
