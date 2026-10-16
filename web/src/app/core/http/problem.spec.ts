import { ApiException } from '../api/api-client';
import { leftToInterceptors, problemOf, shownByInterceptor } from './problem';

describe('problemOf', () => {
  it('reads a declared status from the ProblemDetails the client throws', () => {
    // Act
    const problem = problemOf({
      status: 400,
      code: 'VALIDATION_FAILED',
      errors: { email: ['INVALID'] },
      traceId: 'abc',
    });

    // Assert
    expect(problem).toEqual({ status: 400, code: 'VALIDATION_FAILED', errors: { email: ['INVALID'] }, traceId: 'abc' });
  });

  it('reads an undeclared status from the ApiException and its raw body', () => {
    // Arrange
    const error = new ApiException(
      'Unexpected',
      500,
      '{"status":500,"code":"UNEXPECTED_ERROR","traceId":"t"}',
      {},
      null,
    );

    // Act
    const problem = problemOf(error);

    // Assert
    expect(problem).toEqual({ status: 500, code: 'UNEXPECTED_ERROR', errors: {}, traceId: 't' });
  });

  it('has no status, code or errors when nothing answered', () => {
    // Act
    const problem = problemOf(new Error('network'));

    // Assert
    expect(problem).toEqual({ status: null, code: null, errors: {}, traceId: null });
  });

  it('drops field errors that are not codes', () => {
    // Act
    const problem = problemOf({ status: 400, errors: { email: ['INVALID', 7], name: 'TOO_LONG' } });

    // Assert
    expect(problem.errors).toEqual({ email: ['INVALID'], name: [] });
  });
});

describe('shownByInterceptor and leftToInterceptors', () => {
  it.each([
    [null, true, true],
    [0, true, true],
    [500, true, true],
    [401, false, true],
    [409, false, false],
  ])('status %s: shown by the interceptor %s, left to the interceptors %s', (status, shown, left) => {
    // Arrange
    const problem = { status, code: null, errors: {}, traceId: null };

    // Act & Assert
    expect(shownByInterceptor(problem)).toBe(shown);
    expect(leftToInterceptors(problem)).toBe(left);
  });
});
