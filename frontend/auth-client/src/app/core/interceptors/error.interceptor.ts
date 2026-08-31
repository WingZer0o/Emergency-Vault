import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { catchError, throwError } from 'rxjs';

/**
 * Surfaces a toast whenever the backend's global exception handler produces a 500 (or the
 * request fails outright, e.g. the API is unreachable). Expected failures (4xx from a
 * ServiceResult) are left alone so features can show their own inline/form error handling.
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const snackBar = inject(MatSnackBar);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 0 || error.status >= 500) {
        snackBar.open('Something went wrong. Please try again.', 'Dismiss', {
          duration: 6000,
          panelClass: 'error-snackbar',
        });
      }

      return throwError(() => error);
    }),
  );
};
