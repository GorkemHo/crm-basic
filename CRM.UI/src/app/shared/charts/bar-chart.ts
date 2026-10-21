import { Component, computed, input, signal } from '@angular/core';

export interface ChartSeries {
  label: string;
  /** Categorical slot 1–4 (fixed order: follow the entity, never its rank). */
  slot: 1 | 2 | 3 | 4;
  values: number[];
}

interface Bar {
  x: number;
  y: number;
  width: number;
  height: number;
  slot: number;
  /** Only the top segment of a column gets rounded corners. */
  rounded: boolean;
}

const WIDTH = 640;
const PLOT_TOP = 12;
const PLOT_LEFT = 56;
const PLOT_RIGHT = 8;
const AXIS_HEIGHT = 28;

/** Rounds the axis maximum up to 1, 2 or 5 × 10ⁿ so ticks land on readable numbers. */
function niceMax(value: number): number {
  if (value <= 0) return 1;
  const power = 10 ** Math.floor(Math.log10(value));
  const step = [1, 2, 5, 10].find((s) => s * power >= value)!;
  return step * power;
}

/**
 * Vertical bar chart (stacked or grouped) as SVG: one y-axis, recessive grid, thin bars with rounded
 * data ends, a 2px gap between fills, hover tooltip per column, legend for two or more series and the
 * numbers in a table below (relief for colors with low contrast, and for screen readers).
 */
@Component({
  selector: 'app-bar-chart',
  template: `
    <figure class="chart">
      @if (series().length > 1) {
        <ul class="legend">
          @for (s of series(); track s.label) {
            <li><span class="swatch" [style.background]="color(s.slot)"></span>{{ s.label }}</li>
          }
        </ul>
      }

      <div class="plot" (mouseleave)="hover.set(null)">
        <svg [attr.viewBox]="'0 0 ' + width + ' ' + height()" preserveAspectRatio="none" role="img" [attr.aria-label]="label()">
          @for (tick of ticks(); track tick.value) {
            <line class="grid" [attr.x1]="plotLeft" [attr.x2]="width - plotRight" [attr.y1]="tick.y" [attr.y2]="tick.y" />
          }
          @for (bar of bars(); track $index) {
            <path [attr.d]="path(bar)" [style.fill]="color(bar.slot)" />
          }
          <line class="baseline" [attr.x1]="plotLeft" [attr.x2]="width - plotRight" [attr.y1]="baseY()" [attr.y2]="baseY()" />
          @for (column of columns(); track $index) {
            <rect
              class="hit"
              [attr.x]="column.x"
              [attr.y]="plotTop"
              [attr.width]="column.width"
              [attr.height]="baseY() - plotTop"
              (mouseenter)="hover.set($index)"
            />
          }
        </svg>

        <div class="y-axis">
          @for (tick of ticks(); track tick.value) {
            <span [style.top.%]="(tick.y / height()) * 100">{{ format()(tick.value) }}</span>
          }
        </div>
        <div class="x-axis" [style.left.%]="(plotLeft / width) * 100" [style.right.%]="(plotRight / width) * 100">
          @for (category of categories(); track $index) {
            <span>{{ category }}</span>
          }
        </div>

        @if (hover() !== null) {
          <div class="tooltip" [style.left.%]="tooltipLeft()" [class.flip]="tooltipLeft() > 60">
            <strong>{{ categories()[hover()!] }}</strong>
            @for (s of series(); track s.label) {
              <span><span class="swatch" [style.background]="color(s.slot)"></span>{{ s.label }}: {{ format()(s.values[hover()!]) }}</span>
            }
          </div>
        }
      </div>

      <details class="table-view">
        <summary>{{ tableLabel() }}</summary>
        <table>
          <thead>
            <tr>
              <th></th>
              @for (s of series(); track s.label) {
                <th>{{ s.label }}</th>
              }
            </tr>
          </thead>
          <tbody>
            @for (category of categories(); track $index; let i = $index) {
              <tr>
                <th>{{ category }}</th>
                @for (s of series(); track s.label) {
                  <td>{{ format()(s.values[i]) }}</td>
                }
              </tr>
            }
          </tbody>
        </table>
      </details>
    </figure>
  `,
  styles: `
    .chart {
      margin: 0;
    }
    .legend {
      display: flex;
      flex-wrap: wrap;
      gap: 0.25rem 1rem;
      list-style: none;
      margin: 0 0 0.5rem;
      padding: 0;
      font: var(--mat-sys-body-small);
      color: var(--mat-sys-on-surface-variant);
    }
    .swatch {
      display: inline-block;
      width: 0.7rem;
      height: 0.7rem;
      border-radius: 0.2rem;
      margin-right: 0.35rem;
      vertical-align: -0.05rem;
    }
    .plot {
      position: relative;
      padding-bottom: 1.5rem;
    }
    svg {
      display: block;
      width: 100%;
      height: 12rem;
    }
    .grid {
      stroke: var(--mat-sys-outline-variant);
      stroke-width: 1;
      vector-effect: non-scaling-stroke;
      opacity: 0.6;
    }
    .baseline {
      stroke: var(--mat-sys-outline);
      stroke-width: 1;
      vector-effect: non-scaling-stroke;
    }
    .hit {
      fill: transparent;
    }
    .hit:hover {
      fill: var(--mat-sys-on-surface);
      fill-opacity: 0.05;
    }
    .y-axis {
      position: absolute;
      top: 0;
      left: 0;
      right: 0;
      height: 12rem; /* same as the svg */
      pointer-events: none;
    }
    .y-axis span {
      position: absolute;
      left: 0;
      width: calc(100% * ${PLOT_LEFT - 6} / ${WIDTH});
      transform: translateY(-50%);
      text-align: right;
      font: var(--mat-sys-label-small);
      color: var(--mat-sys-on-surface-variant);
      white-space: nowrap;
    }
    .x-axis {
      position: absolute;
      bottom: 0;
      display: flex;
    }
    .x-axis span {
      flex: 1;
      text-align: center;
      font: var(--mat-sys-label-small);
      color: var(--mat-sys-on-surface-variant);
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .tooltip {
      position: absolute;
      top: 0;
      transform: translateX(0.75rem);
      display: flex;
      flex-direction: column;
      gap: 0.15rem;
      padding: 0.5rem 0.75rem;
      border-radius: 0.5rem;
      background: var(--mat-sys-inverse-surface);
      color: var(--mat-sys-inverse-on-surface);
      font: var(--mat-sys-body-small);
      pointer-events: none;
      white-space: nowrap;
      z-index: 1;
    }
    .tooltip.flip {
      transform: translateX(calc(-100% - 0.75rem));
    }
    .table-view {
      margin-top: 0.5rem;
      font: var(--mat-sys-body-small);
    }
    .table-view summary {
      cursor: pointer;
      color: var(--mat-sys-on-surface-variant);
    }
    .table-view table {
      border-collapse: collapse;
      margin-top: 0.5rem;
    }
    .table-view th,
    .table-view td {
      padding: 0.2rem 0.75rem 0.2rem 0;
      text-align: right;
    }
    .table-view th:first-child {
      text-align: left;
    }
  `,
})
export class BarChart {
  readonly categories = input.required<string[]>();
  readonly series = input.required<ChartSeries[]>();
  readonly stacked = input(false);
  readonly format = input<(value: number) => string>((value) => String(value));
  /** Accessible name of the chart. */
  readonly label = input('');
  readonly tableLabel = input('Data');

