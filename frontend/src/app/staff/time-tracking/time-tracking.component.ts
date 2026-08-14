import { Component, computed, effect, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { EmployeeService } from '../../core/services/employee.service';
import { AuthService } from '../../core/services/auth.service';
import { PaginationComponent } from '../../shared/pagination.component';

@Component({
  selector: 'app-time-tracking',
  standalone: true,
  imports: [FormsModule, DatePipe, PaginationComponent],
  template: `
    <h1>Time Tracking</h1>
    <p class="text-muted" style="margin-top:0.3rem;">{{ isAdmin() ? 'Clock in/out and review attendance across the team.' : 'Clock in/out and review your own attendance.' }}</p>

    @if (me(); as m) {
      <div class="panel panel-pad clock-panel" style="margin-top:1.25rem;">
        <div>
          <div class="clock-label">{{ m.fullName }}</div>
          <div class="text-muted" style="font-size:0.8rem;">{{ todayStatus() }}</div>
        </div>
        <div class="clock-actions">
          @if (!hasOpenLogToday()) {
            <button class="btn btn-primary" (click)="clockIn(m.id)">Clock In</button>
          } @else {
            <button class="btn btn-secondary" (click)="clockOut(m.id)">Clock Out</button>
          }
        </div>
      </div>
    }

    <div class="panel panel-pad" style="margin-top:1.25rem;">
      @if (isAdmin()) {
        <div class="filters">
          <select [ngModel]="employeeFilter()" (ngModelChange)="employeeFilter.set($event)">
            <option value="">All employees</option>
            @for (e of employees.employees(); track e.id) { <option [value]="e.id">{{ e.fullName }}</option> }
          </select>
        </div>
      }
      <div class="table-scroll"><table>
        <thead><tr>@if (isAdmin()) {<th>Employee</th>} <th>Date</th><th>Start</th><th>Finish</th><th>Total Hours</th></tr></thead>
        <tbody>
          @for (l of filteredLogs(); track l.id) {
            <tr>
              @if (isAdmin()) {<td>{{ employeeName(l.employeeId) }}</td>}
              <td>{{ l.date }}</td>
              <td class="text-muted">{{ l.startTime ? (l.startTime | date:'shortTime') : '—' }}</td>
              <td class="text-muted">{{ l.finishTime ? (l.finishTime | date:'shortTime') : '—' }}</td>
              <td>{{ l.totalHours ? (l.totalHours + ' h') : '—' }}</td>
            </tr>
          }
          @empty { <tr><td [attr.colspan]="isAdmin() ? 5 : 4" class="text-muted" style="text-align:center; padding:1.5rem;">No time logs for this filter.</td></tr> }
        </tbody>
      </table></div>
      @if (isAdmin()) {
        <app-pagination
          [page]="employees.timeLogsPage()"
          [totalPages]="employees.timeLogsTotalPages()"
          [totalCount]="employees.timeLogsTotalCount()"
          [pageSize]="employees.timeLogsPageSize()"
          (pageChange)="goToPage($event)">
        </app-pagination>
      }
    </div>
  `,
  styles: [`
    .clock-panel { display: flex; justify-content: space-between; align-items: center; }
    .clock-label { font-weight: 600; }
    .filters { margin-bottom: 1rem; }
  `],
})
export class TimeTrackingComponent {
  employeeFilter = signal('');

  constructor(public employees: EmployeeService, private auth: AuthService) {
    // Re-fetch page 1 of the admin table whenever the employee filter changes.
    effect(() => {
      const filter = this.employeeFilter();
      if (this.isAdmin()) {
        void this.employees.refreshTimeLogsPaged(filter || undefined, 1);
      }
    });
  }

  me = computed(() => this.auth.currentEmployee());
  isAdmin = computed(() => this.me()?.roles.includes('Admin') ?? false);

  hasOpenLogToday = computed(() => {
    const m = this.me();
    if (!m) return false;
    const today = new Date().toISOString().slice(0, 10);
    return this.employees.timeLogs().some(l => l.employeeId === m.id && l.date === today && !l.finishTime);
  });

  todayStatus = computed(() => (this.hasOpenLogToday() ? 'Currently clocked in' : 'Not clocked in today'));

  filteredLogs = computed(() => {
    const m = this.me();
    if (!m) return [];

    // A non-Admin (Technician) only ever sees their own attendance — the
    // "All employees" filter and the Employee column don't apply to them at
    // all, so there's nothing to switch on here besides their own id. This
    // list is small (one person's history), so it stays unpaged.
    if (!this.isAdmin()) {
      return this.employees.timeLogs()
        .filter(l => l.employeeId === m.id)
        .sort((a, b) => b.date.localeCompare(a.date));
    }

    // Admin view is server-paged and server-filtered by employeeId already.
    return this.employees.pagedTimeLogs();
  });

  goToPage(page: number) {
    void this.employees.goToTimeLogsPage(page, this.employeeFilter() || undefined);
  }

  employeeName(id: string): string {
    return this.employees.getById(id)?.fullName ?? id;
  }

  async clockIn(employeeId: string) {
    await this.employees.clockIn(employeeId);
  }

  async clockOut(employeeId: string) {
    await this.employees.clockOut(employeeId);
  }
}