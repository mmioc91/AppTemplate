import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

// Same origin (ADR 0032): the API's paths start here, and nothing else ever gets the token — not another site whose
// address happens to contain /api/.
const API_PREFIX = '/api/';

// The calls that sign in: they carry no token, and their 401 means wrong credentials or a refused refresh token, not an
// expired access token.
const SIGN_IN_CALLS = ['/api/v1/auth/login', '/api/v1/auth/register', '/api/v1/auth/refresh'];

// The bearer on every API call while signed in; a 401 on anything but the sign-in calls refreshes once and retries
// (ADR 0032). Only a session the API refused — or one another tab ended — goes to the sign-in page; no connection or a
// busy server keeps it for the next try, and the error interceptor tells the user.
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (!request.url.startsWith(API_PREFIX)) {
    return next(request);
  }

  const token = auth.accessToken();
  const isSignIn = SIGN_IN_CALLS.some((call) => request.url.endsWith(call));
  const sent = token !== null && !isSignIn ? withBearer(request, token) : request;

  // The session is over: forget it, and sign in again to come back here.
  const endSession = (): void => {
    auth.clear();
    void router.navigate(['/login'], { queryParams: { returnUrl: router.url } });
  };

  return next(sent).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401 || isSignIn) {
        return throwError(() => error);
      }

      // A parallel call refreshed while this one was under way: its token is new already, no second rotation.
      const fresh = auth.accessToken();
      if (token !== null && fresh !== null && fresh !== token) {
        return next(withBearer(request, fresh));
      }

      // No refresh token any more: another tab signed out.
      if (!auth.canRefresh()) {
        endSession();
        return throwError(() => error);
      }

      return auth.refresh().pipe(
        catchError((refreshError: unknown) => {
          // A refused refresh token is forgotten by the refresh itself; a lost connection keeps it for the next try.
          if (!auth.canRefresh()) {
            endSession();
          }

          return throwError(() => refreshError);
        }),
        switchMap((refreshed) => next(withBearer(request, refreshed))),
      );
    }),
  );
};

function withBearer(request: HttpRequest<unknown>, token: string): HttpRequest<unknown> {
  return request.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
}
