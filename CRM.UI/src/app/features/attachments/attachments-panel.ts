import { HttpEventType } from '@angular/common/http';
import { Component, effect, inject, input, signal, untracked } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TranslocoPipe } from '@jsverse/transloco';

import { AttachmentDto, AttachmentRecordType } from '../../core/api/models';
import { AuthStore } from '../../core/auth/auth.store';
import { Permissions } from '../../core/auth/permissions';
import { AppDatePipe } from '../../core/i18n/date.pipe';
import { LanguageService } from '../../core/i18n/language.service';
import { ConfirmService } from '../../core/ui/confirm.service';
import { NotifyService } from '../../core/ui/notify.service';
import { AttachmentsApi, fileIcon, formatSize } from './attachments.api';

interface Upload {
  name: string;
  /** 0–100 */
  progress: number;
}

/** Files of one record: upload (button or drop), download, delete for the uploader or a manager. */
@Component({
  selector: 'app-attachments-panel',
  imports: [TranslocoPipe, MatButtonModule, MatProgressBarModule, MatTooltipModule, AppDatePipe],
  template: `
    @if (canWrite) {
      <div
        class="drop-zone"
        [class.dragging]="dragging()"
        (dragover)="$event.preventDefault(); dragging.set(true)"
        (dragleave)="dragging.set(false)"
        (drop)="onDrop($event)"
      >
        <span class="material-symbols-outlined">upload_file</span>
        <span>{{ 'attachments.dropHere' | transloco }}</span>
        <button matButton="tonal" type="button" (click)="picker.click()">{{ 'attachments.choose' | transloco }}</button>
        <input #picker class="picker" type="file" multiple (change)="onPick(picker)" />
      </div>
    }

    @for (upload of uploads(); track upload.name) {
      <div class="upload">
        <span class="muted">{{ upload.name }}</span>
        <mat-progress-bar mode="determinate" [value]="upload.progress" />
      </div>
    }

    @if (loading() && items().length === 0) {
      <mat-progress-bar mode="indeterminate" />
    } @else if (items().length === 0) {
      <p class="muted empty">{{ 'attachments.empty' | transloco }}</p>
    } @else {
      <ul class="files">
        @for (file of items(); track file.id) {
          <li class="file">
            <span class="material-symbols-outlined icon">{{ icon(file.contentType) }}</span>
            <div class="body">
              <button class="name" type="button" (click)="download(file)" [matTooltip]="'attachments.download' | transloco">{{ file.fileName }}</button>
              <span class="muted meta">
                {{ size(file.size) }} · {{ file.createdByName ?? ('history.bySystem' | transloco) }} · {{ file.createdAt | appDate }}
              </span>
            </div>
            <button matIconButton (click)="download(file)" [attr.aria-label]="'attachments.download' | transloco">
              <span class="material-symbols-outlined">download</span>
            </button>
            @if (canDelete(file)) {
              <button matIconButton (click)="remove(file)" [attr.aria-label]="'common.delete' | transloco">
                <span class="material-symbols-outlined">delete</span>
              </button>
            }
          </li>
        }
      </ul>
    }
  `,
  styles: `
    .drop-zone {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: center;
      gap: 0.75rem;
      padding: 1.25rem;
      margin-bottom: 1rem;
      border: 2px dashed var(--mat-sys-outline-variant);
      border-radius: var(--app-radius);
      color: var(--mat-sys-on-surface-variant);
    }
    .drop-zone.dragging {
      border-color: var(--mat-sys-primary);
      background: var(--mat-sys-primary-container);
    }
    .picker {
      display: none;
    }
    .upload {
      margin-bottom: 0.75rem;
    }
    .empty {
      padding: 0.5rem 0;
    }
    .files {
      list-style: none;
      margin: 0;
      padding: 0;
    }
    .file {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      padding: 0.5rem 0;
      border-bottom: 1px solid var(--mat-sys-outline-variant);
    }
    .file:last-child {
      border-bottom: none;
    }
    .icon {
      color: var(--mat-sys-primary);
    }
    .body {
      flex: 1;
      min-width: 0;
      display: flex;
      flex-direction: column;
    }
    .name {
      border: none;
      background: none;
      padding: 0;
      font: var(--mat-sys-body-large);
      color: var(--mat-sys-primary);
      text-align: left;
      cursor: pointer;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .meta {
      font: var(--mat-sys-body-small);
    }
  `,
})
export class AttachmentsPanel {
  private readonly api = inject(AttachmentsApi);
  private readonly notify = inject(NotifyService);
  private readonly confirm = inject(ConfirmService);
  private readonly language = inject(LanguageService);

  private readonly store = inject(AuthStore);
  readonly canWrite = this.store.can(Permissions.attachmentsWrite);
  private readonly canManage = this.store.can(Permissions.attachmentsManage);

  readonly recordType = input.required<AttachmentRecordType>();
  readonly recordId = input.required<number>();

  readonly items = signal<AttachmentDto[]>([]);
  readonly uploads = signal<Upload[]>([]);
  readonly loading = signal(true);
  readonly dragging = signal(false);

  readonly icon = fileIcon;

  constructor() {
    effect(() => {
      this.recordType();
      this.recordId();
      untracked(() => this.load());
    });
  }

  size(bytes: number): string {
    return formatSize(bytes, this.language.current());
  }

  canDelete(file: AttachmentDto): boolean {
    return this.canWrite && (this.canManage || file.createdBy === this.store.user()?.id);
  }

  onPick(input: HTMLInputElement): void {
    this.uploadAll(Array.from(input.files ?? []));
    input.value = '';
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    this.dragging.set(false);
    this.uploadAll(Array.from(event.dataTransfer?.files ?? []));
  }

  download(file: AttachmentDto): void {
    this.api.download(file).subscribe({ error: (error) => this.notify.error(error) });
  }

  remove(file: AttachmentDto): void {
    this.confirm.delete(file.fileName, () =>
      this.api.delete(file.id).subscribe({
        next: () => {
          this.notify.success('common.deleted');
          this.load();
        },
        error: (error) => this.notify.error(error),
      }),
    );
  }

  private uploadAll(files: File[]): void {
    for (const file of files) {
      const upload: Upload = { name: file.name, progress: 0 };
      this.uploads.update((list) => [...list, upload]);
      const done = () => this.uploads.update((list) => list.filter((u) => u !== upload));

      this.api.upload(this.recordType(), this.recordId(), file).subscribe({
        next: (event) => {
          if (event.type === HttpEventType.UploadProgress && event.total) {
            upload.progress = Math.round((100 * event.loaded) / event.total);
            this.uploads.update((list) => [...list]);
          } else if (event.type === HttpEventType.Response && event.body) {
            done();
            this.items.update((items) => [event.body!, ...items]);
            this.notify.success('attachments.uploaded', { name: file.name });
          }
        },
        error: (error) => {
          done();
          this.notify.error(error);
        },
      });
    }
  }

  private load(): void {
    this.loading.set(true);
    this.api.list(this.recordType(), this.recordId()).subscribe({
      next: (items) => {
        this.items.set(items);
        this.loading.set(false);
      },
      error: (error) => {
        this.loading.set(false);
        this.notify.error(error);
      },
    });
  }
}
