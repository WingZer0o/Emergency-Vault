import { HttpErrorResponse } from '@angular/common/http';

/** The API returns either { errors: string[] } or { message: string } on failure. */
export function extractErrorMessage(err: HttpErrorResponse, fallback: string): string {
  return err.error?.errors?.[0] ?? err.error?.message ?? fallback;
}
