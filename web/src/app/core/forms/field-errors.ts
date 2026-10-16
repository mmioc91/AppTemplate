import { AbstractControl, FormGroup } from '@angular/forms';
import { Problem } from '../http/problem';

// The error code to show under a field: the API's code when the server refused the value, otherwise the code that
// matches the client-side check that failed — the same codes the API uses (ADR 0012), so one message set serves both.
export function fieldErrorCode(control: AbstractControl): string | null {
  const errors = control.errors;

  if (!errors) {
    return null;
  }

  if (typeof errors['server'] === 'string') {
    return errors['server'];
  }

  if (errors['required']) {
    return 'REQUIRED';
  }

  if (errors['minlength']) {
    return 'TOO_SHORT';
  }

  if (errors['maxlength']) {
    return 'TOO_LONG';
  }

  if (errors['max']) {
    return 'TOO_LARGE';
  }

  return 'INVALID';
}

// Puts the API's field errors (a 400's `errors`, keys in the request's JSON naming) on the form's controls; returns
// whether any matched a control, so the caller can show the rest as a form-level message.
export function applyServerErrors(form: FormGroup, problem: Problem): boolean {
  let applied = false;

  for (const [name, codes] of Object.entries(problem.errors)) {
    const control = form.get(name);

    if (control && codes.length > 0) {
      control.setErrors({ server: codes[0] });
      control.markAsTouched();
      applied = true;
    }
  }

  return applied;
}
