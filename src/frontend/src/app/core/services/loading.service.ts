import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';

/**
 * Global loading signal for long-running actions. Reference-counted so that
 * concurrent `show()` calls don't prematurely hide the spinner when one
 * finishes before another.
 */
@Injectable({ providedIn: 'root' })
export class LoadingService {
  private activeCount = 0;
  private loadingSubject = new BehaviorSubject<boolean>(false);

  public readonly loading$: Observable<boolean> = this.loadingSubject.asObservable();

  show(): void {
    this.activeCount++;
    if (this.activeCount === 1) {
      this.loadingSubject.next(true);
    }
  }

  hide(): void {
    this.activeCount = Math.max(0, this.activeCount - 1);
    if (this.activeCount === 0) {
      this.loadingSubject.next(false);
    }
  }

  /** Force-reset the counter — useful in error recovery paths. */
  reset(): void {
    this.activeCount = 0;
    this.loadingSubject.next(false);
  }

  get isLoading(): boolean {
    return this.loadingSubject.value;
  }
}
