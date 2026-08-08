import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';

import { AuthService } from '../services/auth.service';

const AUTH_ENDPOINTS = ['/auth/login', '/auth/register', '/auth/refresh'];

/**
 * On a 401 from any endpoint other than the auth endpoints themselves, attempts a single
 * silent refresh (via the httpOnly cookie) and retries the original request once.
 */
export const refreshInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const isAuthEndpoint = AUTH_ENDPOINTS.some((path) => req.url.includes(path));

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status !== 401 || isAuthEndpoint) {
        return throwError(() => error);
      }

      return authService.refresh().pipe(
        switchMap(() => {
          const token = authService.getAccessToken();
          const retried = req.clone({
            headers: token ? req.headers.set('Authorization', `Bearer ${token}`) : req.headers,
          });
          return next(retried);
        }),
        catchError((refreshError) => throwError(() => refreshError)),
      );
    }),
  );
};
