import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

export interface ModalState<T = unknown> {
  isOpen: boolean;
  data: T | null;
}

@Injectable({
  providedIn: 'root'
})
export class ModalService {
  private readonly stateSubject = new BehaviorSubject<ModalState>({ isOpen: false, data: null });
  readonly state$ = this.stateSubject.asObservable();

  get state(): ModalState {
    return this.stateSubject.value;
  }

  open<T = unknown>(data?: T): void {
    this.stateSubject.next({ isOpen: true, data: data ?? null });
  }

  close(): void {
    this.stateSubject.next({ isOpen: false, data: null });
  }
}
