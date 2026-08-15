import { Component, computed, signal } from '@angular/core';
import { BarChartComponent, BarChartDatum } from '../../shared/bar-chart.component';
import { DonutChartComponent, DonutSlice } from '../../shared/donut-chart.component';
import { ReportService } from '../../core/services/report.service';
import { ClientService } from '../../core/services/client.service';
import { AgreementService } from '../../core/services/agreement.service';
import { TicketService } from '../../core/services/ticket.service';
import { MaintenanceService } from '../../core/services/maintenance.service';
import { EmployeeService } from '../../core/services/employee.service';
import { SatisfactionSurveyService } from '../../core/services/satisfaction-survey.service';
import { PdfExportService, PdfReportSpec } from '../../core/services/pdf-export.service';
import { OnTimeReport, AiSummaryResult, OperationsOverview } from '../../core/models';

interface ReportDef {
  id: string;
  title: string;
  description: string;
}

const REPORTS: ReportDef[] = [
  { id: 'clients-agreements', title: 'Active Clients & Agreement Status', description: 'All clients with current agreement status and billing tier.' },
  { id: 'tickets-by-filter', title: 'Tickets by Client / Employee / Date Range', description: 'Ticket volume and resolution breakdown across filters.' },
  { id: 'agreements-expiring', title: 'Agreements Expiring Soon or Expired', description: 'Upcoming and past-due agreement renewals.' },
  { id: 'maintenance-history', title: 'Maintenance History', description: 'Internal maintenance records by category, date range, or employee.' },
  { id: 'time-performance', title: 'Employee Time-Log & Performance', description: 'Attendance combined with ticket resolution stats per employee.' },
  { id: 'satisfaction-surveys', title: 'Client Satisfaction Survey Responses', description: 'The 5-question follow-up survey, aggregated across all respondents.' },
];

