import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule } from '@angular/forms';

/**
 * Shared module for common components, directives, and pipes
 * Can be imported by multiple feature modules
 */
@NgModule({
  declarations: [],
  imports: [CommonModule, ReactiveFormsModule, FormsModule],
  exports: [CommonModule, ReactiveFormsModule, FormsModule]
})
export class SharedModule {}
