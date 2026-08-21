import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { DashboardData, DashboardFilter } from '../models';
import { API_BASE_URL } from './api-base';

/** Backs the Dashboard's charts and KPI cards — see TicketReportService for the Reports module's tables (the two are intentionally separate data sources, per the product's Reports-vs-Dashboard split). */
@Injectable({ providedIn: 'root' })
export class DashboardService {
  constructor(private http: HttpClient) {}

  async getDashboardData(filter: DashboardFilter): Promise<DashboardData> {
    let params = new HttpParams();
    if (filter.fromDate) params = params.set('fromDate', filter.fromDate);
    if (filter.toDate) params = params.set('toDate', filter.toDate);
    if (filter.region) params = params.set('region', filter.region);

    return firstValueFrom(this.http.get<DashboardData>(`${API_BASE_URL}/reports/dashboard`, { params }));
  }
}
