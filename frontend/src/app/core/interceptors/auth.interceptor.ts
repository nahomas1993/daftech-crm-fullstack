import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { BehaviorSubject, catchError, filter, switchMap, take, throwError } from 'rxjs';
import { API_BASE_URL } from '../services/api-base';
import { TokenStorageService } from '../services/token-storage.service';
import { AuthService } from '../services/auth.service';

// Module-level (not a service field) so every concurrent request through this
// interceptor shares the same in-flight refresh instead of each one triggering
// its own — without this, three parallel 401s would each try to refresh,
// and the second/third would be rejected as a reused/stale refresh token.
let refreshInProgress = false;
const refreshCompleted$ = new BehaviorSubject<boolean>(true);

const AUTH_ENDPOINTS = ['/auth/employee-login', '/auth/client-login', '/auth/refresh'];

/**
 * Attaches the current access token to every request to our own API, and
 * on a 401 response, attempts exactly one silent refresh-and-retry before
 * giving up and forcing a logout. Login/refresh endpoints themselves are
 * excluded (they either don't need a token yet or would recurse).
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const tokenStorage = inject(TokenStorageService);
  const auth = inject(AuthService);

  const isOwnApi = req.url.startsWith(API_BASE_URL);
  const isAuthEndpoint = AUTH_ENDPOINTS.some((path) => req.url.includes(path));

  const accessToken = tokenStorage.accessToken;
  const authorizedReq = isOwnApi && accessToken && !isAuthEndpoint
    ? req.clone({ setHeaders: { Authorization: `Bearer ${accessToken}` } })
    : req;

  return next(authorizedReq).pipe(
    catchError((error: unknown) => {
      // ASP.NET Core sometimes surfaces an expired/invalid token as 403
      // rather than 401 once a RequireClaim/RequireRole policy is involved
      // (the auth handler fails the claim check before it gets a chance to
      // report "not authenticated"). Treat both the same: attempt one
      // silent refresh-and-retry before giving up.
      if (!(error instanceof HttpErrorResponse) || (error.status !== 401 && error.status !== 403) || !isOwnApi || isAuthEndpoint) {
        return throwError(() => error);
      }

      // Someone else's refresh is already in flight — wait for it, then
      // retry this request once with whatever token comes out of it.
      if (refreshInProgress) {
        return refreshCompleted$.pipe(
          filter((done) => done),
          take(1),
          switchMap(() => {
            const refreshedToken = tokenStorage.accessToken;
            const retryReq = refreshedToken
              ? req.clone({ setHeaders: { Authorization: `Bearer ${refreshedToken}` } })
              : req;
            return next(retryReq);
          })
        );
      }

      refreshInProgress = true;
      refreshCompleted$.next(false);

      return auth.refreshTokens().pipe(
        switchMap(() => {
          refreshInProgress = false;
          refreshCompleted$.next(true);
          const refreshedToken = tokenStorage.accessToken;
          const retryReq = refreshedToken
            ? req.clone({ setHeaders: { Authorization: `Bearer ${refreshedToken}` } })
            : req;
          return next(retryReq);
        }),
        catchError((refreshError: unknown) => {
          refreshInProgress = false;
          refreshCompleted$.next(true);
          // The refresh token itself is invalid/expired/reused — nothing left
          // to do but force a clean logout so the user re-authenticates.
          auth.forceLogoutAfterRefreshFailure();
          return throwError(() => refreshError);
        })
      );
    })
  );
};
