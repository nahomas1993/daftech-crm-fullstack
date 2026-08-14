import { Injectable, signal } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { Employee, DeviceSession, LoginRecord, EmployeeRole, TimeLog, EmployeeRegisteredResult, PagedResult } from '../models';
import { API_BASE_URL } from './api-base';

@Injectable({ providedIn: 'root' })
export class EmployeeService {
  private readonly _employees = signal<Employee[]>([]);
  private readonly _timeLogs = signal<TimeLog[]>([]);
  readonly employees = this._employees.asReadonly();
  readonly timeLogs = this._timeLogs.asReadonly();

  // Paged state for the Employees table. Kept separate from the full-list
  // cache above, which activeEmployees()/getById() still rely on.
  private readonly _page = signal(1);
  private readonly _pageSize = signal(20);
  private readonly _totalCount = signal(0);
  private readonly _totalPages = signal(0);
  private readonly _pagedEmployees = signal<Employee[]>([]);
  readonly pagedEmployees = this._pagedEmployees.asReadonly();
  readonly page = this._page.asReadonly();
  readonly pageSize = this._pageSize.asReadonly();
  readonly totalCount = this._totalCount.asReadonly();
  readonly totalPages = this._totalPages.asReadonly();

  constructor(private http: HttpClient) {
    void this.refresh();
    void this.refreshPaged();
    void this.refreshTimeLogs();
    void this.refreshTimeLogsPaged();
  }

  async refresh(): Promise<void> {
    const list = await firstValueFrom(this.http.get<Employee[]>(`${API_BASE_URL}/employees`));
    this._employees.set(list);
  }