@Component({
  selector: 'app-reports',
  standalone: true,
  imports: [BarChartComponent, DonutChartComponent],
  template: `
    <h1>Reports</h1>
    <p class="text-muted" style="margin-top:0.3rem;">Generate downloadable reports across the system.</p>

    <div class="panel panel-pad" style="margin-top:1.25rem;">
      <div class="chart-header">
        <div>
          <h3>Overall Operations</h3>
          <p class="text-muted" style="font-size:0.82rem; margin-top:0.25rem;">
            Live snapshot of every ticket in the system right now, by current status.
          </p>
        </div>
      </div>

      @if (opsLoading()) {
        <p class="text-muted" style="margin-top:1rem;">Loading…</p>
      } @else {
        @if (ops(); as o) {
          <div class="chart-grid">
            <div class="chart-cell">
              <h4>Tickets by Status ({{ o.totalTickets }} total)</h4>
              <app-donut-chart [data]="opsDonutData()" [centerOverride]="o.totalTickets" centerLabel="Tickets" centerSuffix=""></app-donut-chart>
            </div>
            <div class="chart-cell">
              <h4>At a Glance</h4>
              <div class="stat-row"><span class="stat-label">Active Clients</span><span class="stat-value">{{ o.activeClients }}</span></div>
              <div class="stat-row"><span class="stat-label">Active Employees</span><span class="stat-value">{{ o.activeEmployees }}</span></div>
              <div class="stat-row"><span class="stat-label">Active Agreements</span><span class="stat-value">{{ o.openAgreements }}</span></div>
              <div class="stat-row"><span class="stat-label">Total Tickets</span><span class="stat-value">{{ o.totalTickets }}</span></div>
            </div>
          </div>
        }
      }
    </div>

    <div class="panel panel-pad" style="margin-top:1.25rem;">
      <div class="chart-header">
        <div>
          <h3>On-Time Ticket Resolution</h3>
          <p class="text-muted" style="font-size:0.82rem; margin-top:0.25rem;">
            "On time" means resolved within {{ report()?.summary?.targetDays ?? '—' }} days of assignment.
          </p>
        </div>
        <button class="btn btn-secondary btn-sm" (click)="downloadOnTimeReport()" [disabled]="!report()">Download as PDF</button>
      </div>

      @if (loading()) {
        <p class="text-muted" style="margin-top:1rem;">Loading…</p>
      } @else {
        @if (report(); as r) {
          <div class="chart-grid">
            <div class="chart-cell">
              <h4>Overall</h4>
              <app-donut-chart [data]="donutData()" centerLabel="On Time"></app-donut-chart>
            </div>
            <div class="chart-cell">
              <h4>On-Time Rate by Employee</h4>
              <app-bar-chart [chartData]="barData()"></app-bar-chart>
            </div>
          </div>
        }
      }
    </div>

    <div class="stack" style="margin-top:1.25rem;">
      @for (r of reports; track r.id) {
        <div class="panel panel-pad">
          <div class="report-header">
            <div>
              <h3>{{ r.title }}</h3>
              <p class="text-muted" style="font-size:0.83rem; margin-top:0.4rem;">{{ r.description }}</p>
            </div>
            <div class="report-actions">
              <button class="btn btn-secondary btn-sm" (click)="toggle(r.id)" [disabled]="generating() === r.id">
                {{ generating() === r.id ? 'Loading…' : (isOpen(r.id) ? 'Hide' : 'View Report') }}
              </button>
              <button class="btn btn-outline btn-sm" (click)="downloadPdf(r.id)" [disabled]="generating() === r.id">
                Download as PDF
              </button>
            </div>
          </div>

          @if (isOpen(r.id)) {
            @if (specCache().get(r.id); as spec) {
              @for (section of spec.sections; track section.heading ?? ''; let first = $first) {
                @if (section.heading) { <h4 class="section-heading">{{ section.heading }}</h4> }

                @if (first) {
                  <div class="ai-summary" [class.unavailable]="!summaryFor(r.id)?.available">
                    @if (summaryLoading() === r.id) {
                      <p class="text-muted" style="margin:0;">Generating AI summary…</p>
                    } @else if (summaryFor(r.id)?.available) {
                      <p style="margin:0;">🤖 {{ summaryFor(r.id)?.narrative }}</p>
                    } @else if (summaryFor(r.id)) {
                      <p class="text-muted" style="margin:0; font-size:0.82rem;">AI summary unavailable — {{ summaryFor(r.id)?.unavailableReason ?? 'try again later.' }}</p>
                    }
                  </div>
                }

                <div class="table-scroll">
                  <table>
                    <thead><tr>@for (col of section.columns; track col) { <th>{{ col }}</th> }</tr></thead>
                    <tbody>
                      @for (row of section.rows; track $index) {
                        <tr>@for (cell of row; track $index) { <td>{{ cell }}</td> }</tr>
                      }
                      @empty { <tr><td [attr.colspan]="section.columns.length" class="text-muted" style="text-align:center; padding:1.5rem;">No data yet.</td></tr> }
                    </tbody>
                  </table>
                </div>
              }
            }
          }
        </div>
      }
    </div>
  `,
  styles: [`
    .stack { display: flex; flex-direction: column; gap: 1rem; }
    .report-header { display: flex; justify-content: space-between; align-items: flex-start; gap: 1rem; flex-wrap: wrap; }
    .report-actions { display: flex; gap: 0.5rem; flex-shrink: 0; }
    .section-heading { margin: 1.1rem 0 0.6rem; font-size: 0.85rem; color: var(--navy-800); }
    .ai-summary { background: var(--slate-50, #f8fafc); border: 1px solid var(--slate-200, #e2e8f0); border-radius: 8px; padding: 0.75rem 0.9rem; margin-bottom: 0.75rem; font-size: 0.87rem; }
    .ai-summary.unavailable { background: transparent; border-style: dashed; }
    .chart-header { display: flex; justify-content: space-between; align-items: flex-start; }
    .chart-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 2rem; margin-top: 1.5rem; align-items: start; }
    .chart-cell h4 { font-size: 0.82rem; margin-bottom: 0.9rem; color: var(--navy-800); }
    @media (max-width: 800px) { .chart-grid { grid-template-columns: 1fr; } }
    .stat-row { display: flex; justify-content: space-between; align-items: center; padding: 0.55rem 0; border-top: 1px solid rgba(0,0,0,0.06); font-size: 0.85rem; }
    .stat-row:first-of-type { border-top: none; }
    .stat-label { color: var(--slate-500); }
    .stat-value { font-weight: 700; color: var(--navy-900); }
  `],
})
export class ReportsComponent {
  reports = REPORTS;
  generating = signal<string | null>(null);

