import { TestBed } from '@angular/core/testing';
import { ModalService } from './modal.service';

describe('ModalService', () => {
  let service: ModalService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(ModalService);
  });

  it('starts closed with null data', () => {
    expect(service.state.isOpen).toBeFalse();
    expect(service.state.data).toBeNull();
  });

  it('opens with injected data', () => {
    service.open({ mode: 'edit', id: 'tx-1' });

    expect(service.state.isOpen).toBeTrue();
    expect(service.state.data).toEqual({ mode: 'edit', id: 'tx-1' });
  });

  it('closes and clears data', () => {
    service.open({ source: 'dashboard' });

    service.close();

    expect(service.state.isOpen).toBeFalse();
    expect(service.state.data).toBeNull();
  });
});
