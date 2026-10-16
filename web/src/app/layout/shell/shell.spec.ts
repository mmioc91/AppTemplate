import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { of } from 'rxjs';
import { signedInUser } from '../../../testing/http';
import { CurrentUserResponse } from '../../core/api/api-client';
import { AuthService } from '../../core/auth/auth.service';
import { Shell } from './shell';

describe('Shell', () => {
  let signOut: ReturnType<typeof vi.fn>;
  let harness: RouterTestingHarness;

  beforeEach(async () => {
    signOut = vi.fn(() => of(undefined));
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'home', component: Shell },
          { path: '**', children: [] },
        ]),
        { provide: AuthService, useValue: { user: signal<CurrentUserResponse | null>(signedInUser), signOut } },
      ],
    });
    harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/home');
  });

  it('shows who is signed in', () => {
    // Act
    const host = harness.routeNativeElement as HTMLElement;

    // Assert
    expect(host.querySelector('.user')?.textContent).toBe('Ana');
  });

  it('signs out and goes to the sign-in page', async () => {
    // Arrange
    const button = Array.from((harness.routeNativeElement as HTMLElement).querySelectorAll('button')).find(
      (candidate) => candidate.textContent?.trim() === 'Sign out',
    )!;

    // Act
    button.click();
    await harness.fixture.whenStable();

    // Assert
    expect(signOut).toHaveBeenCalled();
    expect(TestBed.inject(Router).url).toBe('/login');
  });
});
