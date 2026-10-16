// The API's input limits for the auth forms (AuthRules in Application, ADR 0031), so a form refuses exactly what the
// API would. FluentValidation's lengths count the value as sent, untrimmed — so do Angular's minLength/maxLength.
export const AuthRules = {
  emailMaxLength: 256,
  passwordMinLength: 10,
  passwordMaxLength: 128,
  displayNameMaxLength: 80,
} as const;
