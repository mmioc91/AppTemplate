import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, provideRouter, Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { AuthService } from './auth.service';
import { authGuard, guestGuard, safeReturnUrl } from './guards';

describe('guards', () => {
  const route = {} as ActivatedRouteSnapshot;
  const state = { url: '/orders?page=2' } as RouterStateSnapshot;
  const signedIn = signal(false);

  beforeEach(() => {
    signedIn.set(false);
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: AuthService, useValue: { isSignedIn: signedIn } }],
    });
  });

  it('authGuard lets a signed-in user through', () => {
    // Arrange
    signedIn.set(true);

    // Act
    const result = TestBed.runInInjectionContext(() => authGuard(route, state));

    // Assert
    expect(result).toBe(true);
  });

  it('authGuard sends a signed-out user to sign in, remembering where they were going', () => {
    // Act
    const result = TestBed.runInInjectionContext(() => authGuard(route, state));

    // Assert
    expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe('/login?returnUrl=%2Forders%3Fpage%3D2');
  });

  it('guestGuard lets a signed-out user through', () => {
    // Act
    const result = TestBed.runInInjectionContext(() => guestGuard(route, state));

    // Assert
    expect(result).toBe(true);
  });

  it('guestGuard sends a signed-in user home', () => {
    // Arrange
    signedIn.set(true);

    // Act
    const result = TestBed.runInInjectionContext(() => guestGuard(route, state));

    // Assert
    expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe('/');
  });
});

describe('safeReturnUrl', () => {
  it.each([
    ['/orders?page=2', '/orders?page=2'],
    [null, '/'],
    ['', '/'],
    ['https://evil.example/login', '/'],
    ['//evil.example', '/'],
    ['/\\evil.example', '/'],
    ['orders', '/'],
  ])('follows %o to %o', (returnUrl, expected) => {
    // Act & Assert
    expect(safeReturnUrl(returnUrl)).toBe(expected);
  });
});