  readonly width = WIDTH;
  readonly plotTop = PLOT_TOP;
  readonly plotLeft = PLOT_LEFT;
  readonly plotRight = PLOT_RIGHT;
  readonly height = signal(220);
  readonly hover = signal<number | null>(null);

  readonly baseY = computed(() => this.height() - AXIS_HEIGHT);

  private readonly max = computed(() => {
    const series = this.series();
    const totals = this.categories().map((_, i) =>
      this.stacked() ? series.reduce((sum, s) => sum + (s.values[i] ?? 0), 0) : Math.max(0, ...series.map((s) => s.values[i] ?? 0)),
    );
    return niceMax(Math.max(0, ...totals));
  });

  readonly ticks = computed(() => {
    const max = this.max();
    return [0, 0.25, 0.5, 0.75, 1].map((f) => ({ value: max * f, y: this.y(max * f) }));
  });

  readonly columns = computed(() => {
    const count = Math.max(1, this.categories().length);
    const width = (WIDTH - PLOT_LEFT - PLOT_RIGHT) / count;
    return this.categories().map((_, i) => ({ x: PLOT_LEFT + i * width, width }));
  });

  readonly bars = computed<Bar[]>(() => {
    const series = this.series();
    const bars: Bar[] = [];

    this.columns().forEach((column, i) => {
      const inner = Math.min(column.width * 0.6, 48);
      const left = column.x + (column.width - inner) / 2;

      if (this.stacked()) {
        let base = 0;
        const filled = series.filter((s) => (s.values[i] ?? 0) > 0);
        filled.forEach((s, index) => {
          const value = s.values[i];
          const top = this.y(base + value);
          // 2px surface gap between stacked fills.
          const bottom = this.y(base) - (index > 0 ? 2 : 0);
          bars.push({ x: left, y: top, width: inner, height: Math.max(0, bottom - top), slot: s.slot, rounded: index === filled.length - 1 });
          base += value;
        });
      } else {
        const gap = 2;
        const each = (inner - gap * (series.length - 1)) / series.length;
        series.forEach((s, index) => {
          const value = s.values[i] ?? 0;
          if (value <= 0) return;
          const top = this.y(value);
          bars.push({ x: left + index * (each + gap), y: top, width: each, height: this.baseY() - top, slot: s.slot, rounded: true });
        });
      }
    });

    return bars;
  });

  readonly tooltipLeft = computed(() => {
    const index = this.hover();
    if (index === null) return 0;
    const column = this.columns()[index];
    return ((column.x + column.width / 2) / WIDTH) * 100;
  });

  color(slot: number): string {
    return `var(--viz-${slot})`;
  }

  /** A bar with 4px rounded corners on its data end only. */
  path(bar: Bar): string {
    const r = bar.rounded ? Math.min(4, bar.width / 2, bar.height) : 0;
    const { x, y, width: w, height: h } = bar;
    return `M${x},${y + h} V${y + r} Q${x},${y} ${x + r},${y} H${x + w - r} Q${x + w},${y} ${x + w},${y + r} V${y + h} Z`;
  }

  private y(value: number): number {
    return this.baseY() - (value / this.max()) * (this.baseY() - PLOT_TOP);
  }
}
