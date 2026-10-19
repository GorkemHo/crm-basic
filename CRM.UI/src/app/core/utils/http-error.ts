import { HttpErrorResponse } from '@angular/common/http';
import { Observable, lastValueFrom } from 'rxjs';

/** RFC 7807 problem response returned by the API for every error. */
export interface ApiProblem {
  status: number;
  title?: string;
  detail?: string;
  /** Stable machine-readable code, e.g. `company.tax_number_taken`. */
  code?: string;
  errors?: Record<string, string[]>;
  traceId?: string;
}

export function getProblem(error: unknown): ApiProblem | null {
  if (error instanceof HttpErrorResponse && error.error && typeof error.error === 'object') {
    return error.error as ApiProblem;
  }
  return null;
}

/** Extracts a human-readable message from an API problem response. */
export function getErrorMessage(error: unknown, fallback = 'Beklenmeyen bir hata oluştu.'): string {
  if (error instanceof HttpErrorResponse && error.status === 0) {
    return 'Sunucuya ulaşılamıyor.';
  }

  const problem = getProblem(error);
  if (!problem) return fallback;

  if (problem.detail) return problem.detail;

  // Model validation errors: show every field message.
  const fieldErrors = Object.values(problem.errors ?? {}).flat();
  if (fieldErrors.length) return fieldErrors.join(' ');

  return problem.title ?? fallback;
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
