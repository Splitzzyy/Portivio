import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { DashboardComponent } from './dashboard.component';
import { AuthService } from '../../../../core/services/auth.service';
import { HomeService } from '../../../../core/services/home.service';
import { ModalService } from '../../../../core/services/modal.service';

describe('DashboardComponent', () => {
  let component: DashboardComponent;
  let fixture: ComponentFixture<DashboardComponent>;
  let authSvc: jasmine.SpyObj<AuthService>;
  let homeSvc: jasmine.SpyObj<HomeService>;
  let modalSvc: jasmine.SpyObj<ModalService>;
  let router: Router;

  beforeEach(async () => {
    authSvc = jasmine.createSpyObj('AuthService', ['getCurrentUser']);
    homeSvc = jasmine.createSpyObj('HomeService', ['getHome']);
    modalSvc = jasmine.createSpyObj('ModalService', ['open', 'close']);

    authSvc.getCurrentUser.and.returnValue(null);
    homeSvc.getHome.and.returnValue(of({
      user: {
        id: 'u1',
        email: 'user@example.com',
        name: 'User Example',
        isVerified: true,
        isActive: true,
        createdAt: '2026-01-01T00:00:00Z',
        lastLoginAt: null
      },
      summary: { totalInvestment: 0, totalMarketValue: 0, totalUnrealizedPnL: 0, profileCount: 0, holdingCount: 0, activeSIPCount: 0, transactionCount: 0 },
      profiles: []
    }));

    await TestBed.configureTestingModule({
      declarations: [DashboardComponent],
      imports: [CommonModule, RouterTestingModule.withRoutes([], { initialNavigation: 'disabled' })],
      providers: [
        { provide: AuthService, useValue: authSvc },
        { provide: HomeService, useValue: homeSvc },
        { provide: ModalService, useValue: modalSvc }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(DashboardComponent);
    component = fixture.componentInstance;

    router = TestBed.inject(Router);
    router.errorHandler = () => null;
    spyOn(router, 'navigate').and.resolveTo(true);

    fixture.detectChanges();
  });

  afterEach(() => {
    router?.dispose();
    fixture?.destroy();
  });

  it('openAddInvestmentModal calls modal service with source', () => {
    component.openAddInvestmentModal('dashboard-header');

    expect(modalSvc.open).toHaveBeenCalledWith({ source: 'dashboard-header' });
    expect(router.navigate).toHaveBeenCalledWith(['/dashboard/add-investment'], {
      state: { data: { source: 'dashboard-header' } }
    });
  });
});
