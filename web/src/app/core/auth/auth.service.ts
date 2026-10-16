import { computed, inject, Injectable, signal } from '@angular/core';
import {
  catchError,
  defer,
  finalize,
  firstValueFrom,
  map,
  Observable,
  of,
  shareReplay,
  switchMap,
  tap,
  throwError,
} from 'rxjs';
import { AuthClient, CurrentUserResponse, TokenPairResponse } from '../api/api-client';
import { problemOf } from '../http/problem';
import { TokenStore } from './token-store';

// Tabs take turns refreshing: a refresh token is spent when used, and two tabs spending the same one at once would look
// like a stolen copy to the API, which then signs the user out everywhere (ADR 0031).
const REFRESH_LOCK = 'apptemplate.auth.refresh';

// How long the app's start waits for the stored session before it shows the first page: on a dead connection the
// refresh would otherwise hold a blank screen until it times out. It still finishes in the background.
export const RESTORE_WAIT_MS = 5_000;

// The signed-in account (ADR 0031, 0032): the access token in memory, the refresh token in storage, the current user as
// a signal. One refresh at a time — parallel 401s share it, and other tabs wait their turn (Web Locks). The session is
// forgotten only when the API refuses the refresh token, never for a lost connection.
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(AuthClient);
  private readonly store = inject(TokenStore);
  private readonly token = signal<string | null>(null);
  private readonly current = signal<CurrentUserResponse | null>(null);
  private refreshing: Observable<string> | null = null;
  private restoring: Promise<void> | null = null;

  readonly user = this.current.asReadonly();
  readonly accessToken = this.token.asReadonly();
  readonly isSignedIn = computed(() => this.current() !== null);

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
      map((tokens) => tokens.accessToken),
      catchError((error: unknown) => {
        if (problemOf(error).status === 401) {
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

    return revoke.pipe(
      map(() => undefined),
      finalize(() => this.clear()),
    );
  }

  // Forget the session (the API refused the refresh token, or the user signed out).
  clear(): void {
    this.token.set(null);
    this.current.set(null);
    this.store.write(null);
  }

  private lockedRefresh(): Promise<TokenPairResponse> {
    const run = async (): Promise<TokenPairResponse> => {
      const refreshToken = this.store.read();

      if (refreshToken === null) {
        // Another tab signed out meanwhile: nothing to refresh, as if the API had refused it.
        throw Object.assign(new Error('No refresh token.'), { status: 401, code: 'AUTH_INVALID_REFRESH_TOKEN' });
      }

      const tokens = await firstValueFrom(this.api.refresh({ refreshToken }));
      this.keep(tokens);
      return tokens;
    };

    return typeof navigator !== 'undefined' && navigator.locks ? navigator.locks.request(REFRESH_LOCK, run) : run();
  }

  private start(tokens: TokenPairResponse): Observable<CurrentUserResponse> {
    this.keep(tokens);
    return this.loadUser();
  }

  private loadUser(): Observable<CurrentUserResponse> {
    return this.api.getCurrentUser().pipe(tap((user) => this.current.set(user)));
  }

  private keep(tokens: TokenPairResponse): void {
    this.token.set(tokens.accessToken);
    this.store.write(tokens.refreshToken);
  }
}
