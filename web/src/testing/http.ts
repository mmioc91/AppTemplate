import { TestRequest } from '@angular/common/http/testing';
import { CurrentUserResponse, TokenPairResponse } from '../app/core/api/api-client';

// The generated client asks for blobs (responseType: 'blob'), so test responses are JSON blobs too.
export function jsonBlob(body: unknown): Blob {
  return new Blob([JSON.stringify(body)], { type: 'application/json' });
}

// Answers a pending request with a ProblemDetails body, as the API does (ADR 0018).
export function flushProblem(
  request: TestRequest,
  status: number,
  code: string,
  errors?: Record<string, string[]>,
): void {
  request.flush(
    jsonBlob({ status, code, traceId: '4bf92f3577b34da6a3ce929d0e0e4736', ...(errors ? { errors } : {}) }),
    {
      status,
      statusText: 'Error',
    },
  );
}

export function tokenPair(accessToken: string, refreshToken: string): TokenPairResponse {
  return {
    accessToken,
    accessTokenExpiresAt: '2030-06-15T12:15:00+00:00',
    refreshToken,
    refreshTokenExpiresAt: '2030-06-29T12:00:00+00:00',
  };
}

export const signedInUser: CurrentUserResponse = {
  id: '0199c000-0000-7000-8000-000000000001',
  email: 'ana@example.com',
  displayName: 'Ana',
  roles: [],
};
