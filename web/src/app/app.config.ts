import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
  provideEnvironmentInitializer,
} from '@angular/core';
import { provideRouter, Router, withComponentInputBinding } from '@angular/router';
import { routes } from './app.routes';
import { API_BASE_URL } from './core/api/api-client';
import { authInterceptor } from './core/auth/auth-interceptor';
import { AuthService, RESTORE_WAIT_MS } from './core/auth/auth.service';
import { errorInterceptor, preloadSnackBar } from './core/http/error-interceptor';

// The composition root (the frontend's Program.cs): everything app-wide is a provider here.
export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    // Route and query parameters arrive as the screen's inputs (`/orders/:id` → `id`, `?page=` → `page`).
    provideRouter(routes, withComponentInputBinding()),
    // The error interceptor sits outside the auth one, so a 401 that a refresh fixes never reaches it.
    provideHttpClient(withFetch(), withInterceptors([errorInterceptor, authInterceptor])),
    // Not awaited: the error interceptor's snackbar downloads alongside the first screen.
    provideEnvironmentInitializer(preloadSnackBar),
    // Same origin (ADR 0029, 0032): the generated client's paths already start with /api/v1 — served next to the app
    // in production, proxied by the dev server locally (proxy.conf.json).
    { provide: API_BASE_URL, useValue: '' },
    // Back into the stored session before the first route decides who may see it — waiting a few seconds at most.
    provideAppInitializer(() => inject(AuthService).restoreWithin(RESTORE_WAIT_MS)),
    // This tab's session ended in another tab (a sign-out, another account): sign in again to come back here.
    provideAppInitializer(() => {
      const router = inject(Router);
      inject(AuthService).endedElsewhere.subscribe(
        () => void router.navigate(['/login'], { queryParams: { returnUrl: router.url } }),
      );
    }),
  ],
};
