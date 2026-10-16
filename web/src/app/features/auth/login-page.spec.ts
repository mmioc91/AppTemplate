import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { of, throwError } from 'rxjs';
import { markedFields, requiredFields } from '../../../testing/form-fields';
import { signedInUser } from '../../../testing/http';
import { ApiException } from '../../core/api/api-client';
import { AuthService } from '../../core/auth/auth.service';
import { LoginPage } from './login-page';

describe('LoginPage', () => {
  let signIn: ReturnType<typeof vi.fn>;
  let harness: RouterTestingHarness;
  const isSignedIn = signal(false);

  beforeEach(async () => {
    isSignedIn.set(false);
    signIn = vi.fn();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'login', component: LoginPage },
          { path: '**', children: [] },
        ]),
        { provide: AuthService, useValue: { signIn, isSignedIn } },
      ],
    });
    harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/login?returnUrl=%2Forders');
  });

  function fill(email: string, password: string): void {
    const host = harness.routeNativeElement as HTMLElement;
    for (const [name, value] of [
      ['email', email],
      ['password', password],
    ]) {
      const input = host.querySelector<HTMLInputElement>(`input[formControlName="${name}"]`)!;
      input.value = value;
      input.dispatchEvent(new Event('input'));
    }
  }

  async function submit(): Promise<HTMLElement> {
    const host = harness.routeNativeElement as HTMLElement;
    host.querySelector('form')!.dispatchEvent(new Event('submit'));
    await harness.fixture.whenStable();
    harness.detectChanges();
    return host;
  }

  it('does not send an empty form, and says what is missing', async () => {
    // Act
    const host = await submit();

    // Assert
    expect(signIn).not.toHaveBeenCalled();
    expect(host.querySelectorAll('mat-error').length).toBe(2);
    expect(host.textContent).toContain('This field is required.');
  });

  it('signs in and returns to where the user was going', async () => {
    // Arrange: a sign-in sets the session, as AuthService does.
    signIn.mockImplementation(() => {
      isSignedIn.set(true);
      return of(signedInUser);
    });
    fill('ana@example.com', 'correct horse battery');

    // Act
    await submit();
    await harness.fixture.whenStable();

    // Assert
    expect(signIn).toHaveBeenCalledWith('ana@example.com', 'correct horse battery');
    expect(TestBed.inject(Router).url).toBe('/orders');
  });

  it('goes on to where the user was going when the session is restored late', async () => {
    // Act: the start stopped waiting, the guard sent the user here, then the restore finished.
    isSignedIn.set(true);
    await harness.fixture.whenStable();

    // Assert
    expect(TestBed.inject(Router).url).toBe('/orders');
  });

  it('does not send a password of spaces only, as the API would refuse it', async () => {
    // Arrange
    fill('ana@example.com', '          ');

    // Act
    const host = await submit();

    // Assert
    expect(signIn).not.toHaveBeenCalled();
    expect(host.querySelector('mat-error')?.textContent).toContain('This field is required.');
  });

  it('shows one message for wrong credentials and stays on the page', async () => {
    // Arrange: what the generated client throws for a declared 401.
    signIn.mockReturnValue(throwError(() => ({ status: 401, code: 'AUTH_INVALID_CREDENTIALS' })));
    fill('ana@example.com', 'wrong horse battery');

    // Act
    const host = await submit();

    // Assert
    expect(host.querySelector('[role="alert"]')?.textContent).toContain('Wrong e-mail address or password.');
    expect(TestBed.inject(Router).url).toBe('/login?returnUrl=%2Forders');
  });

  it("puts the API's field code under its field", async () => {
    // Arrange
    signIn.mockReturnValue(
      throwError(() => ({ status: 400, code: 'VALIDATION_FAILED', errors: { email: ['INVALID'] } })),
    );
    fill('ana@', 'correct horse battery');

    // Act
    const host = await submit();

    // Assert
    expect(host.querySelector('mat-error')?.textContent).toContain("This value isn't valid.");
    expect(host.querySelector('[role="alert"]')).toBeNull();
  });

  it('adds no message of its own when the server could not be reached (the interceptor shows one)', async () => {
    // Arrange: what the generated client throws when nothing answered.
    signIn.mockReturnValue(throwError(() => new ApiException('Unexpected', 0, '', {}, null)));
    fill('ana@example.com', 'correct horse battery');

    // Act
    const host = await submit();

    // Assert
    expect(host.querySelector('[role="alert"]')).toBeNull();
  });

  it('marks both fields required for a screen reader, with no asterisk', async () => {
    // Arrange
    const loader = TestbedHarnessEnvironment.loader(harness.fixture);

    // Act & Assert
    expect(await requiredFields(loader)).toEqual([
      ['E-mail address', true],
      ['Password', true],
    ]);
    expect(await markedFields(loader)).toEqual([]);
  });
});
