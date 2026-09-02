import { Injectable, signal } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { MaintenanceRecord, MaintenanceStatus, PagedResult } from '../models';
import { API_BASE_URL } from './api-base';

@Injectable({ providedIn: 'root' })
export class MaintenanceService {
  private readonly _records = signal<MaintenanceRecord[]>([]);
  readonly records = this._records.asReadonly();

  // Paged state for the Maintenance table. Kept separate from the
  // full-list cache above, which the category filter and reports rely on.
  private readonly _page = signal(1);
  private readonly _pageSize = signal(20);
  private readonly _totalCount = signal(0);
  private readonly _totalPages = signal(0);
  private readonly _pagedRecords = signal<MaintenanceRecord[]>([]);
  readonly pagedRecords = this._pagedRecords.asReadonly();
  readonly page = this._page.asReadonly();
  readonly pageSize = this._pageSize.asReadonly();
  readonly totalCount = this._totalCount.asReadonly();
  readonly totalPages = this._totalPages.asReadonly();

  constructor(private http: HttpClient) {
    void this.refresh();
    void this.refreshPaged();
  }

  async refresh(): Promise<void> {
    const list = await firstValueFrom(this.http.get<MaintenanceRecord[]>(`${API_BASE_URL}/maintenance`));
    this._records.set(list);
  }

  async refreshPaged(page = this._page(), pageSize = this._pageSize()): Promise<void> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    const result = await firstValueFrom(
      this.http.get<PagedResult<MaintenanceRecord>>(`${API_BASE_URL}/maintenance/paged`, { params })
    );
    this._page.set(result.page);
    this._pageSize.set(result.pageSize);
    this._totalCount.set(result.totalCount);
    this._totalPages.set(result.totalPages);
    this._pagedRecords.set(result.items);
  }

  async goToPage(page: number): Promise<void> {
    await this.refreshPaged(page);
  }

  async create(data: {
    category: string; description: string; performedByEmployeeId: string; status: MaintenanceStatus; remarks?: string;
    clientId?: string; systemProductId?: string; ticketId?: string;
  }): Promise<MaintenanceRecord> {
    const record = await firstValueFrom(this.http.post<MaintenanceRecord>(`${API_BASE_URL}/maintenance`, data));
    await Promise.all([this.refresh(), this.refreshPaged()]);
    return record;
  }

  /** A client's maintenance history, newest first — powers the Maintenance History tab on Client Detail. Employee-only per MaintenanceController (a client hitting this endpoint would only ever see their own via the /client/{clientId} ownership check, but this call site is admin-side). */
  async getForClient(clientId: string): Promise<MaintenanceRecord[]> {
    const list = await firstValueFrom(this.http.get<MaintenanceRecord[]>(`${API_BASE_URL}/maintenance/client/${clientId}`));
    return [...list].sort((a, b) => b.date.localeCompare(a.date));
  }

  /** A system/product's maintenance history, newest first — powers the Maintenance History tab on the System/Product panel. Employee-only endpoint (MaintenanceController.GetForSystemProduct). */
  async getForSystemProduct(systemProductId: string): Promise<MaintenanceRecord[]> {
    const list = await firstValueFrom(this.http.get<MaintenanceRecord[]>(`${API_BASE_URL}/maintenance/system-product/${systemProductId}`));
    return [...list].sort((a, b) => b.date.localeCompare(a.date));
  }
}
