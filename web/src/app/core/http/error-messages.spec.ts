import { errorMessage } from './error-messages';

describe('errorMessage', () => {
  it('says what a known code means', () => {
    // Act & Assert
    expect(errorMessage('AUTH_INVALID_CREDENTIALS')).toBe('Wrong e-mail address or password.');
  });

  it.each([null, undefined, '', 'NO_SUCH_CODE'])('falls back to the general message for %s', (code) => {
    // Act & Assert
    expect(errorMessage(code)).toBe('Something went wrong. Please try again.');
  });
});
