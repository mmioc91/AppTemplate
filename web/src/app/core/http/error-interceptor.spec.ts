import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { flushProblem, jsonBlob } from '../../../testing/http';
import { errorInterceptor } from './error-interceptor';

describe('errorInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  let open: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    open = vi.fn();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
        { provide: MatSnackBar, useValue: { open } },
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
  });

  it('shows a server error with its trace id for support', async () => {
    // Act
    http.get('/api/v1/things', { responseType: 'blob' }).subscribe({ error: () => undefined });
    backend
      .expectOne('/api/v1/things')
      .flush(jsonBlob({ status: 500, code: 'UNEXPECTED_ERROR', traceId: '4bf92f3577b34da6' }), {
        status: 500,
        statusText: 'Error',
      });
    await vi.waitFor(() => expect(open).toHaveBeenCalled());

    // Assert: the source text; Croatian comes from the translation.
    expect(open.mock.calls[0][0]).toContain('4bf92f3577b34da6');
  });

  it('shows a message when the server can not be reached', async () => {
    // Act
    http.get('/api/v1/things').subscribe({ error: () => undefined });
    backend.expectOne('/api/v1/things').error(new ProgressEvent('error'), { status: 0 });
    await vi.waitFor(() => expect(open).toHaveBeenCalled());

    // Assert
    expect(open.mock.calls[0][0]).toContain("can't be reached");
  });

  it('leaves a 4xx to the screen', async () => {
    // Act: a 4xx, then a 5xx as a marker — once the marker's message is out, a message for the 4xx (taking the same
    // asynchronous path) would be out too.
    http.get('/api/v1/things', { responseType: 'blob' }).subscribe({ error: () => undefined });
    flushProblem(backend.expectOne('/api/v1/things'), 409, 'CONCURRENCY_CONFLICT');
    http.get('/api/v1/other', { responseType: 'blob' }).subscribe({ error: () => undefined });
    flushProblem(backend.expectOne('/api/v1/other'), 500, 'UNEXPECTED_ERROR');
    await vi.waitFor(() => expect(open).toHaveBeenCalled());
    await new Promise((resolve) => setTimeout(resolve, 20));

    // Assert: only the marker's.
    expect(open).toHaveBeenCalledOnce();
  });
});
