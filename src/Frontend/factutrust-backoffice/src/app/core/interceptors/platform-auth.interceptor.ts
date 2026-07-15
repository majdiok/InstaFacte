import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { PlatformAuthService } from '@core/services/platform-auth.service';
import { environment } from '@environments/environment';

export const platformAuthInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(PlatformAuthService);
  const url = req.url;
  const isOurApi = url.startsWith(environment.apiUrl) || url.startsWith('/api');
  if (!isOurApi) {
    return next(req);
  }

  const token = auth.getAccessToken();
  const headers: Record<string, string> = {};
  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }

  const authReq = Object.keys(headers).length ? req.clone({ setHeaders: headers }) : req;

  return next(authReq).pipe(
    catchError((err: HttpErrorResponse) => {
      if (
        err.status === 401 &&
        token &&
        !url.includes('/platform/auth/login') &&
        !url.includes('/platform/auth/refresh')
      ) {
        return auth.tryRefresh().pipe(
          switchMap(res => {
            if (res.success && res.data?.accessToken) {
              const retry = req.clone({
                setHeaders: { Authorization: `Bearer ${res.data.accessToken}` }
              });
              return next(retry);
            }
            auth.clearSessionAndNavigate();
            return throwError(() => err);
          }),
          catchError(() => {
            auth.clearSessionAndNavigate();
            return throwError(() => err);
          })
        );
      }
      return throwError(() => err);
    })
  );
};
