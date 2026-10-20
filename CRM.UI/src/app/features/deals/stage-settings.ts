import { CdkDrag, CdkDragDrop, CdkDragHandle, CdkDropList, moveItemInArray } from '@angular/cdk/drag-drop';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TranslocoPipe } from '@jsverse/transloco';
import { Observable, filter } from 'rxjs';

import { StageDto } from '../../core/api/models';
import { ConfirmService } from '../../core/ui/confirm.service';
import { NotifyService } from '../../core/ui/notify.service';
import { FieldError } from '../../shared/field-error';
import { PageHeader } from '../../shared/page-header';
import { StagesApi } from './deals.api';

/** Add or edit a stage (name and win probability). */
@Component({
  selector: 'app-stage-form-dialog',
  imports: [ReactiveFormsModule, TranslocoPipe, MatDialogModule, MatButtonModule, MatFormFieldModule, MatInputModule, FieldError],
  template: `
    <h2 mat-dialog-title>{{ (stage ? 'pipeline.editStage' : 'pipeline.newStage') | transloco }}</h2>
    <mat-dialog-content>
      <form [formGroup]="form" (ngSubmit)="save()" class="form-grid" id="stage-form">
        <mat-form-field>
          <mat-label>{{ 'pipeline.stageName' | transloco }}</mat-label>
          <input matInput id="stageName" formControlName="name" cdkFocusInitial />
          <mat-error><app-field-error [control]="form.controls.name" /></mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>{{ 'pipeline.winProbability' | transloco }}</mat-label>
          <input matInput id="probability" type="number" min="0" max="100" formControlName="probability" />
          <span matTextSuffix>%</span>
          <mat-error><app-field-error [control]="form.controls.probability" /></mat-error>
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton mat-dialog-close type="button">{{ 'common.cancel' | transloco }}</button>
      <button matButton="filled" type="submit" form="stage-form" [disabled]="saving()">{{ 'common.save' | transloco }}</button>
    </mat-dialog-actions>
  `,
})
class StageFormDialog {
  private readonly api = inject(StagesApi);
  private readonly notify = inject(NotifyService);
  private readonly dialogRef = inject(MatDialogRef<StageFormDialog, StageDto>);
  readonly stage = inject<StageDto | null>(MAT_DIALOG_DATA);

  readonly saving = signal(false);
  readonly form = inject(FormBuilder).nonNullable.group({
    name: [this.stage?.name ?? '', [Validators.required, Validators.maxLength(100)]],
    probability: [this.stage?.probability ?? 50, [Validators.required, Validators.min(0), Validators.max(100)]],
  });

  static open(dialog: MatDialog, stage: StageDto | null = null): Observable<StageDto | undefined> {
    return dialog.open<StageFormDialog, StageDto | null, StageDto>(StageFormDialog, { data: stage, width: 'min(28rem, 95vw)' }).afterClosed();
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { name, probability } = this.form.getRawValue();
    const request = this.stage ? this.api.update(this.stage.id, { name, probability: Number(probability) }) : this.api.create(name, Number(probability));

    this.saving.set(true);
    request.subscribe({
      next: (saved) => {
        this.notify.success(this.stage ? 'common.saved' : 'common.created');
        this.dialogRef.close(saved);
      },
      error: (error) => {
        this.saving.set(false);
        if (!this.notify.applyToForm(this.form, error)) this.notify.error(error);
      },
    });
  }
}

