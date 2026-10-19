import { Component, Injectable, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule } from '@angular/material/dialog';
import { TranslocoPipe } from '@jsverse/transloco';
import { filter } from 'rxjs';

interface ConfirmData {
  name: string;
}

@Component({
  selector: 'app-confirm-delete-dialog',
  imports: [MatDialogModule, MatButtonModule, TranslocoPipe],
  template: `
    <h2 mat-dialog-title>{{ 'common.confirmDeleteTitle' | transloco }}</h2>
    <mat-dialog-content>{{ 'common.confirmDelete' | transloco: { name: data.name } }}</mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton mat-dialog-close>{{ 'common.cancel' | transloco }}</button>
      <button matButton="filled" class="danger" [mat-dialog-close]="true" cdkFocusInitial>{{ 'common.delete' | transloco }}</button>
    </mat-dialog-actions>
  `,
  styles: `
    .danger {
      --mat-button-filled-container-color: var(--mat-sys-error);
      --mat-button-filled-label-text-color: var(--mat-sys-on-error);
    }
  `,
})
class ConfirmDeleteDialog {
  readonly data = inject<ConfirmData>(MAT_DIALOG_DATA);
}

@Injectable({ providedIn: 'root' })
export class ConfirmService {
  private readonly dialog = inject(MatDialog);

  /** Asks "Delete "{name}"?"; runs `onAccept` when confirmed. */
  delete(name: string, onAccept: () => void): void {
    this.dialog
      .open(ConfirmDeleteDialog, { data: { name }, width: 'min(28rem, 95vw)', autoFocus: 'dialog' })
      .afterClosed()
      .pipe(filter(Boolean))
      .subscribe(() => onAccept());
  }
}
