import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { AuthService } from './auth.service';
import { environment } from '../../../environments/environment';
import { UpdateUserProfileRequest, ChangePasswordRequest, AuthResponse } from '../models/auth.model';

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [AuthService]
    });
    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  describe('updateProfile', () => {
    it('should update profile via PUT and emit new user data', () => {
      const mockRequest: UpdateUserProfileRequest = { name: 'New Name' };
      const mockResponse: AuthResponse = {
        success: true,
        user: { id: '1', email: 'test@example.com', name: 'New Name', isVerified: true, isActive: true }
      };

      let emittedUser: any = null;
      service.user$.subscribe(user => {
        emittedUser = user;
      });

      service.updateProfile(mockRequest).subscribe(res => {
        expect(res).toEqual(mockResponse);
      });

      const req = httpMock.expectOne(`${environment.apiUrl}/auth/profile`);
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual(mockRequest);
      req.flush(mockResponse);

      expect(emittedUser).toEqual(mockResponse.user);
      expect(localStorage.getItem(`portivio_user_v2`)).toContain('New Name');
    });
  });

  describe('changePassword', () => {
    it('should send POST request to change password', () => {
      const mockRequest: ChangePasswordRequest = {
        newPassword: 'new-password',
        confirmPassword: 'new-password'
      };
      const mockResponse: AuthResponse = { success: true, message: 'Password changed successfully' };

      service.changePassword(mockRequest).subscribe(res => {
        expect(res).toEqual(mockResponse);
      });

      const req = httpMock.expectOne(`${environment.apiUrl}/auth/change-password`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual(mockRequest);
      req.flush(mockResponse);
    });
  });
});
