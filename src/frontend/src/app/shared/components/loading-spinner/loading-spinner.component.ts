import { Component, Input } from '@angular/core';

/**
 * Minimal inline spinner. Use with `[loading]` input to gate visibility.
 * Intentionally dependency-free so any feature module can embed it without
 * pulling in Bootstrap JS or third-party icons.
 */
@Component({
  selector: 'app-loading-spinner',
  templateUrl: './loading-spinner.component.html',
  styleUrls: ['./loading-spinner.component.scss']
})
export class LoadingSpinnerComponent {
  /** When false, the spinner is not rendered at all. */
  @Input() loading = true;

  /** Optional text shown next to the spinner for screen readers / visible label. */
  @Input() label = 'Loading';

  /** 'sm' | 'md' | 'lg' — controls diameter via CSS custom property. */
  @Input() size: 'sm' | 'md' | 'lg' = 'md';

  /** Render without the text label (spinner only). */
  @Input() iconOnly = false;
}