/** Pipeline stages: drag to reorder, add, edit, delete (only stages without deals). */
@Component({
  selector: 'app-stage-settings',
  imports: [TranslocoPipe, MatButtonModule, MatProgressBarModule, MatTooltipModule, CdkDropList, CdkDrag, CdkDragHandle, PageHeader],
  template: `
    <app-page-header [title]="'pipeline.stagesTitle' | transloco" [subtitle]="'pipeline.stagesSubtitle' | transloco" backLink="/pipeline">
      <button matButton="filled" (click)="create()"><span class="material-symbols-outlined">add</span> {{ 'pipeline.newStage' | transloco }}</button>
    </app-page-header>

    <div class="card">
      @if (loading()) {
        <mat-progress-bar mode="indeterminate" />
      }
      <ol class="stages" cdkDropList (cdkDropListDropped)="drop($event)">
        @for (stage of stages(); track stage.id; let i = $index) {
          <li class="stage" cdkDrag>
            <span class="material-symbols-outlined handle" cdkDragHandle [matTooltip]="'pipeline.dragToReorder' | transloco">drag_indicator</span>
            <span class="position">{{ i + 1 }}</span>
            <span class="name">{{ stage.name }}</span>
            <span class="muted">{{ 'pipeline.probability' | transloco: { n: stage.probability } }}</span>
            <span class="muted deals">{{ 'pipeline.openDeals' | transloco: { n: stage.openDealCount } }}</span>
            <button matIconButton [matTooltip]="'common.edit' | transloco" [attr.aria-label]="'common.edit' | transloco" (click)="edit(stage)">
              <span class="material-symbols-outlined">edit</span>
            </button>
            <button matIconButton [matTooltip]="'common.delete' | transloco" [attr.aria-label]="'common.delete' | transloco" (click)="remove(stage)">
              <span class="material-symbols-outlined">delete</span>
            </button>
          </li>
        }
      </ol>
      <p class="muted hint">{{ 'pipeline.stagesHint' | transloco }}</p>
    </div>
  `,
  styles: `
    button .material-symbols-outlined {
      margin-right: 0.25rem;
    }
    .stages {
      list-style: none;
      margin: 0;
      padding: 0;
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
    }
    .stage {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      padding: 0.25rem 0.5rem;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: 0.75rem;
      background: var(--mat-sys-surface);
    }
    .handle {
      cursor: grab;
      color: var(--mat-sys-on-surface-variant);
    }
    .position {
      font: var(--mat-sys-label-large);
      width: 1.5rem;
      text-align: center;
    }
    .name {
      flex: 1;
      font: var(--mat-sys-body-large);
    }
    .deals {
      min-width: 7rem;
    }
    .hint {
      margin: 1rem 0 0;
      font: var(--mat-sys-body-small);
    }
    .cdk-drag-placeholder {
      opacity: 0.4;
    }
    .cdk-drag-preview {
      box-shadow: var(--mat-sys-level3);
    }
  `,
})
export class StageSettings {
  private readonly api = inject(StagesApi);
  private readonly dialog = inject(MatDialog);
  private readonly notify = inject(NotifyService);
  private readonly confirm = inject(ConfirmService);

  readonly stages = signal<StageDto[]>([]);
  readonly loading = signal(true);

  constructor() {
    this.load();
  }

  drop(event: CdkDragDrop<StageDto[]>): void {
    if (event.previousIndex === event.currentIndex) return;

    const ordered = [...this.stages()];
    moveItemInArray(ordered, event.previousIndex, event.currentIndex);
    this.stages.set(ordered);

    this.api.reorder(ordered.map((s) => s.id)).subscribe({
      next: (stages) => {
        this.stages.set(stages);
        this.notify.success('common.saved');
      },
      error: (error) => {
        this.notify.error(error);
        this.load();
      },
    });
  }

  create(): void {
    StageFormDialog.open(this.dialog).pipe(filter(Boolean)).subscribe(() => this.load());
  }

  edit(stage: StageDto): void {
    StageFormDialog.open(this.dialog, stage).pipe(filter(Boolean)).subscribe(() => this.load());
  }

  remove(stage: StageDto): void {
    this.confirm.delete(stage.name, () =>
      this.api.delete(stage.id).subscribe({
        next: () => {
          this.notify.success('common.deleted');
          this.load();
        },
        error: (error) => this.notify.error(error),
      }),
    );
  }

  private load(): void {
    this.loading.set(true);
    this.api.list().subscribe({
      next: (stages) => {
        this.stages.set(stages);
        this.loading.set(false);
      },
      error: (error) => {
        this.loading.set(false);
        this.notify.error(error);
      },
    });
  }
}
