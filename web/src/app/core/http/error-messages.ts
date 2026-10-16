// The words for each error code the API returns (ADR 0018, 0032): the API sends codes only, and the frontend owns every
// sentence, translated like every other text. A field code says how a value is wrong; the field itself is shown by its
// form. A new code a screen explains gets its line here, and its <target> in messages.hr.xlf.
const messages: Readonly<Record<string, string>> = {
  REQUIRED: $localize`:@@error.required:This field is required.`,
  INVALID: $localize`:@@error.invalid:This value isn't valid.`,
  TOO_SHORT: $localize`:@@error.tooShort:This value is too short.`,
  TOO_LONG: $localize`:@@error.tooLong:This value is too long.`,
  NOT_POSITIVE: $localize`:@@error.notPositive:Enter a number greater than zero.`,
  TOO_LARGE: $localize`:@@error.tooLarge:Enter a smaller number.`,
  NOT_ALLOWED: $localize`:@@error.notAllowed:This value isn't allowed here.`,
  NOT_FOUND: $localize`:@@error.notFound:This no longer exists.`,
  FORBIDDEN: $localize`:@@error.forbidden:You aren't allowed to do this.`,
  CONCURRENCY_CONFLICT: $localize`:@@error.concurrencyConflict:This changed in the meantime. Here it is as it is now.`,
  TOO_MANY_REQUESTS: $localize`:@@error.tooManyRequests:Too many attempts. Wait a minute and try again.`,
  AUTH_INVALID_CREDENTIALS: $localize`:@@error.invalidCredentials:Wrong e-mail address or password.`,
  AUTH_EMAIL_TAKEN: $localize`:@@error.emailTaken:An account with this e-mail address already exists.`,
};

const fallback = $localize`:@@error.unexpected:Something went wrong. Please try again.`;

// The message for a code; an unknown or missing code gets the general one rather than nothing.
export function errorMessage(code: string | null | undefined): string {
  return (code && messages[code]) || fallback;
}
