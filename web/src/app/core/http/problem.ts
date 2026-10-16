import { ApiException } from '../api/api-client';

// What a screen needs from a failed API call (ADR 0018, 0032): the status, the stable `code`, field errors as codes
// (keys in the request's JSON naming) and the trace id a user can quote to support (ADR 0028).
export interface Problem {
  readonly status: number | null;
  readonly code: string | null;
  readonly errors: Readonly<Record<string, readonly string[]>>;
  readonly traceId: string | null;
}

// The generated client throws a declared status's ProblemDetails as the error itself, and an undeclared one as an
// ApiException holding the raw body; anything else (no response at all) has no problem body.
export function problemOf(error: unknown): Problem {
  const body = ApiException.isApiException(error) ? parse(error.response) : error;
  const status = ApiException.isApiException(error) ? error.status : field(body, 'status');

  return {
    status: typeof status === 'number' ? status : null,
    code: text(field(body, 'code')),
    errors: fieldErrors(field(body, 'errors')),
    traceId: text(field(body, 'traceId')),
  };
}

// Whether the error interceptor already told the user: no response at all (status 0, or nothing with a status) or a
// server failure (5xx). A screen shows only the rest.
export function shownByInterceptor(problem: Problem): boolean {
  return problem.status === null || problem.status === 0 || problem.status >= 500;
}

// Whether a screen should say nothing of its own about a refused call: what the error interceptor tells, and a 401 —
// the session ended, and the auth interceptor takes the user to sign in. Only the sign-in screens read a 401
// themselves (wrong password), so they use shownByInterceptor instead.
export function leftToInterceptors(problem: Problem): boolean {
  return shownByInterceptor(problem) || problem.status === 401;
}

function parse(json: string): unknown {
  try {
    return JSON.parse(json);
  } catch {
    return null;
  }
}

function field(body: unknown, name: string): unknown {
  return typeof body === 'object' && body !== null ? (body as Record<string, unknown>)[name] : undefined;
}

function text(value: unknown): string | null {
  return typeof value === 'string' ? value : null;
}

function fieldErrors(value: unknown): Record<string, readonly string[]> {
  if (typeof value !== 'object' || value === null) {
    return {};
  }

  return Object.fromEntries(
    Object.entries(value).map(([name, codes]) => [
      name,
      Array.isArray(codes) ? codes.filter((code): code is string => typeof code === 'string') : [],
    ]),
  );
}
