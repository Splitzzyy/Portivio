import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ReactiveFormsModule } from '@angular/forms';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { MyProfileComponent } from './my-profile.component';
import { AuthService } from '../../../../core/services/auth.service';
import { of } from 'rxjs';

describe('MyProfileComponent', () => {
  let component: MyProfileComponent;
  let fixture: ComponentFixture<MyProfileComponent>;
  let authServiceSpy: jasmine.SpyObj<AuthService>;

  beforeEach(async () => {
    authServiceSpy = jasmine.createSpyObj('AuthService', ['getCurrentUser', 'updateProfile', 'changePassword']);
    authServiceSpy.getCurrentUser.and.returnValue({ id: '1', email: 'test@example.com', name: 'Test User', isVerified: true, isActive: true });
    authServiceSpy.updateProfile.and.returnValue(of({ success: true }));
    authServiceSpy.changePassword.and.returnValue(of({ success: true }));

    await TestBed.configureTestingModule({
      declarations: [MyProfileComponent],
      imports: [ReactiveFormsModule, HttpClientTestingModule],
      providers: [
        { provide: AuthService, useValue: authServiceSpy }
      ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(MyProfileComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('initializes profile form with user data', () => {
    expect(component.profileForm.get('name')?.value).toBe('Test User');
  });

  it('updates profile successfully', () => {
    component.profileForm.patchValue({ name: 'Updated User' });
    component.onUpdateProfile();
    expect(authServiceSpy.updateProfile).toHaveBeenCalledWith({ name: 'Updated User' });
    expect(component.profileSuccess).toBeTrue();
  });

  it('changes password successfully', () => {
    component.passwordForm.patchValue({
      newPassword: 'newpassword123',
      confirmPassword: 'newpassword123'
    });
    component.onChangePassword();
    expect(authServiceSpy.changePassword).toHaveBeenCalled();
    expect(component.passwordSuccess).toBeTrue();
  });

  it('validates password mismatch', () => {
    component.passwordForm.patchValue({
      newPassword: 'password1',
      confirmPassword: 'password2'
    });
    expect(component.passwordForm.hasError('mismatch')).toBeTrue();
  });
});
