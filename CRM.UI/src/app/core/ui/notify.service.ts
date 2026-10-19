import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { FormGroup } from '@angular/forms';
import { TranslocoService } from '@jsverse/transloco';
import { MessageService } from 'primeng/api';

/** RFC 7807 problem returned by the API for every error. */
export interface ApiProblem {
  status: number;
  title?: string;
  detail?: string;
  /** Stable code, e.g. `company.tax_number_taken`; translated via `errors.<code>`. */
  code?: string;
  errors?: Record<string, string[]>;
  traceId?: string;
}

export function getProblem(error: unknown): ApiProblem | null {
  return error instanceof HttpErrorResponse && error.error && typeof error.error === 'object'
    ? (error.error as ApiProblem)
    : null;
}

/** Toasts and API-error presentation. */
@Injectable({ providedIn: 'root' })
export class NotifyService {
  private readonly messages = inject(MessageService);
  private readonly transloco = inject(TranslocoService);

  success(key: string, params?: Record<string, unknown>): void {
    this.messages.add({ severity: 'success', summary: this.transloco.translate(key, params), life: 3000 });
  }

  /** Shows an API error: a translation of its code when we have one, otherwise the API's message. */
  error(error: unknown): void {
    this.messages.add({
      severity: 'error',
      summary: this.transloco.translate('common.error'),
      detail: this.describe(error),
      life: 6000,
    });
  }

  describe(error: unknown): string {
    if (error instanceof HttpErrorResponse && error.status === 0) {
      return this.transloco.translate('errors.network');
    }

    const problem = getProblem(error);
    if (!problem) {
      return this.transloco.translate('errors.unexpected');
    }

    const key = problem.code ? `errors.${problem.code}` : null;
    if (key && this.transloco.translate(key) !== key) {
      return this.transloco.translate(key);
    }

    const fieldErrors = Object.values(problem.errors ?? {}).flat();
    return problem.detail && !fieldErrors.length ? problem.detail : fieldErrors.join(' ') || problem.title || '';
  }

  /**
   * Puts API validation errors on the matching form controls (`server` error) and returns true if any
   * field matched; callers show a toast otherwise.
   */
  applyToForm(form: FormGroup, error: unknown): boolean {
    const errors = getProblem(error)?.errors;
    if (!errors) return false;

    let matched = false;
    for (const [field, messages] of Object.entries(errors)) {
      const control = form.get(field);
      if (control) {
        control.setErrors({ ...control.errors, server: messages.join(' ') });
        control.markAsTouched();
        matched = true;
      }
    }

    return matched;
  }
}