  openIds = signal<Set<string>>(new Set());
  specCache = signal<Map<string, PdfReportSpec>>(new Map());
  summaries = signal<Map<string, AiSummaryResult>>(new Map());
  summaryLoading = signal<string | null>(null);

  report = signal<OnTimeReport | null>(null);
  loading = signal(true);

  ops = signal<OperationsOverview | null>(null);
  opsLoading = signal(true);

  constructor(
    private reportsSvc: ReportService,
    private clients: ClientService,
    private agreements: AgreementService,
    private tickets: TicketService,
    private maintenance: MaintenanceService,
    private employees: EmployeeService,
    private surveys: SatisfactionSurveyService,
    private pdf: PdfExportService
  ) {
    void this.load();
    void this.loadOps();
  }

  private async load() {
    this.loading.set(true);
    try {
      const r = await this.reportsSvc.getOnTimeResolutionReport();
      this.report.set(r);
    } finally {
      this.loading.set(false);
    }
  }

  private async loadOps() {
    this.opsLoading.set(true);
    try {
      const o = await this.reportsSvc.getOperationsOverview();
      this.ops.set(o);
    } finally {
      this.opsLoading.set(false);
    }
  }

  /** Same status → color mapping as app-badge, so the pie chart reads consistently with badges shown everywhere else (green = healthy/done, amber = active work, red = escalated, blue = awaiting the client, slate = anything else). Zero-count statuses are dropped so the legend doesn't clutter with slices that aren't there. */
  private readonly statusColors: Record<string, string> = {
    Resolved: '#16a34a',
    Closed: '#16a34a',
    Submitted: '#d97706',
    Forwarded: '#d97706',
    Assigned: '#d97706',
    InProgress: '#d97706',
    AwaitingClientConfirmation: '#2563eb',
    Escalated: 'var(--brand-red, #dc2626)',
  };

  opsDonutData = computed((): DonutSlice[] => {
    const o = this.ops();
    if (!o) return [];
    return o.ticketsByStatus
      .filter(s => s.count > 0)
      .map(s => ({
        label: s.status.replace(/([a-z])([A-Z])/g, '$1 $2'),
        value: s.count,
        color: this.statusColors[s.status] ?? '#64748b',
      }));
  });

  donutData = computed((): DonutSlice[] => {
    const r = this.report();
    if (!r) return [];
    return [
      { label: 'On Time', value: r.summary.onTimeCount, color: '#16a34a' },
      { label: 'Late', value: r.summary.lateCount, color: 'var(--brand-red, #dc2626)' },
    ];
  });

  barData = computed((): BarChartDatum[] => {
    const r = this.report();
    if (!r) return [];
    return r.byEmployee.map(e => ({
      label: e.employeeName,
      value: e.onTimeRate,
      color: e.onTimeRate >= 90 ? '#16a34a' : e.onTimeRate >= 70 ? '#b45309' : 'var(--brand-red, #dc2626)',
    }));
  });

  private generatedAt(): string {
    return `Generated ${new Date().toLocaleString()}`;
  }

