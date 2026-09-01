import { ValidatorFn, Validators } from '@angular/forms';

// Kept in sync with the account password rule enforced server-side (see PasswordRules in
// EmergencyVault.Application.Dtos.AuthDtos and the Identity options in
// EmergencyVault.Infrastructure.DependencyInjection): at least 10 characters, one uppercase
// letter, one special (non-alphanumeric) character.
export const PASSWORD_STRENGTH_PATTERN = /^(?=.*[A-Z])(?=.*[^a-zA-Z0-9]).{10,}$/;

export const PASSWORD_STRENGTH_MESSAGE =
  'Password must be at least 10 characters and include an uppercase letter and a special character.';

export const passwordStrengthValidators: ValidatorFn[] = [
  Validators.required,
  Validators.pattern(PASSWORD_STRENGTH_PATTERN),
];
