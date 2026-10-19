import { Component, computed, input } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { AbstractControl } from '@angular/forms';
import { TranslocoPipe } from '@jsverse/transloco';
import { startWith, switchMap } from 'rxjs';

/** Shows the first validation error of a control once it was touched (client-side or the API's `server` error). */
@Component({
  selector: 'app-field-error',
  imports: [TranslocoPipe],
  template: `
    @if (error(); as e) {
      <small class="field-error">
        @switch (e.key) {
          @case ('server') { {{ e.value }} }
          @case ('required') { {{ 'common.required' | transloco }} }
          @case ('email') { {{ 'common.invalidEmail' | transloco }} }
          @case ('minlength') { {{ 'common.minLength' | transloco: { n: e.value.requiredLength } }} }
          @case ('maxlength') { {{ 'common.maxLength' | transloco: { n: e.value.requiredLength } }} }
          @case ('mismatch') { {{ 'auth.passwordsDontMatch' | transloco }} }
        }
      </small>
    }
  `,
})
export class FieldError {
  readonly control = input.required<AbstractControl>();

  // Any value/status/touched change of the control re-evaluates the message.
  private readonly changed = toSignal(
    toObservable(this.control).pipe(switchMap((control) => control.events.pipe(startWith(null)))),
  );

  readonly error = computed(() => {
    this.changed();
    const control = this.control();
    if (!control.errors || !control.touched) return null;

    const [key, value] = Object.entries(control.errors)[0];
    return { key, value };
  });
}
