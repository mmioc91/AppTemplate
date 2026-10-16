import { computed, DestroyRef, inject, Injectable, signal } from '@angular/core';
import {
  catchError,
  defer,
  finalize,
  firstValueFrom,
  map,
  Observable,
  of,
  shareReplay,
  Subject,
  switchMap,
  tap,
  throwError,
  timeout,
} from 'rxjs';
import { AuthClient, CurrentUserResponse, TokenPairResponse } from '../api/api-client';
import { problemOf } from '../http/problem';
import { REFRESH_TOKEN_KEY } from './storage-keys';
import { TokenStore } from './token-store';

// Tabs take turns refreshing: a refresh token is spent when used, and two tabs spending the same one at once would look
// like a stolen copy to the API, which then signs the user out everywhere (ADR 0031).
const REFRESH_LOCK = 'apptemplate.auth.refresh';

// A refresh that hangs holds the lock, and with it every other tab's refresh: past this it counts as no connection.
const REFRESH_TIMEOUT_MS = 30_000;

// How long the app's start waits for the stored session before it shows the first page: on a dead connection the
// refresh would otherwise hold a blank screen until it times out. It still finishes in the background.
export const RESTORE_WAIT_MS = 5_000;

// This tab's session ended in another tab — a sign-out there, or another account signed in there. Like a refused
// refresh token, it answers 401; unlike one, it leaves the stored token alone: it is the other tab's session now.
export class SessionEndedElsewhere extends Error {
  readonly status = 401;

  constructor() {
    super('The session ended in another tab.');
    this.name = 'SessionEndedElsewhere';
  }
}

