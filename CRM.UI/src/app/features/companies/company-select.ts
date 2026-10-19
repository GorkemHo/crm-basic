import { Component, DestroyRef, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ControlValueAccessor, FormControl, NgControl, ReactiveFormsModule } from '@angular/forms';
import { ErrorStateMatcher } from '@angular/material/core';
import { MatAutocompleteModule, MatAutocompleteSelectedEvent } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { TranslocoPipe } from '@jsverse/transloco';
import { debounceTime, distinctUntilChanged, filter, startWith, switchMap, tap } from 'rxjs';

import { CompanyDto } from '../../core/api/models';
import { FieldError } from '../../shared/field-error';
import { CompaniesApi } from './companies.api';

type CompanyOption = Pick<CompanyDto, 'id' | 'companyName'>;

/**
 * Company picker that searches on the server as you type (works with any number of companies).
 * Form value is the company id; `initialName` shows the current company before any search.
 * Validation errors of the bound control are shown inside the field.
 */
@Component({
  selector: 'app-company-select',
  imports: [ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatAutocompleteModule, MatButtonModule, MatProgressSpinnerModule, TranslocoPipe, FieldError],
  template: `
    <mat-form-field class="full-width">
      <mat-label>{{ label() }}</mat-label>
      <input
        matInput
        [id]="inputId() ?? ''"
        [formControl]="text"
        [matAutocomplete]="auto"
        [errorStateMatcher]="errorState"
        (blur)="onBlur()"
        autocomplete="off"
      />
      @if (loading()) {
        <mat-spinner matSuffix diameter="18" />
      } @else if (clearable() && selectedId() !== null && !text.disabled) {
        <button matIconButton matSuffix type="button" (click)="clear()" [attr.aria-label]="'common.clearSelection' | transloco">
          <span class="material-symbols-outlined">close</span>
        </button>
      }
      <mat-autocomplete #auto="matAutocomplete" [displayWith]="displayName" (optionSelected)="onSelected($event)">
        @for (company of options(); track company.id) {
          <mat-option [value]="company">{{ company.companyName }}</mat-option>
        }
      </mat-autocomplete>
      @if (ngControl?.control; as control) {
        <mat-error><app-field-error [control]="control" /></mat-error>
      }
    </mat-form-field>
  `,
})
export class CompanySelect implements ControlValueAccessor {
  private readonly api = inject(CompaniesApi);
  /** The form control this picker is bound to (it registers itself as its value accessor). */
  readonly ngControl = inject(NgControl, { self: true, optional: true });

  readonly label = input('');
  readonly inputId = input<string>();
  readonly initialName = input<string>();
  readonly clearable = input(false);

  readonly text = new FormControl<string | CompanyOption>('', { nonNullable: true });
  readonly options = signal<CompanyOption[]>([]);
  readonly loading = signal(false);
  readonly selectedId = signal<number | null>(null);

  /** Show the outer control's error state on the inner input. */
  readonly errorState: ErrorStateMatcher = {
    isErrorState: () => !!this.ngControl?.invalid && !!this.ngControl?.touched,
  };

  private selectedName = '';
  private onChange: (value: number | null) => void = () => undefined;
  private onTouched: () => void = () => undefined;

  readonly displayName = (value: string | CompanyOption | null) =>
    typeof value === 'string' ? value : (value?.companyName ?? '');

  constructor() {
    if (this.ngControl) {
      this.ngControl.valueAccessor = this;
    }

    this.text.valueChanges
      .pipe(
        startWith(''),
        filter((value): value is string => typeof value === 'string'),
        debounceTime(250),
        distinctUntilChanged(),
        tap(() => this.loading.set(true)),
        switchMap((search) => this.api.list({ search, pageSize: 20, sortBy: 'companyName' })),
        takeUntilDestroyed(inject(DestroyRef)),
      )
      .subscribe({
        next: (page) => {
          this.loading.set(false);
          this.options.set(page.items);
        },
        error: () => this.loading.set(false),
      });
  }

  onSelected(event: MatAutocompleteSelectedEvent): void {
    const company = event.option.value as CompanyOption;
    this.selectedId.set(company.id);
    this.selectedName = company.companyName;
    this.onChange(company.id);
  }

  clear(): void {
    this.selectedId.set(null);
    this.selectedName = '';
    this.text.setValue('');
    this.onChange(null);
  }

  /** Typed text that doesn't match a chosen option reverts to the selection. */
  onBlur(): void {
    this.onTouched();
    const value = this.text.value;
    if (typeof value === 'string' && value !== this.selectedName) {
      if (value === '' && this.clearable()) {
        this.clear();
      } else {
        this.text.setValue(this.selectedName, { emitEvent: false });
      }
    }
  }

  writeValue(id: number | null): void {
    this.selectedId.set(id);
    this.selectedName = id ? (this.initialName() ?? '') : '';
    this.text.setValue(this.selectedName, { emitEvent: false });
  }

  registerOnChange(fn: (value: number | null) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(disabled: boolean): void {
    if (disabled) this.text.disable({ emitEvent: false });
    else this.text.enable({ emitEvent: false });
  }
}
