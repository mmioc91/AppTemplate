import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { of, throwError } from 'rxjs';
import { markedFields, requiredFields } from '../../../testing/form-fields';
import { signedInUser } from '../../../testing/http';
import { AuthRules } from '../../core/auth/auth-rules';
import { AuthService } from '../../core/auth/auth.service';
import { RegisterPage } from './register-page';

describe('RegisterPage', () => {
  let register: ReturnType<typeof vi.fn>;
  let harness: RouterTestingHarness;

  beforeEach(async () => {
    register = vi.fn();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'register', component: RegisterPage },
          { path: '**', children: [] },
        ]),
        { provide: AuthService, useValue: { register } },
      ],
    });
    harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/register');
  });

  function fill(email: string, password: string, displayName: string): void {
    const host = harness.routeNativeElement as HTMLElement;
    for (const [name, value] of [
      ['email', email],
      ['password', password],
      ['displayName', displayName],
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

  it('registers and lands home', async () => {
    // Arrange
    register.mockReturnValue(of(signedInUser));
    fill('ana@example.com', 'correct horse battery', 'Ana');

    // Act
    await submit();

    // Assert
    expect(register).toHaveBeenCalledWith('ana@example.com', 'correct horse battery', 'Ana');
    expect(TestBed.inject(Router).url).toBe('/');
  });

  it('refuses a password shorter than the API allows, before sending anything', async () => {
    // Arrange
    fill('ana@example.com', 'p'.repeat(AuthRules.passwordMinLength - 1), 'Ana');

    // Act
    const host = await submit();

    // Assert
    expect(register).not.toHaveBeenCalled();
    expect(host.querySelector('mat-error')?.textContent).toContain('This value is too short.');
  });

  it('refuses a name of spaces only, as the API does', async () => {
    // Arrange
    fill('ana@example.com', 'correct horse battery', '   ');

    // Act
    const host = await submit();

    // Assert
    expect(register).not.toHaveBeenCalled();
    expect(host.querySelector('mat-error')?.textContent).toContain('This field is required.');
  });

  it('lets an address through for the API to judge', async () => {
    // Arrange: an apostrophe and an international domain, both valid; Angular's e-mail check is not the API's.
    register.mockReturnValue(of(signedInUser));
    fill("o'brien@žuti.hr", 'correct horse battery', 'Seán');

    // Act
    await submit();

    // Assert
    expect(register).toHaveBeenCalledWith("o'brien@žuti.hr", 'correct horse battery', 'Seán');
  });

  it('says an address is taken, and stays on the page', async () => {
    // Arrange
    register.mockReturnValue(throwError(() => ({ status: 409, code: 'AUTH_EMAIL_TAKEN' })));
    fill('ana@example.com', 'correct horse battery', 'Ana');

    // Act
    const host = await submit();

    // Assert
    expect(host.querySelector('[role="alert"]')?.textContent).toContain(
      'An account with this e-mail address already exists.',
    );
    expect(TestBed.inject(Router).url).toBe('/register');
  });

  it('marks every field required for a screen reader, with no asterisk', async () => {
    // Arrange
    const loader = TestbedHarnessEnvironment.loader(harness.fixture);

    // Act & Assert
    expect(await requiredFields(loader)).toEqual([
      ['E-mail address', true],
      ['Password', true],
      ['Name', true],
    ]);
    expect(await markedFields(loader)).toEqual([]);
  });
});