// The signed-in account (ADR 0031, 0032): the access token in memory, the refresh token in storage, the current user as
// a signal. One refresh at a time — parallel 401s share it, and other tabs wait their turn (Web Locks). The session is
// forgotten when the API refuses the refresh token, never for a lost connection; and this tab's session ends when
// another tab signs out, or signs another account in — the stored token is shared, the account on screen is not.
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(AuthClient);
  private readonly store = inject(TokenStore);
  private readonly token = signal<string | null>(null);
  private readonly current = signal<CurrentUserResponse | null>(null);
  private readonly endings = new Subject<void>();
  private refreshing: Observable<string> | null = null;
  private restoring: Promise<void> | null = null;

  readonly user = this.current.asReadonly();
  readonly accessToken = this.token.asReadonly();
  readonly isSignedIn = computed(() => this.current() !== null);

  // This tab's session ended in another tab; the app goes to the sign-in page (app.config).
  readonly endedElsewhere: Observable<void> = this.endings.asObservable();

  constructor() {
    // Another tab removed the refresh token: it signed out, so this tab is signed out too — at once, not after its
    // access token runs out. A replaced token is usually that tab's rotation of the same session; the refresh checks it.
    const onStorage = (event: StorageEvent): void => {
      if (event.key === REFRESH_TOKEN_KEY && event.newValue === null && this.current() !== null) {
        this.endHere();
      }
    };

    if (typeof window !== 'undefined') {
      window.addEventListener('storage', onStorage);
      inject(DestroyRef).onDestroy(() => window.removeEventListener('storage', onStorage));
    }
  }

  // Whether a refresh could sign back in: a refresh token is stored.
  canRefresh(): boolean {
    return this.store.read() !== null;
  }

  // On start: back in with the stored refresh token, or stay signed out. Never fails; one at a time.
  restore(): Promise<void> {
    if (!this.canRefresh()) {
      return Promise.resolve();
    }

    this.restoring ??= firstValueFrom(this.refresh().pipe(switchMap(() => this.loadUser())))
      .then(() => undefined)
      .catch(() => this.token.set(null))
      .finally(() => (this.restoring = null));
    return this.restoring;
  }

  // restore(), but the caller waits at most that long for it.
  restoreWithin(milliseconds: number): Promise<void> {
    return Promise.race([this.restore(), new Promise<void>((resolve) => setTimeout(resolve, milliseconds))]);
  }

  signIn(email: string, password: string): Observable<CurrentUserResponse> {
    return this.api.login({ email, password }).pipe(switchMap((tokens) => this.start(tokens)));
  }

  register(email: string, password: string, displayName: string): Observable<CurrentUserResponse> {
    return this.api.register({ email, password, displayName }).pipe(switchMap((tokens) => this.start(tokens)));
  }

  // A new access token from the stored refresh token. The API spends the old one, so the new pair is stored inside the
  // lock, where the next tab reads it. A refused token (401) ends the session; any other failure keeps it for later.
  refresh(): Observable<string> {
    if (this.refreshing) {
      return this.refreshing;
    }

    this.refreshing = defer(() => this.lockedRefresh()).pipe(
      catchError((error: unknown) => {
        if (problemOf(error).status === 401 && !(error instanceof SessionEndedElsewhere)) {
          this.clear();
        }

        return throwError(() => error);
      }),
      finalize(() => (this.refreshing = null)),
      shareReplay(1),
    );
    return this.refreshing;
  }

  // Revokes the refresh token's family on the API, then forgets the session — also when the API can't be reached: the
  // access token dies on its own within minutes.
  signOut(): Observable<void> {
    const refreshToken = this.store.read();
    const revoke =
      refreshToken !== null && this.token() !== null
        ? this.api.logout({ refreshToken }).pipe(catchError(() => of(undefined)))
        : of(undefined);

    return revoke.pipe(map(() => this.clear()));
  }

  // Forget the session (the API refused the refresh token, or the user signed out).
  clear(): void {
    this.token.set(null);
    this.current.set(null);
    this.store.write(null);
  }

  private lockedRefresh(): Promise<string> {
    const run = async (): Promise<string> => {
      const refreshToken = this.store.read();

      if (refreshToken === null) {
        // Another tab signed out meanwhile: nothing to refresh, as if the API had refused it.
        throw Object.assign(new Error('No refresh token.'), { status: 401, code: 'AUTH_INVALID_REFRESH_TOKEN' });
      }

      const tokens = await firstValueFrom(this.api.refresh({ refreshToken }).pipe(timeout(REFRESH_TIMEOUT_MS)));

      // The stored token was spent: its replacement is stored whoever's it is, or the other tab's session breaks.
      this.store.write(tokens.refreshToken);
      const signedIn = this.current();

      // Another account signed in in another tab since this one loaded its user: this tab must not go on as them.
      if (signedIn !== null && subjectOf(tokens.accessToken) !== signedIn.id) {
        this.endHere();
        throw new SessionEndedElsewhere();
      }

      this.token.set(tokens.accessToken);
      return tokens.accessToken;
    };

    return typeof navigator !== 'undefined' && navigator.locks ? navigator.locks.request(REFRESH_LOCK, run) : run();
  }

  // This tab's session only; the stored token stays, it is another tab's.
  private endHere(): void {
    this.token.set(null);
    this.current.set(null);
    this.endings.next();
  }

  private start(tokens: TokenPairResponse): Observable<CurrentUserResponse> {
    this.token.set(tokens.accessToken);
    this.store.write(tokens.refreshToken);
    return this.loadUser();
  }

  private loadUser(): Observable<CurrentUserResponse> {
    return this.api.getCurrentUser().pipe(tap((user) => this.current.set(user)));
  }
}

// The user an access token was issued to: its `sub` claim. Read, not verified — the API verifies; this only tells
// which account a token belongs to.
function subjectOf(accessToken: string): string | null {
  try {
    const payload = accessToken.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
    const claims: unknown = JSON.parse(atob(payload));
    const subject = typeof claims === 'object' && claims !== null ? (claims as Record<string, unknown>)['sub'] : null;
    return typeof subject === 'string' ? subject : null;
  } catch {
    return null;
  }
}
