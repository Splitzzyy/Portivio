import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

const EMAIL_PATTERN = /^[a-z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-z0-9-]+(?:\.[a-z0-9-]+)+$/i;

export function normalizeEmailValue(value: string | null | undefined): string {
  return (value ?? '').trim().toLowerCase();
}

export function normalizeEmailControl(control: AbstractControl | null): void {
  if (!control) {
    return;
  }

  const normalizedValue = normalizeEmailValue(control.value as string | null | undefined);
  if (normalizedValue !== control.value) {
    control.setValue(normalizedValue);
  }
}

export function emailFormatValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const normalizedValue = normalizeEmailValue(control.value as string | null | undefined);
    if (!normalizedValue) {
      return null;
    }

    if (!EMAIL_PATTERN.test(normalizedValue) || normalizedValue.includes('..')) {
      return { emailFormat: true };
    }

    const [localPart, domainPart] = normalizedValue.split('@');
    if (!localPart || !domainPart) {
      return { emailFormat: true };
    }

    if (localPart.startsWith('.') || localPart.endsWith('.')) {
      return { emailFormat: true };
    }

    const domainLabels = domainPart.split('.');
    const hasInvalidDomainLabel = domainLabels.some(label =>
      !label || label.startsWith('-') || label.endsWith('-')
    );

    return hasInvalidDomainLabel ? { emailFormat: true } : null;
  };
}