  downloadOnTimeReport() {
    const r = this.report();
    if (!r) return;
    const spec: PdfReportSpec = {
      title: 'On-Time Ticket Resolution',
      subtitle: `${this.generatedAt()} — target: resolve within ${r.summary.targetDays} days`,
      sections: [
        {
          heading: 'Overall',
          columns: ['On Time', 'Late', 'On-Time Rate'],
          rows: [[r.summary.onTimeCount, r.summary.lateCount, `${this.overallRate(r)}%`]],
        },
        {
          heading: 'By Employee',
          columns: ['Employee', 'On-Time Rate'],
          rows: r.byEmployee.map(e => [e.employeeName, `${e.onTimeRate}%`]),
        },
      ],
    };
    this.pdf.export(spec, 'on-time-resolution-report');
  }

  private overallRate(r: OnTimeReport): number {
    const total = r.summary.onTimeCount + r.summary.lateCount;
    return total === 0 ? 0 : Math.round((r.summary.onTimeCount / total) * 100);
  }

  isOpen(id: string): boolean {
    return this.openIds().has(id);
  }

  summaryFor(id: string): AiSummaryResult | undefined {
    return this.summaries().get(id);
  }

  async toggle(id: string) {
    const open = new Set(this.openIds());
    if (open.has(id)) {
      open.delete(id);
      this.openIds.set(open);
      return;
    }
    open.add(id);
    this.openIds.set(open);

    if (this.specCache().has(id)) return;

    this.generating.set(id);
    try {
      const spec = await this.buildSpec(id);
      if (!spec) return;
      const cache = new Map(this.specCache());
      cache.set(id, spec);
      this.specCache.set(cache);

      void this.loadSummary(id, spec);
    } finally {
      this.generating.set(null);
    }
  }

  private async loadSummary(id: string, spec: PdfReportSpec) {
    const section = spec.sections[0];
    if (!section || section.rows.length === 0) return;

    this.summaryLoading.set(id);
    try {
      const result = await this.reportsSvc.summarizeTabularReport(spec.title, section.columns, section.rows);
      const map = new Map(this.summaries());
      map.set(id, result);
      this.summaries.set(map);
    } catch {
      const map = new Map(this.summaries());
      map.set(id, { available: false, unavailableReason: 'Could not reach the AI summary service.' });
      this.summaries.set(map);
    } finally {
      this.summaryLoading.set(null);
    }
  }

  async downloadPdf(id: string) {
    this.generating.set(id);
    try {
      const spec = this.specCache().get(id) ?? await this.buildSpec(id);
      if (spec) this.pdf.export(spec, id);
    } finally {
      this.generating.set(null);
    }
  }

  private async buildSpec(id: string): Promise<PdfReportSpec | null> {
    switch (id) {
      case 'clients-agreements':
        return this.buildClientsAgreementsSpec();
      case 'tickets-by-filter':
        return this.buildTicketsSpec();
      case 'agreements-expiring':
        return this.buildAgreementsExpiringSpec();
      case 'maintenance-history':
        return this.buildMaintenanceSpec();
      case 'time-performance':
        return this.buildTimePerformanceSpec();
      case 'satisfaction-surveys':
        return this.buildSatisfactionSurveysSpec();
      default:
        return null;
    }
  }

  private async buildClientsAgreementsSpec(): Promise<PdfReportSpec> {
    await Promise.all([this.clients.refresh(), this.agreements.refresh()]);
    const clientList = this.clients.clients();
    return {
      title: 'Active Clients & Agreement Status',
      subtitle: this.generatedAt(),
      sections: [{
        columns: ['Client', 'Status', 'Office', 'Agreements', 'Billing Tiers'],
        rows: clientList.map(c => {
          const clientAgreements = this.agreements.forClient(c.id);
          return [
            c.name,
            c.accountStatus,
            c.office,
            clientAgreements.length,
            clientAgreements.map(a => a.billingTier).join(', ') || '—',
          ];
        }),
      }],
    };
  }

