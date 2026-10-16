import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthRules } from '../../core/auth/auth-rules';
import { AuthService } from '../../core/auth/auth.service';
import { applyServerErrors, fieldErrorCode } from '../../core/forms/field-errors';
import { requiredTextChecks } from '../../core/forms/validators';
import { errorMessage } from '../../core/http/error-messages';
import { problemOf, shownByInterceptor } from '../../core/http/problem';
import { provideFormFields } from '../../shared/ui/form-fields';

// Registration: a new account is signed in at once and lands home. The form checks what the API checks (AuthRules) —
// the address's shape is the API's to judge, and its refusal comes back as a code under the field.
@Component({
  selector: 'app-register-page',
  imports: [MatButtonModule, MatCardModule, MatFormFieldModule, MatInputModule, ReactiveFormsModule, RouterLink],
  providers: [provideFormFields()],
  templateUrl: './register-page.html',
  styleUrl: './auth-page.scss',
})
export class RegisterPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly rules = AuthRules;

  protected readonly form = new FormGroup({
    email: new FormControl('', {
      nonNullable: true,
      validators: [...requiredTextChecks, Validators.maxLength(AuthRules.emailMaxLength)],
    }),
    password: new FormControl('', {
      nonNullable: true,
      validators: [
        Validators.required,
        Validators.minLength(AuthRules.passwordMinLength),
        Validators.maxLength(AuthRules.passwordMaxLength),
      ],
    }),
    displayName: new FormControl('', {
      nonNullable: true,
      validators: [...requiredTextChecks, Validators.maxLength(AuthRules.displayNameMaxLength)],
    }),
  });

  protected readonly formError = signal<string | null>(null);
  protected readonly submitting = signal(false);
  protected readonly fieldErrorCode = fieldErrorCode;
  protected readonly errorMessage = errorMessage;

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { email, password, displayName } = this.form.getRawValue();
    this.submitting.set(true);
    this.formError.set(null);

    this.auth
      .register(email, password, displayName)
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: () => void this.router.navigateByUrl('/'),
        error: (error: unknown) => {
          const problem = problemOf(error);

          if (!shownByInterceptor(problem) && !applyServerErrors(this.form, problem)) {
            this.formError.set(errorMessage(problem.code));
          }
        },
      });
  }
}
