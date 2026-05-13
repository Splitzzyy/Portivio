import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { AuthService } from '../../../../core/services/auth.service';

@Component({
  selector: 'app-my-profile',
  templateUrl: './my-profile.component.html',
  styleUrls: ['../shared-page.scss', './my-profile.component.scss']
})
export class MyProfileComponent implements OnInit {
  profileForm: FormGroup;
  passwordForm: FormGroup;

  profileUpdating = false;
  profileSuccess = false;
  profileError = '';

  passwordUpdating = false;
  passwordSuccess = false;
  passwordError = '';

  constructor(private fb: FormBuilder, private authService: AuthService) {
    this.profileForm = this.fb.group({
      name: ['', [Validators.required, Validators.minLength(2)]]
    });

    this.passwordForm = this.fb.group({
      newPassword: ['', [Validators.required, Validators.minLength(6)]],
      confirmPassword: ['', Validators.required]
    }, { validators: this.passwordMatchValidator });
  }

  ngOnInit(): void {
    const user = this.authService.getCurrentUser();
    if (user) {
      this.profileForm.patchValue({ name: user.name });
    }
  }

  passwordMatchValidator(g: FormGroup) {
    return g.get('newPassword')?.value === g.get('confirmPassword')?.value
      ? null : { mismatch: true };
  }

  onUpdateProfile(): void {
    if (this.profileForm.invalid) return;

    this.profileUpdating = true;
    this.profileSuccess = false;
    this.profileError = '';

    this.authService.updateProfile(this.profileForm.value).subscribe({
      next: () => {
        this.profileUpdating = false;
        this.profileSuccess = true;
        setTimeout(() => this.profileSuccess = false, 3000);
      },
      error: (err) => {
        this.profileUpdating = false;
        this.profileError = err.error?.message || 'Failed to update profile';
      }
    });
  }

  onChangePassword(): void {
    if (this.passwordForm.invalid) return;

    this.passwordUpdating = true;
    this.passwordSuccess = false;
    this.passwordError = '';

    this.authService.changePassword(this.passwordForm.value).subscribe({
      next: () => {
        this.passwordUpdating = false;
        this.passwordSuccess = true;
        this.passwordForm.reset();
        setTimeout(() => this.passwordSuccess = false, 3000);
      },
      error: (err) => {
        this.passwordUpdating = false;
        this.passwordError = err.error?.message || 'Failed to change password';
      }
    });
  }
}