  private async buildTicketsSpec(): Promise<PdfReportSpec> {
    await this.tickets.refresh();
    const ticketList = this.tickets.tickets();
    return {
      title: 'Tickets by Client / Employee / Date Range',
      subtitle: `${this.generatedAt()} — all tickets currently in the system`,
      sections: [{
        columns: ['Client', 'Employee', 'Category', 'Status', 'Submitted', 'Chargeable'],
        rows: ticketList.map(t => [
          t.clientName,
          t.assignedEmployeeName ?? 'Unassigned',
          t.category,
          t.status,
          new Date(t.dateSubmitted).toLocaleDateString(),
          t.chargeable ? 'Yes' : 'No',
        ]),
      }],
    };
  }

  private async buildAgreementsExpiringSpec(): Promise<PdfReportSpec> {
    await this.agreements.refresh();
    const expiring = this.agreements.expiringSoon();
    return {
      title: 'Agreements Expiring Soon or Expired',
      subtitle: `${this.generatedAt()} — within 30 days or already past expiry`,
      sections: [{
        columns: ['Client', 'Document #', 'Expiry Date', 'Billing Tier', 'Status'],
        rows: expiring.map(a => [
          this.clients.getById(a.clientId)?.name ?? a.clientId,
          a.documentNumber,
          a.expiryDate,
          a.billingTier,
          a.status,
        ]),
      }],
    };
  }

  private async buildMaintenanceSpec(): Promise<PdfReportSpec> {
    await this.maintenance.refresh();
    const records = this.maintenance.records();
    return {
      title: 'Maintenance History',
      subtitle: this.generatedAt(),
      sections: [{
        columns: ['Date', 'Category', 'Description', 'Performed By', 'Status'],
        rows: records.map(r => [
          r.date,
          r.category,
          r.description,
          this.employeeName(r.performedByEmployeeId),
          r.status,
        ]),
      }],
    };
  }

  private async buildTimePerformanceSpec(): Promise<PdfReportSpec> {
    await Promise.all([this.employees.refresh(), this.employees.refreshTimeLogs()]);
    const employeeList = this.employees.employees();
    const logs = this.employees.timeLogs();

    const hoursByEmployee = new Map<string, number>();
    for (const log of logs) {
      hoursByEmployee.set(log.employeeId, (hoursByEmployee.get(log.employeeId) ?? 0) + (log.totalHours ?? 0));
    }

    return {
      title: 'Employee Time-Log & Performance',
      subtitle: this.generatedAt(),
      sections: [{
        columns: ['Employee', 'Open Tickets', 'Avg. Satisfaction', 'Total Hours Logged'],
        rows: employeeList.map(e => [
          e.fullName,
          e.openTicketCount,
          e.averageSatisfactionScore != null ? e.averageSatisfactionScore.toFixed(1) : '—',
          (hoursByEmployee.get(e.id) ?? 0).toFixed(1),
        ]),
      }],
    };
  }

  private async buildSatisfactionSurveysSpec(): Promise<PdfReportSpec> {
    await this.surveys.refresh();
    const surveyList = this.surveys.surveys();
    return {
      title: 'Client Satisfaction Survey Responses',
      subtitle: `${this.generatedAt()} — ${surveyList.length} response(s)`,
      sections: [{
        columns: ['Submitted', 'Response Speed', 'Professionalism', 'Clarity', 'Would Recommend', 'Feedback'],
        rows: surveyList.map(s => [
          new Date(s.submittedAt).toLocaleDateString(),
          s.responseSpeedRating,
          s.professionalismRating,
          s.communicationClarityRating,
          s.likelihoodToRecommend,
          s.improvementFeedback ?? '—',
        ]),
      }],
    };
  }

  private employeeName(employeeId: string): string {
    return this.employees.employees().find(e => e.id === employeeId)?.fullName ?? employeeId;
  }
}