import { HttpContext, HttpContextToken } from '@angular/common/http';

/**
 * When set to true on a request, the error interceptor will not show global UI for that
 * request's error (neither toast nor modale 403). The caller handles feedback (e.g. inline or dialog).
 */
export const SKIP_ERROR_TOAST = new HttpContextToken<boolean>(() => false);

/** Context for requests where the caller suppresses global error UI (e.g. dashboard aggregate calls). */
export function createHttpContextSkipGlobalErrorUi(): HttpContext {
  return new HttpContext().set(SKIP_ERROR_TOAST, true);
}
