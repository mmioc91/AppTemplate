import { AbstractControl, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';

// Checks the way the API makes them (ADR 0032): a form never refuses what the API accepts, and refuses what it would
// refuse with the same code. They report the keys fieldErrorCode maps to the API's codes — required → REQUIRED.

// No value, or white space only, is empty, as FluentValidation's NotEmpty sees it.
export const requiredText: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const value: unknown = control.value;

  return value === null || value === undefined || (typeof value === 'string' && value.trim() === '')
    ? { required: true }
    : null;
};

// A required text field's checks: requiredText refuses white space only, as the API does, and Validators.required beside
// it is what Angular Material reads to mark the field required for a screen reader (aria-required). Both report
// `required`, so the message is the same.
export const requiredTextChecks: readonly ValidatorFn[] = [Validators.required, requiredText];

// Blank → null: optional text the API treats as "not given".
export function textOrNull(value: string | null | undefined): string | null {
  return value && value.trim() !== '' ? value : null;
}
