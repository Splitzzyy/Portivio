import { NgModule } from '@angular/core';
import { RouterModule } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';
import { LandingRoutingModule } from './landing-routing.module';
import { LandingComponent } from './pages/landing/landing.component';

/**
 * Public marketing landing page. Not lazy-loaded: ships in the main bundle
 * so the `/` route has instant FCP for first-time visitors.
 */
@NgModule({
  declarations: [LandingComponent],
  imports: [SharedModule, RouterModule, LandingRoutingModule]
})
export class LandingModule {}
