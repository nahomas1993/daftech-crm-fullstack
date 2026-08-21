import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import {
  TicketReportFilter, TableReportResult, ReportType,
  CustomerSupportReportRow, EmployeePerformanceReportRow, RegionalReportRow,
  FailureTypeReportRow, ResolutionTimeReportRow, CustomerRatingReportRow,
} from '../models';
import { API_BASE_URL } from './api-base';

/**
 * Backs the Reports module (tables only — see DashboardService, once built
 * in Phase 3, for charts/KPIs). Every report shares the same
 * TicketReportFilter and pagination shape, so this service is one thin
 * generic fetch method per report plus PDF/CSV export — no client-side
 * caching/signals like the other services, since a report's result set is
 * always fetched fresh for its current filter+page rather than being a
 * long-lived collection the rest of the app reacts to.
 */
@Injectable({ providedIn: 'root' })
export class TicketReportService {
  constructor(private http: HttpClient) {}

  private buildParams(filter: TicketReportFilter, page: number, pageSize: number): HttpParams {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    const entries: [string, string | number | undefined][] = [
      ['fromDate', filter.fromDate], ['toDate', filter.toDate], ['month', filter.month],
      ['region', filter.region], ['zone', filter.zone], ['woreda', filter.woreda],
      ['employeeId', filter.employeeId], ['failureTypeId', filter.failureTypeId],
      ['status', filter.status], ['supportPhase', filter.supportPhase], ['search', filter.search],
    ];
    for (const [key, value] of entries) {
      if (value !== undefined && value !== null && value !== '') {
        params = params.set(key, value);
      }
    }
    return params;
  }

  async getCustomerSupport(filter: TicketReportFilter, page: number, pageSize: number): Promise<TableReportResult<CustomerSupportReportRow>> {
    return firstValueFrom(this.http.get<TableReportResult<CustomerSupportReportRow>>(
      `${API_BASE_URL}/ticket-reports/customer-support`, { params: this.buildParams(filter, page, pageSize) }));
  }

  async getEmployeePerformance(filter: TicketReportFilter, page: number, pageSize: number): Promise<TableReportResult<EmployeePerformanceReportRow>> {
    return firstValueFrom(this.http.get<TableReportResult<EmployeePerformanceReportRow>>(
      `${API_BASE_URL}/ticket-reports/employee-performance`, { params: this.buildParams(filter, page, pageSize) }));
  }

  async getRegional(filter: TicketReportFilter, page: number, pageSize: number): Promise<TableReportResult<RegionalReportRow>> {
    return firstValueFrom(this.http.get<TableReportResult<RegionalReportRow>>(
      `${API_BASE_URL}/ticket-reports/regional`, { params: this.buildParams(filter, page, pageSize) }));
  }

  async getFailureType(filter: TicketReportFilter, page: number, pageSize: number): Promise<TableReportResult<FailureTypeReportRow>> {
    return firstValueFrom(this.http.get<TableReportResult<FailureTypeReportRow>>(
      `${API_BASE_URL}/ticket-reports/failure-type`, { params: this.buildParams(filter, page, pageSize) }));
  }

  async getResolutionTime(filter: TicketReportFilter, page: number, pageSize: number): Promise<TableReportResult<ResolutionTimeReportRow>> {
    return firstValueFrom(this.http.get<TableReportResult<ResolutionTimeReportRow>>(
      `${API_BASE_URL}/ticket-reports/resolution-time`, { params: this.buildParams(filter, page, pageSize) }));
  }

  async getCustomerRating(filter: TicketReportFilter, page: number, pageSize: number): Promise<TableReportResult<CustomerRatingReportRow>> {
    return firstValueFrom(this.http.get<TableReportResult<CustomerRatingReportRow>>(
      `${API_BASE_URL}/ticket-reports/customer-rating`, { params: this.buildParams(filter, page, pageSize) }));
  }

  /** Downloads the full filtered (unpaged) report as a PDF and triggers a browser save. */
  async exportPdf(reportType: ReportType, filter: TicketReportFilter): Promise<void> {
    const params = this.buildParams(filter, 1, 1); // page/pageSize ignored server-side for export, but harmless to include
    const blob = await firstValueFrom(
      this.http.get(`${API_BASE_URL}/ticket-reports/${reportType}/export/pdf`, { params, responseType: 'blob' })
    );
    this.saveBlob(blob, `${reportType}-report.pdf`);
  }

  /** Downloads the full filtered (unpaged) report as CSV and triggers a browser save. */
  async exportCsv(reportType: ReportType, filter: TicketReportFilter): Promise<void> {
    const params = this.buildParams(filter, 1, 1);
    const blob = await firstValueFrom(
      this.http.get(`${API_BASE_URL}/ticket-reports/${reportType}/export/csv`, { params, responseType: 'blob' })
    );
    this.saveBlob(blob, `${reportType}-report.csv`);
  }

  private saveBlob(blob: Blob, filename: string) {
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = filename;
    link.click();
    URL.revokeObjectURL(url);
  }
}
