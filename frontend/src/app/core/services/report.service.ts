import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { OnTimeReport, EmployeePerformanceReport, AiSummaryResult } from '../models';
import { API_BASE_URL } from './api-base';

@Injectable({ providedIn: 'root' })
export class ReportService {
  constructor(private http: HttpClient) {}

  async getOnTimeResolutionReport(): Promise<OnTimeReport> {
    return firstValueFrom(this.http.get<OnTimeReport>(`${API_BASE_URL}/reports/on-time-resolution`));
  }

  /**
   * includeAiNarrative defaults to false so the numbers-only view loads
   * fast; pass true to also request the optional AI summary (SRS v2.0
   * §4.10) — it may come back unavailable, which is expected and handled
   * by the caller, not an error.
   */
  async getEmployeePerformanceReport(employeeId: string, includeAiNarrative = false): Promise<EmployeePerformanceReport> {
    return firstValueFrom(
      this.http.get<EmployeePerformanceReport>(`${API_BASE_URL}/reports/employee-performance/${employeeId}`, {
        params: { includeAiNarrative },
      })
    );
  }

  /**
   * AI narrative summary for any report table on the Reports page. Sends
   * the same columns/rows already rendered on screen — this is
   * best-effort and may return available:false, which callers should
   * treat as "no summary shown", never as an error blocking the table.
   */
  async summarizeTabularReport(title: string, columns: string[], rows: (string | number)[][]): Promise<AiSummaryResult> {
    const stringRows = rows.map(row => row.map(cell => String(cell)));
    return firstValueFrom(
      this.http.post<AiSummaryResult>(`${API_BASE_URL}/reports/summarize`, { title, columns, rows: stringRows })
    );
  }
}
