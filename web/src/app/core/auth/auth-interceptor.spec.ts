import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { firstValueFrom, of, throwError } from 'rxjs';
import { authInterceptor } from './auth-interceptor';
import { AuthService } from './auth.service';

describe('authInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  let auth: {
    accessToken: ReturnType<typeof signal<string | null>>;
    canRefresh: ReturnType<typeof vi.fn>;
    refresh: ReturnType<typeof vi.fn>;
    clear: ReturnType<typeof vi.fn>;
  };

  beforeEach(async () => {
    auth = {
      accessToken: signal<string | null>('access-1'),
      canRefresh: vi.fn(() => true),
      refresh: vi.fn(),
      clear: vi.fn(),
    };
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([{ path: '**', children: [] }]),
        { provide: AuthService, useValue: auth },
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
    await TestBed.inject(Router).navigateByUrl('/orders');
  });

  afterEach(() => backend.verify());

  it('sends the access token with every API call', () => {
    // Act
    http.get('/api/v1/orders').subscribe();

    // Assert
    expect(backend.expectOne('/api/v1/orders').request.headers.get('Authorization')).toBe('Bearer access-1');
  });

  it('sends no token with the sign-in calls, nor anywhere outside the API', () => {
    // Act
    http.post('/api/v1/auth/login', {}).subscribe();
    http.get('/assets/logo.svg').subscribe();

    // Assert
    expect(backend.expectOne('/api/v1/auth/login').request.headers.has('Authorization')).toBe(false);
    expect(backend.expectOne('/assets/logo.svg').request.headers.has('Authorization')).toBe(false);
  });

  it('never sends the token to another site, whatever its path', () => {
    // Act
    http.get('https://maps.example.com/api/geocode').subscribe();

    // Assert
    expect(backend.expectOne('https://maps.example.com/api/geocode').request.headers.has('Authorization')).toBe(false);
  });

  it('repeats a call with the token a parallel refresh already brought, without refreshing again', async () => {
    // Act: the call went out with access-1; meanwhile another call's refresh brought access-2.
    const answer = firstValueFrom(http.get('/api/v1/orders'));
    auth.accessToken.set('access-2');
    backend.expectOne('/api/v1/orders').flush(null, { status: 401, statusText: 'Unauthorized' });
    const retried = backend.expectOne('/api/v1/orders');
    retried.flush({ items: [] });

    // Assert
    expect(retried.request.headers.get('Authorization')).toBe('Bearer access-2');
    expect(auth.refresh).not.toHaveBeenCalled();
    expect(await answer).toEqual({ items: [] });
  });

  it('refreshes after a 401 and repeats the call with the new token', async () => {
    // Arrange
    auth.refresh.mockReturnValue(of('access-2'));

    // Act
    const answer = firstValueFrom(http.get('/api/v1/orders'));
    backend.expectOne('/api/v1/orders').flush(null, { status: 401, statusText: 'Unauthorized' });
    const retried = backend.expectOne('/api/v1/orders');
    retried.flush({ items: [] });

    // Assert
    expect(retried.request.headers.get('Authorization')).toBe('Bearer access-2');
    expect(await answer).toEqual({ items: [] });
  });

  it('ends the session when the API refuses the refresh, and asks to sign in again', async () => {
    // Arrange: a refused refresh token is forgotten by the refresh itself.
    auth.refresh.mockImplementation(() => {
      auth.canRefresh.mockReturnValue(false);
      return throwError(() => ({ status: 401 }));
    });

    // Act
    const answer = firstValueFrom(http.get('/api/v1/orders'));
    backend.expectOne('/api/v1/orders').flush(null, { status: 401, statusText: 'Unauthorized' });

    // Assert
    await expect(answer).rejects.toBeTruthy();
    await vi.waitFor(() => expect(TestBed.inject(Router).url).toBe('/login?returnUrl=%2Forders'));
    expect(auth.clear).toHaveBeenCalled();
  });

  it('ends the session at once when another tab signed out (no refresh token left)', async () => {
    // Arrange
    auth.canRefresh.mockReturnValue(false);

    // Act
    const answer = firstValueFrom(http.get('/api/v1/orders'));
    backend.expectOne('/api/v1/orders').flush(null, { status: 401, statusText: 'Unauthorized' });

    // Assert
    await expect(answer).rejects.toBeTruthy();
    await vi.waitFor(() => expect(TestBed.inject(Router).url).toBe('/login?returnUrl=%2Forders'));
    expect(auth.refresh).not.toHaveBeenCalled();
  });

  it('keeps the session when the refresh could not reach the API', async () => {
    // Arrange: the refresh token stays stored.
    auth.refresh.mockReturnValue(throwError(() => ({ status: 0 })));

    // Act
    const answer = firstValueFrom(http.get('/api/v1/orders'));
    backend.expectOne('/api/v1/orders').flush(null, { status: 401, statusText: 'Unauthorized' });

    // Assert
    await expect(answer).rejects.toBeTruthy();
    expect(TestBed.inject(Router).url).toBe('/orders');
    expect(auth.clear).not.toHaveBeenCalled();
  });

  it("leaves a sign-in call's 401 — wrong credentials — to its screen", async () => {
    // Act
    const answer = firstValueFrom(http.post('/api/v1/auth/login', {}));
    backend.expectOne('/api/v1/auth/login').flush(null, { status: 401, statusText: 'Unauthorized' });

    // Assert
    await expect(answer).rejects.toBeTruthy();
    expect(auth.refresh).not.toHaveBeenCalled();
    expect(TestBed.inject(Router).url).toBe('/orders');
  });
});
