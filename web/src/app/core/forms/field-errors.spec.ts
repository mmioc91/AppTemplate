import { FormControl, FormGroup, Validators } from '@angular/forms';
import { applyServerErrors, fieldErrorCode } from './field-errors';

describe('fieldErrorCode', () => {
  it.each([
    [{ server: 'AUTH_EMAIL_TAKEN' }, 'AUTH_EMAIL_TAKEN'],
    [{ required: true }, 'REQUIRED'],
    [{ minlength: { requiredLength: 10, actualLength: 3 } }, 'TOO_SHORT'],
    [{ maxlength: { requiredLength: 80, actualLength: 81 } }, 'TOO_LONG'],
    [{ max: { max: 100, actual: 101 } }, 'TOO_LARGE'],
    [{ email: true }, 'INVALID'],
  ])('maps %o to the API code %s', (errors, code) => {
    // Arrange
    const control = new FormControl('');
    control.setErrors(errors);

    // Act & Assert
    expect(fieldErrorCode(control)).toBe(code);
  });

  it('is null for a valid control', () => {
    // Act & Assert
    expect(fieldErrorCode(new FormControl('ok'))).toBeNull();
  });
});

describe('applyServerErrors', () => {
  it('puts each field code under its control, touched so it shows', () => {
    // Arrange
    const form = new FormGroup({ email: new FormControl('ana@x'), displayName: new FormControl('Ana') });

    // Act
    const applied = applyServerErrors(form, {
      status: 400,
      code: 'VALIDATION_FAILED',
      errors: { email: ['INVALID'] },
      traceId: null,
    });

    // Assert
    expect(applied).toBe(true);
    expect(form.controls.email.errors).toEqual({ server: 'INVALID' });
    expect(form.controls.email.touched).toBe(true);
    expect(form.controls.displayName.valid).toBe(true);
  });

  it('reports false when no field error matches a control, so the screen shows the code itself', () => {
    // Arrange
    const form = new FormGroup({ email: new FormControl('', Validators.required) });

    // Act
    const applied = applyServerErrors(form, { status: 409, code: 'AUTH_EMAIL_TAKEN', errors: {}, traceId: null });

    // Assert
    expect(applied).toBe(false);
  });
});