  async refreshPaged(page = this._page(), pageSize = this._pageSize()): Promise<void> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    const result = await firstValueFrom(
      this.http.get<PagedResult<Employee>>(`${API_BASE_URL}/employees/paged`, { params })
    );
    this._page.set(result.page);
    this._pageSize.set(result.pageSize);
    this._totalCount.set(result.totalCount);
    this._totalPages.set(result.totalPages);
    this._pagedEmployees.set(result.items);
  }

  async goToPage(page: number): Promise<void> {
    await this.refreshPaged(page);
  }

  async refreshTimeLogs(employeeId?: string): Promise<void> {
    const list = await firstValueFrom(
      this.http.get<TimeLog[]>(`${API_BASE_URL}/time-logs`, employeeId ? { params: { employeeId } } : {})
    );
    this._timeLogs.set(list);
  }

  // Paged state for the Time Tracking table (Admin view). Kept separate
  // from the full-list cache above, which the Technician's own-attendance
  // view and the "currently clocked in" check still rely on.
  private readonly _timeLogsPage = signal(1);
  private readonly _timeLogsPageSize = signal(20);
  private readonly _timeLogsTotalCount = signal(0);
  private readonly _timeLogsTotalPages = signal(0);
  private readonly _pagedTimeLogs = signal<TimeLog[]>([]);
  readonly pagedTimeLogs = this._pagedTimeLogs.asReadonly();
  readonly timeLogsPage = this._timeLogsPage.asReadonly();
  readonly timeLogsPageSize = this._timeLogsPageSize.asReadonly();
  readonly timeLogsTotalCount = this._timeLogsTotalCount.asReadonly();
  readonly timeLogsTotalPages = this._timeLogsTotalPages.asReadonly();

  async refreshTimeLogsPaged(employeeId?: string, page = this._timeLogsPage(), pageSize = this._timeLogsPageSize()): Promise<void> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (employeeId) params = params.set('employeeId', employeeId);
    const result = await firstValueFrom(
      this.http.get<PagedResult<TimeLog>>(`${API_BASE_URL}/time-logs/paged`, { params })
    );
    this._timeLogsPage.set(result.page);
    this._timeLogsPageSize.set(result.pageSize);
    this._timeLogsTotalCount.set(result.totalCount);
    this._timeLogsTotalPages.set(result.totalPages);
    this._pagedTimeLogs.set(result.items);
  }

  async goToTimeLogsPage(page: number, employeeId?: string): Promise<void> {
    await this.refreshTimeLogsPaged(employeeId, page);
  }

  async clockIn(employeeId: string): Promise<void> {
    await firstValueFrom(this.http.post(`${API_BASE_URL}/time-logs/${employeeId}/clock-in`, {}));
    await Promise.all([this.refreshTimeLogs(), this.refreshTimeLogsPaged()]);
  }

  async clockOut(employeeId: string): Promise<void> {
    await firstValueFrom(this.http.post(`${API_BASE_URL}/time-logs/${employeeId}/clock-out`, {}));
    await Promise.all([this.refreshTimeLogs(), this.refreshTimeLogsPaged()]);
  }

  activeEmployees(): Employee[] {
    return this._employees().filter(e => e.accountStatus === 'Active');
  }

  getById(id: string): Employee | undefined {
    return this._employees().find(e => e.id === id);
  }

  /**
   * Admin registers a new staff account. The API generates a username and
   * one-time password and returns them ONCE in this response — the caller
   * must show them to the Admin immediately, since they can't be
   * retrieved again afterward.
   */
  async registerEmployee(data: {
    fullName: string; email: string; phoneNumber: string; specialization: string;
    roles: EmployeeRole[]; extraRoleLabels: string[]; allowedIpAddresses: string[];
  }): Promise<EmployeeRegisteredResult> {
    const result = await firstValueFrom(this.http.post<EmployeeRegisteredResult>(`${API_BASE_URL}/employees`, data));
    await Promise.all([this.refresh(), this.refreshPaged()]);
    return result;
  }

  async resendCredentialEmail(employeeId: string): Promise<{ emailSent: boolean; emailError?: string }> {
    return firstValueFrom(this.http.post<{ emailSent: boolean; emailError?: string }>(`${API_BASE_URL}/employees/${employeeId}/resend-credential-email`, {}));
  }

  /**
   * Admin disables an employee's account — e.g. on offboarding. The API
   * revokes all active device sessions and blocks future logins in the
   * same request; historical tickets/maintenance/time-logs are untouched.
   */
  async disableEmployee(id: string, reason: string): Promise<void> {
    await firstValueFrom(this.http.post<Employee>(`${API_BASE_URL}/employees/${id}/disable`, { reason }));
    await Promise.all([this.refresh(), this.refreshPaged()]);
  }

  async enableEmployee(id: string): Promise<void> {
    await firstValueFrom(this.http.post<Employee>(`${API_BASE_URL}/employees/${id}/enable`, {}));
    await Promise.all([this.refresh(), this.refreshPaged()]);
  }

  async addAllowedIp(employeeId: string, ip: string): Promise<void> {
    await firstValueFrom(this.http.post<Employee>(`${API_BASE_URL}/employees/${employeeId}/allowed-ips`, { ipAddress: ip }));
    await Promise.all([this.refresh(), this.refreshPaged()]);
  }

  async removeAllowedIp(employeeId: string, ip: string): Promise<void> {
    await firstValueFrom(this.http.delete<Employee>(`${API_BASE_URL}/employees/${employeeId}/allowed-ips/${encodeURIComponent(ip)}`));
    await Promise.all([this.refresh(), this.refreshPaged()]);
  }

  async devicesFor(employeeId: string): Promise<DeviceSession[]> {
    return firstValueFrom(this.http.get<DeviceSession[]>(`${API_BASE_URL}/employees/${employeeId}/devices`));
  }

  async revokeDevice(deviceSessionId: string): Promise<void> {
    await firstValueFrom(this.http.post(`${API_BASE_URL}/employees/devices/${deviceSessionId}/revoke`, {}));
  }

  async loginHistoryFor(employeeId: string): Promise<LoginRecord[]> {
    return firstValueFrom(this.http.get<LoginRecord[]>(`${API_BASE_URL}/employees/${employeeId}/login-history`));
  }
}
