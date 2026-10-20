import { Component, input } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { TranslocoPipe } from '@jsverse/transloco';
import { Observable } from 'rxjs';

import { DealStatus } from '../../core/api/models';

/** Open / Won / Lost badge. */
@Component({
  selector: 'app-deal-status',
  imports: [TranslocoPipe],
  template: `<span class="tag" [class]="status()">{{ 'deals.status.' + status() | transloco }}</span>`,
  styles: `
    .tag {
      display: inline-block;
      padding: 0.15rem 0.6rem;
      border-radius: 1rem;
      font: var(--mat-sys-label-medium);
      background: var(--mat-sys-secondary-container);
      color: var(--mat-sys-on-secondary-container);
    }
    .won {
      background: var(--app-success-container);
      color: var(--app-success);
    }
    .lost {
      background: var(--mat-sys-error-container);
      color: var(--mat-sys-on-error-container);
    }
  `,
})
export class DealStatusTag {
  readonly status = input.required<DealStatus>();
}

/** Asks for an optional reason; closes with the reason (empty string for none) or undefined when cancelled. */
@Component({
  selector: 'app-lose-deal-dialog',
  imports: [ReactiveFormsModule, TranslocoPipe, MatDialogModule, MatButtonModule, MatFormFieldModule, MatInputModule],
  template: `
    <h2 mat-dialog-title>{{ 'deals.markLost' | transloco }}</h2>
    <mat-dialog-content>
      <mat-form-field class="full-width">
        <mat-label>{{ 'deals.lostReason' | transloco }}</mat-label>
        <textarea matInput id="lostReason" rows="3" maxlength="500" [formControl]="reason" cdkFocusInitial></textarea>
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton mat-dialog-close type="button">{{ 'common.cancel' | transloco }}</button>
      <button matButton="filled" id="confirmLost" [mat-dialog-close]="reason.value">{{ 'deals.markLost' | transloco }}</button>
    </mat-dialog-actions>
  `,
})
export class LoseDealDialog {
  readonly reason = new FormControl('', { nonNullable: true, validators: Validators.maxLength(500) });

  static open(dialog: MatDialog): Observable<string | undefined> {
    return dialog.open<LoseDealDialog, void, string>(LoseDealDialog, { width: 'min(30rem, 95vw)' }).afterClosed();
  }
}

/** Whether the date (yyyy-MM-dd) is before today and the deal still open. */
export function isOverdue(expectedCloseDate: string | null, status: DealStatus): boolean {
  if (!expectedCloseDate || status !== 'open') return false;
  return expectedCloseDate < new Date().toISOString().slice(0, 10);
}

/** Date-only values (yyyy-MM-dd) as local dates, so formatting doesn't shift them by the time zone. */
export function dateOnly(value: string | null): Date | null {
  if (!value) return null;
  const [y, m, d] = value.split('-').map(Number);
  return new Date(y, m - 1, d);
}
