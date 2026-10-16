import { Provider } from '@angular/core';
import { MAT_FORM_FIELD_DEFAULT_OPTIONS, MatFormFieldDefaultOptions } from '@angular/material/form-field';

// How every form's fields look: no asterisk on a required field — an optional field says "(optional)" in its label,
// so the rest are required — while a screen reader still hears "required" (aria-required, from Validators.required or
// requiredTextChecks). Provide it in the component that holds the form (a page or a dialog), never in app.config: the
// token's file would bring Material's form field into the start-up bundle.
export function provideFormFields(options: MatFormFieldDefaultOptions = {}): Provider {
  return {
    provide: MAT_FORM_FIELD_DEFAULT_OPTIONS,
    useValue: { ...options, hideRequiredMarker: true } satisfies MatFormFieldDefaultOptions,
  };
}
