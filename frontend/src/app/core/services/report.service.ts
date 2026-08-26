import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { OnTimeReport, EmployeePerformanceReport, OperationsOverview, SupportOverview, OverallClientReport } from '../models';
import { API_BASE_URL } from './api-base';

@Injectable({ providedIn: 'root' })
export class ReportService {
  constructor(private http: HttpClient) {}

  async getOnTimeResolutionReport(): Promise<OnTimeReport> {
    return firstValueFrom(this.http.get<OnTimeReport>(`${API_BASE_URL}/reports/on-time-resolution`));
  }

  /** Live system-wide ticket-status breakdown + headline counts for the "Overall Operations" pie chart. */
  async getSupportOverview(): Promise<SupportOverview> {
    return firstValueFrom(this.http.get<SupportOverview>(`${API_BASE_URL}/reports/support-overview`));
  }

  async getOperationsOverview(): Promise<OperationsOverview> {
    return firstValueFrom(this.http.get<OperationsOverview>(`${API_BASE_URL}/reports/operations-overview`));
  }

  /** Everything about one client in one call — profile, systems/products with agreements and training history, every ticket, every satisfaction survey, and a summary block. Admin-only. Powers the Reports page's "Overall Client Report" tab. */
  async getOverallClientReport(clientId: string): Promise<OverallClientReport> {
    return firstValueFrom(this.http.get<OverallClientReport>(`${API_BASE_URL}/reports/client-report/${clientId}`));
  }

  async getEmployeePerformanceReport(employeeId: string): Promise<EmployeePerformanceReport> {
    return firstValueFrom(
      this.http.get<EmployeePerformanceReport>(`${API_BASE_URL}/reports/employee-performance/${employeeId}`)
    );
  }
}
