import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';

import { environment } from '../../../environments/environment';
import { AuthService } from '../services/auth.service';

/**
 * Attaches the in-memory access token to outgoing API requests and enables credentials
 * (cookies) so the httpOnly refresh-token cookie flows on /api/auth/* calls.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.includes('/api/')) {
    return next(req);
  }

  const token = inject(AuthService).getAccessToken();
  const headers = token ? req.headers.set('Authorization', `Bearer ${token}`) : req.headers;

  return next(req.clone({ headers, withCredentials: true }));
};
