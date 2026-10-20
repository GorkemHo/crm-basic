import { Component } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';

import { PageHeader } from '../../shared/page-header';
import { TaskList } from './task-list';

/** The caller's tasks by default, with a switch to everyone's. */
@Component({
  selector: 'app-task-page',
  imports: [TranslocoPipe, PageHeader, TaskList],
  template: `
    <app-page-header [title]="'tasks.title' | transloco" [subtitle]="'tasks.subtitle' | transloco" />
    <div class="card"><app-task-list [scope]="true" [onlyMine]="true" /></div>
  `,
})
export class TaskPage {}
