import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject, Injector } from '@angular/core';
import { catchError, throwError } from 'rxjs';

// The snackbar — and the Material overlay under it — loads on its own instead of with the start-up: nothing at start-up
// shows one, and it would be most of the start-up bundle's Material.
const snackBarModule = () => import('@angular/material/snack-bar');

// Fetches the snackbar right after start-up, in the background, so the "can't be reached" message never needs a
// download while the connection is down.
export function preloadSnackBar(): void {
  void snackBarModule();
}

// Failures no screen can do anything about — the server failed (5xx) or never answered (status 0) — get one message
// here, with the trace id for support (ADR 0028). Every 4xx is the screen's to explain, by its code (ADR 0032).
export const errorInterceptor: HttpInterceptorFn = (request, next) => {
  const injector = inject(Injector);

  return next(request).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && (error.status === 0 || error.status >= 500)) {
        void show(error, injector);
      }

      return throwError(() => error);
    }),
  );
};

async function show(error: HttpErrorResponse, injector: Injector): Promise<void> {
  const [message, { MatSnackBar }] = await Promise.all([messageFor(error), snackBarModule()]);
  injector.get(MatSnackBar).open(message, $localize`:@@error.dismiss:Close`, { duration: 10000 });
}

async function messageFor(error: HttpErrorResponse): Promise<string> {
  if (error.status === 0) {
    return $localize`:@@error.offline:The server can't be reached. Check your connection and try again.`;
  }

  const traceId = await traceIdOf(error.error);

  return traceId
    ? $localize`:@@error.server:Something went wrong on our side. Reference: ${traceId}:traceId:`
    : $localize`:@@error.serverNoReference:Something went wrong on our side.`;
}

// The generated client asks for blobs, so the ProblemDetails body arrives as one.
async function traceIdOf(body: unknown): Promise<string | null> {
  try {
    const parsed: unknown = body instanceof Blob ? JSON.parse(await body.text()) : body;
    const value = typeof parsed === 'object' && parsed !== null ? (parsed as Record<string, unknown>)['traceId'] : null;
    return typeof value === 'string' ? value : null;
  } catch {
    return null;
  }
}
