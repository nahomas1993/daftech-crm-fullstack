import { Component, Input, Output, EventEmitter, computed, signal } from '@angular/core';

/**
 * Pager controls for a server-paged table. Purely presentational — the
 * owning component/service holds the actual page state and data; this
 * just renders Prev/Next + page numbers and emits the page the user picked.
 *
 * Usage:
 *   <app-pagination
 *     [page]="tickets.page()"
 *     [totalPages]="tickets.totalPages()"
 *     [totalCount]="tickets.totalCount()"
 *     [pageSize]="tickets.pageSize()"
 *     (pageChange)="tickets.goToPage($event)">
 *   </app-pagination>
 */
@Component({
  selector: 'app-pagination',
  standalone: true,
  template: `
    @if (pageCount() > 0) {
      <div class="pagination">
        <span class="pagination-summary text-muted">
          Showing {{ rangeStart() }}–{{ rangeEnd() }} of {{ itemCount() }}
        </span>
        <div class="pagination-controls">
          <button
            type="button"
            class="btn btn-outline btn-sm"
            [disabled]="currentPage() <= 1"
            (click)="goTo(currentPage() - 1)">
            ← Prev
          </button>

          @for (p of pageNumbers(); track p) {
            @if (p === -1) {
              <span class="pagination-ellipsis">…</span>
            } @else {
              <button
                type="button"
                class="btn btn-sm"
                [class.btn-outline]="p !== currentPage()"
                (click)="goTo(p)">
                {{ p }}
              </button>
            }
          }

          <button
            type="button"
            class="btn btn-outline btn-sm"
            [disabled]="currentPage() >= pageCount()"
            (click)="goTo(currentPage() + 1)">
            Next →
          </button>
        </div>
      </div>
    }
  `,
  styles: [`
    .pagination {
      display: flex;
      align-items: center;
      justify-content: space-between;
      flex-wrap: wrap;
      gap: 0.75rem;
      margin-top: 1rem;
      padding-top: 0.85rem;
      border-top: 1px solid var(--border, #e5e7eb);
    }
    .pagination-summary {
      font-size: 0.8rem;
    }
    .pagination-controls {
      display: flex;
      align-items: center;
      gap: 0.3rem;
    }
    .pagination-ellipsis {
      padding: 0 0.35rem;
      color: var(--text-muted, #6b7280);
      font-size: 0.85rem;
    }
  `],
})
export class PaginationComponent {
  private readonly _page = signal(1);
  private readonly _totalPages = signal(0);
  private readonly _totalCount = signal(0);
  private readonly _pageSize = signal(20);

  @Input({ required: true }) set page(value: number) { this._page.set(value); }
  @Input({ required: true }) set totalPages(value: number) { this._totalPages.set(value); }
  @Input({ required: true }) set totalCount(value: number) { this._totalCount.set(value); }
  @Input({ required: true }) set pageSize(value: number) { this._pageSize.set(value); }

  @Output() pageChange = new EventEmitter<number>();

  currentPage = this._page.asReadonly();
  pageCount = this._totalPages.asReadonly();
  itemCount = this._totalCount.asReadonly();
  itemsPerPage = this._pageSize.asReadonly();

  rangeStart = computed(() => this.itemCount() === 0 ? 0 : (this.currentPage() - 1) * this.itemsPerPage() + 1);
  rangeEnd = computed(() => Math.min(this.currentPage() * this.itemsPerPage(), this.itemCount()));

  /** Compact page list with ellipses, e.g. 1 … 4 5 [6] 7 8 … 20 */
  pageNumbers = computed<number[]>(() => {
    const total = this.pageCount();
    const current = this.currentPage();
    if (total <= 7) return Array.from({ length: total }, (_, i) => i + 1);

    const pages = new Set<number>([1, total, current, current - 1, current + 1]);
    const sorted = [...pages].filter(p => p >= 1 && p <= total).sort((a, b) => a - b);

    const result: number[] = [];
    for (let i = 0; i < sorted.length; i++) {
      if (i > 0 && sorted[i] - sorted[i - 1] > 1) result.push(-1);
      result.push(sorted[i]);
    }
    return result;
  });

  goTo(p: number): void {
    if (p < 1 || p > this.pageCount() || p === this.currentPage()) return;
    this.pageChange.emit(p);
  }
}
