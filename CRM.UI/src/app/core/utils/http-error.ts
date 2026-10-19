import { HttpErrorResponse } from '@angular/common/http';
import { Observable, lastValueFrom } from 'rxjs';

/** Extracts the API's error message (ExceptionResponse.message) from an HTTP error. */
export function getErrorMessage(error: unknown, fallback = 'Beklenmeyen bir hata oluştu.'): string {
  if (error instanceof HttpErrorResponse) {
    const body = error.error;
    if (body && typeof body === 'object' && typeof body.message === 'string') {
      return body.message;
    }
    if (error.status === 0) {
      return 'Sunucuya ulaşılamıyor.';
    }
  }

  return fallback;
}

/**
 * Awaits an HTTP observable and rethrows failures as Error(message) so that
 * DevExtreme stores show the API's message instead of a generic one.
 */
export async function request<T>(source: Observable<T>): Promise<T> {
  try {
    return await lastValueFrom(source);
  } catch (error) {
    throw new Error(getErrorMessage(error));
  }
}
