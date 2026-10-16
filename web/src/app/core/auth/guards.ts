import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

// Signed-in screens: without a session, to the sign-in page, which brings the user back here afterwards.
export const authGuard: CanActivateFn = (_route, state) =>
  inject(AuthService).isSignedIn() ||
  inject(Router).createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });

// Sign-in and registration: someone already signed in goes home instead.
export const guestGuard: CanActivateFn = () => !inject(AuthService).isSignedIn() || inject(Router).createUrlTree(['/']);

// Where to go after signing in: the page the user asked for, if it is one of this app's — never another site, which a
// crafted link could otherwise send a freshly signed-in user to.
export function safeReturnUrl(returnUrl: string | null | undefined): string {
  return returnUrl && returnUrl.startsWith('/') && !returnUrl.startsWith('//') && !returnUrl.startsWith('/\\')
    ? returnUrl
    : '/';
}
