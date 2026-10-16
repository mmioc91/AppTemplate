import { Component, effect, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthRules } from '../../core/auth/auth-rules';
import { AuthService } from '../../core/auth/auth.service';
import { safeReturnUrl } from '../../core/auth/guards';
import { applyServerErrors, fieldErrorCode } from '../../core/forms/field-errors';
import { requiredTextChecks } from '../../core/forms/validators';
import { errorMessage } from '../../core/http/error-messages';
import { problemOf, shownByInterceptor } from '../../core/http/problem';
import { provideFormFields } from '../../shared/ui/form-fields';

// Sign-in: on success back to where the user was going, or home. A refusal is one message for the whole form — the API
// doesn't say whether the e-mail address or the password was wrong (ADR 0031).
@Component({
  selector: 'app-login-page',
  imports: [MatButtonModule, MatCardModule, MatFormFieldModule, MatInputModule, ReactiveFormsModule, RouterLink],
  providers: [provideFormFields()],
  templateUrl: './login-page.html',
  styleUrl: './auth-page.scss',
})
export class LoginPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  // Emptiness and length only, as the API checks them: it answers a malformed address with its own field code, and a
  // wrong one like a wrong password, so a stricter check here could only lock out an account the API accepts.
  protected readonly form = new FormGroup({
    email: new FormControl('', {
      nonNullable: true,
      validators: [...requiredTextChecks, Validators.maxLength(AuthRules.emailMaxLength)],
    }),
    password: new FormControl('', {
      nonNullable: true,
      validators: [...requiredTextChecks, Validators.maxLength(AuthRules.passwordMaxLength)],
    }),
  });

  protected readonly formError = signal<string | null>(null);
  protected readonly submitting = signal(false);
  protected readonly fieldErrorCode = fieldErrorCode;
  protected readonly errorMessage = errorMessage;

  constructor() {
    // The session restored late — after the start stopped waiting and the guard sent the user here: go on.
    effect(() => {
      if (this.auth.isSignedIn()) {
        void this.router.navigateByUrl(safeReturnUrl(this.route.snapshot.queryParamMap.get('returnUrl')));
      }
    });
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { email, password } = this.form.getRawValue();
    this.submitting.set(true);
    this.formError.set(null);

    this.auth
      .signIn(email, password)
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        // Signed in: the effect above goes on to where the user was going.
        next: () => undefined,
        error: (error: unknown) => {
          const problem = problemOf(error);

          // 5xx and offline are told by the error interceptor; the rest belongs to this form.
          if (!shownByInterceptor(problem) && !applyServerErrors(this.form, problem)) {
            this.formError.set(errorMessage(problem.code));
          }
        },
      });
  }
}
