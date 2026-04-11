import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule } from '@angular/forms';
import { LoadingSpinnerComponent } from './components/loading-spinner/loading-spinner.component';

/**
 * Shared module for common components, directives, and pipes.
 * Can be imported by multiple feature modules.
 */
@NgModule({
  declarations: [LoadingSpinnerComponent],
  imports: [CommonModule, ReactiveFormsModule, FormsModule],
  exports: [
    CommonModule,
    ReactiveFormsModule,
    FormsModule,
    LoadingSpinnerComponent
  ]
})
export class SharedModule {}
