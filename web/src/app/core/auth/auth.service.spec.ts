import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { flushProblem, jsonBlob, signedInUser, tokenPair } from '../../../testing/http';
import { API_BASE_URL } from '../api/api-client';
import { AuthService } from './auth.service';
import { REFRESH_TOKEN_KEY } from './storage-keys';

describe('AuthService', () => {
  let auth: AuthService;
  let backend: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), { provide: API_BASE_URL, useValue: '' }],
    });
    auth = TestBed.inject(AuthService);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  // The current user loaded after any sign-in.
  async function answerMe(): Promise<void> {
    await vi.waitFor(() => backend.expectOne('/api/v1/auth/me').flush(jsonBlob(signedInUser)));
  }

  it('signs in: the access token in memory, the refresh token in storage, the user loaded', async () => {
    // Act
    const signedIn = firstValueFrom(auth.signIn('ana@example.com', 'correct horse battery'));
    const login = backend.expectOne('/api/v1/auth/login');
    login.flush(jsonBlob(tokenPair('access-1', 'refresh-1')));
    await answerMe();
    await signedIn;

    // Assert
    expect(login.request.body).toBe('{"email":"ana@example.com","password":"correct horse battery"}');
    expect(auth.accessToken()).toBe('access-1');
    expect(localStorage.getItem(REFRESH_TOKEN_KEY)).toBe('refresh-1');
    expect(auth.user()).toEqual(signedInUser);
    expect(auth.isSignedIn()).toBe(true);
  });

  it('refreshes once for callers that ask together, and stores the new pair', async () => {
    // Arrange
    localStorage.setItem(REFRESH_TOKEN_KEY, 'refresh-1');

    // Act
    const first = firstValueFrom(auth.refresh());
    const second = firstValueFrom(auth.refresh());
    const refresh = await vi.waitFor(() => backend.expectOne('/api/v1/auth/refresh'));
    refresh.flush(jsonBlob(tokenPair('access-2', 'refresh-2')));

    // Assert
    expect(await first).toBe('access-2');
    expect(await second).toBe('access-2');
    expect(refresh.request.body).toBe('{"refreshToken":"refresh-1"}');
    expect(localStorage.getItem(REFRESH_TOKEN_KEY)).toBe('refresh-2');
  });

  it('forgets the session when the API refuses the refresh token', async () => {
    // Arrange
    localStorage.setItem(REFRESH_TOKEN_KEY, 'refresh-1');

    // Act
    const refreshed = firstValueFrom(auth.refresh());
    flushProblem(await vi.waitFor(() => backend.expectOne('/api/v1/auth/refresh')), 401, 'AUTH_INVALID_REFRESH_TOKEN');

    // Assert
    await expect(refreshed).rejects.toBeTruthy();
    expect(auth.canRefresh()).toBe(false);
    expect(auth.isSignedIn()).toBe(false);
  });

  it('keeps the refresh token when the API can not be reached', async () => {
    // Arrange
    localStorage.setItem(REFRESH_TOKEN_KEY, 'refresh-1');

    // Act
    const refreshed = firstValueFrom(auth.refresh());
    (await vi.waitFor(() => backend.expectOne('/api/v1/auth/refresh'))).error(new ProgressEvent('error'), {
      status: 0,
    });

    // Assert
    await expect(refreshed).rejects.toBeTruthy();
    expect(localStorage.getItem(REFRESH_TOKEN_KEY)).toBe('refresh-1');
  });

  it('restores nothing, and asks the API nothing, without a stored refresh token', async () => {
    // Act
    await auth.restore();

    // Assert: afterEach verifies that no request went out.
    expect(auth.isSignedIn()).toBe(false);
  });

  it('restores a stored session: a refresh, then the current user', async () => {
    // Arrange
    localStorage.setItem(REFRESH_TOKEN_KEY, 'refresh-1');

    // Act
    const restored = auth.restore();
    (await vi.waitFor(() => backend.expectOne('/api/v1/auth/refresh'))).flush(
      jsonBlob(tokenPair('access-2', 'refresh-2')),
    );
    await answerMe();
    await restored;

    // Assert
    expect(auth.user()).toEqual(signedInUser);
    expect(auth.accessToken()).toBe('access-2');
  });

  it('signs out: revokes the refresh token on the API, then forgets the session', async () => {
    // Arrange
    const signedIn = firstValueFrom(auth.signIn('ana@example.com', 'correct horse battery'));
    backend.expectOne('/api/v1/auth/login').flush(jsonBlob(tokenPair('access-1', 'refresh-1')));
    await answerMe();
    await signedIn;

    // Act
    const signedOut = firstValueFrom(auth.signOut(), { defaultValue: undefined });
    const logout = backend.expectOne('/api/v1/auth/logout');
    logout.flush(null, { status: 204, statusText: 'No Content' });
    await signedOut;

    // Assert
    expect(logout.request.body).toBe('{"refreshToken":"refresh-1"}');
    expect(auth.isSignedIn()).toBe(false);
    expect(localStorage.getItem(REFRESH_TOKEN_KEY)).toBeNull();
  });

  it('signs out locally also when the API fails', async () => {
    // Arrange
    const signedIn = firstValueFrom(auth.signIn('ana@example.com', 'correct horse battery'));
    backend.expectOne('/api/v1/auth/login').flush(jsonBlob(tokenPair('access-1', 'refresh-1')));
    await answerMe();
    await signedIn;

    // Act
    const signedOut = firstValueFrom(auth.signOut(), { defaultValue: undefined });
    flushProblem(backend.expectOne('/api/v1/auth/logout'), 500, 'UNEXPECTED_ERROR');
    await signedOut;

    // Assert
    expect(auth.isSignedIn()).toBe(false);
    expect(localStorage.getItem(REFRESH_TOKEN_KEY)).toBeNull();
  });
});
