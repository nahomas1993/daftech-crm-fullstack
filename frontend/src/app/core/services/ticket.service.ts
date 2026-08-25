import { Injectable, signal } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { Ticket, TicketCategory, TicketStatus, TicketPriority, PagedResult, TicketQuote } from '../models';
import { API_BASE_URL } from './api-base';
import { AuthService } from './auth.service';

@Injectable({ providedIn: 'root' })
export class TicketService {
  private readonly _tickets = signal<Ticket[]>([]);
  readonly tickets = this._tickets.asReadonly();

  private readonly _myTickets = signal<Ticket[]>([]);
  readonly myTickets = this._myTickets.asReadonly();

  private readonly _page = signal(1);
  private readonly _pageSize = signal(20);
  private readonly _totalCount = signal(0);
  private readonly _totalPages = signal(0);
  private readonly _pagedTickets = signal<Ticket[]>([]);
  readonly pagedTickets = this._pagedTickets.asReadonly();
  readonly page = this._page.asReadonly();
  readonly pageSize = this._pageSize.asReadonly();
  readonly totalCount = this._totalCount.asReadonly();
  readonly totalPages = this._totalPages.asReadonly();

  constructor(private http: HttpClient, private auth: AuthService) {
    if (this.auth.isStaffAuthenticated()) {
      void this.refresh();
      void this.refreshPaged();
    }
    // NOTE: this only fires once, at app bootstrap, when TicketService
    // (providedIn: 'root') is first constructed — typically before login
    // has happened. It doesn't reliably load data after the fact. Pages
    // that need myTickets data must call refreshMyTickets() themselves in
    // ngOnInit (see MyTicketsComponent, MaintenanceHistoryComponent,
    // DashboardComponent) rather than relying on this constructor check.
  }

  async refresh(): Promise<void> {
    // GET /api/tickets is Admin-only (it returns every ticket in the
    // system, unscoped) — non-admin technicians must rely solely on
    // refreshPaged(), which the backend scopes to their own assigned
    // tickets. Skipping the call here (rather than letting it 403) keeps
    // _tickets() simply empty for non-admins instead of surfacing an
    // error for a call they were never meant to make.
    if (!this.auth.currentEmployee()?.roles.includes('Admin')) return;

    const list = await firstValueFrom(
      this.http.get<Ticket[]>(`${API_BASE_URL}/tickets`)
    );
    this._tickets.set(list);
  }

  async refreshPaged(
    page = this._page(),
    pageSize = this._pageSize()
  ): Promise<void> {
    const params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize);

    const result = await firstValueFrom(
      this.http.get<PagedResult<Ticket>>(
        `${API_BASE_URL}/tickets/paged`,
        { params }
      )
    );

    this._page.set(result.page);
    this._pageSize.set(result.pageSize);
    this._totalCount.set(result.totalCount);
    this._totalPages.set(result.totalPages);
    this._pagedTickets.set(result.items);
  }

  async goToPage(page: number): Promise<void> {
    await this.refreshPaged(page);
  }

  getById(id: string): Ticket | undefined {
    return this._tickets().find(t => t.id === id)
      ?? this._myTickets().find(t => t.id === id);
  }

  async refreshMyTickets(clientId: string): Promise<void> {
    const list = await firstValueFrom(
      this.http.get<Ticket[]>(
        `${API_BASE_URL}/tickets/client/${clientId}`
      )
    );

    this._myTickets.set(list);
  }

  forClient(clientId: string): Ticket[] {
    return this._myTickets()
      .filter(t => t.clientId === clientId)
      .sort((a, b) =>
        b.dateSubmitted.localeCompare(a.dateSubmitted)
      );
  }

  forEmployee(employeeId: string): Ticket[] {
    // _tickets() is Admin-only data (see refresh()) and is empty for
    // non-admins — fall back to pagedTickets(), which for a non-admin
    // caller is already server-scoped to their own assigned tickets, so
    // this still returns the right set for e.g. a technician's own
    // dashboard tiles.
    const source = this._tickets().length > 0 ? this._tickets() : this._pagedTickets();
    return source.filter(t => t.assignedEmployeeId === employeeId);
  }

  awaitingConfirmationForClient(clientId: string): Ticket[] {
    return this._myTickets()
      .filter(
        t =>
          t.clientId === clientId &&
          t.status === 'AwaitingClientConfirmation'
      );
  }

  escalated(): Ticket[] {
    return this._tickets()
      .filter(t => t.status === 'Escalated');
  }

  openTicketCountByEmployee(): Record<string, number> {
    const open: TicketStatus[] = ['Assigned', 'InProgress'];
    const counts: Record<string, number> = {};

    for (const t of this._tickets()) {
      if (
        t.assignedEmployeeId &&
        open.includes(t.status)
      ) {
        counts[t.assignedEmployeeId] =
          (counts[t.assignedEmployeeId] ?? 0) + 1;
      }
    }

    return counts;
  }

  async submitFromClient(
    clientId: string,
    agreementId: string,
    description: string,
    category: TicketCategory,
    failureTypeId?: string,
    voiceNote?: {
      storageKey: string;
      fileName: string;
    },
    supportTypeId?: string,
    acknowledgeChargeable = false
  ): Promise<Ticket> {
    const ticket = await firstValueFrom(
      this.http.post<Ticket>(
        `${API_BASE_URL}/tickets`,
        {
          clientId,
          agreementId,
          description,
          category,
          failureTypeId,
          supportTypeId,
          acknowledgeChargeable,
          voiceNoteStorageKey: voiceNote?.storageKey,
          voiceNoteFileName: voiceNote?.fileName,
        }
      )
    );

    if (this.auth.isStaffAuthenticated()) {
      await Promise.all([
        this.refresh(),
        this.refreshPaged()
      ]);
    }

    await this.refreshMyTickets(clientId);

    return ticket;
  }

  /**
   * Asks the server what this issue would cost before it is submitted. The
   * price is always the server's — we only display what it returns, so a
   * client can't talk the figure down from the browser.
   */
  async quote(
    agreementId: string,
    failureTypeId?: string,
    supportTypeId?: string
  ): Promise<TicketQuote> {
    let params = new HttpParams().set('agreementId', agreementId);
    if (failureTypeId) params = params.set('failureTypeId', failureTypeId);
    if (supportTypeId) params = params.set('supportTypeId', supportTypeId);

    return firstValueFrom(
      this.http.get<TicketQuote>(`${API_BASE_URL}/tickets/quote`, { params })
    );
  }

  async uploadVoiceNote(
    blob: Blob,
    fileName: string
  ): Promise<{
    storageKey: string;
    fileName: string;
  }> {
    const form = new FormData();
    form.append('file', blob, fileName);

    const result = await firstValueFrom(
      this.http.post<{
        storageKey: string;
        fileName: string;
      }>(
        `${API_BASE_URL}/tickets/voice-note`,
        form
      )
    );

    return result;
  }

  async downloadVoiceNote(ticketId: string): Promise<Blob> {
    return firstValueFrom(
      this.http.get(
        `${API_BASE_URL}/tickets/${ticketId}/voice-note`,
        { responseType: 'blob' }
      )
    );
  }

  /** Sets a ticket's priority (Low/Medium/High) — any employee may set this. Feeds workload-aware Trainer assignment's "high-priority tickets" dimension; has no effect on technician auto-assignment. */
  async setPriority(ticketId: string, priority: TicketPriority): Promise<void> {
    await firstValueFrom(this.http.patch<Ticket>(`${API_BASE_URL}/tickets/${ticketId}/priority`, { priority }));
    await this.refresh();
  }

  async updateStatus(
    ticketId: string,
    status: TicketStatus,
    actorName: string
  ): Promise<void> {
    // Send the change against the latest server state, and retry a couple of
    // times on 409 before ever showing an error. A 409 only means "the row
    // moved between the server's read and its write" — it is recoverable and
    // the backend's resolve path is idempotent, so retrying can never
    // double-apply. Between attempts we reload the list so the next PATCH is
    // built from fresh ticket data.
    const maxAttempts = 3;
    let lastError: any = null;

    for (let attempt = 1; attempt <= maxAttempts; attempt++) {
      try {
        await this.patchStatus(ticketId, status, actorName);
        lastError = null;
        break;
      } catch (err: any) {
        lastError = err;

        if (err?.status !== 409 || attempt === maxAttempts) break;

        // Authoritative check: ask the server for THIS ticket instead of
        // hunting for it in a cached/paged/filtered list. A resolved ticket
        // usually leaves the technician's open-ticket page entirely, and the
        // old list lookup then reported "not found" -> "changed by another
        // user" for an update that had actually succeeded.
        if (await this.serverHasReached(ticketId, status)) {
          lastError = null;
          break;
        }

        await this.refreshAll();
      }
    }

    // Always resync from the server, whatever happened, so the list the
    // technician sees (and any follow-up retry) reflects the latest data.
    await this.refreshAll();

    if (lastError) {
      // Final safety net: re-read the single ticket from the API. Only a
      // fresh read that still shows a different status counts as a real
      // conflict; anything else is treated as success so a landed update can
      // never surface a red error.
      if (await this.serverHasReached(ticketId, status)) return;

      throw new Error(TicketService.describeStatusUpdateError(lastError));
    }
  }

  /**
   * Reads one ticket straight from the API (never from a cached list).
   * Returns null when the read itself fails, so callers can tell
   * "definitely not there yet" from "couldn't check".
   */
  private async fetchOne(ticketId: string): Promise<Ticket | null> {
    try {
      return await firstValueFrom(
        this.http.get<Ticket>(`${API_BASE_URL}/tickets/${ticketId}`)
      );
    } catch {
      return null;
    }
  }

  /** True when the server's own copy of the ticket already reflects the requested change. */
  private async serverHasReached(
    ticketId: string,
    status: TicketStatus
  ): Promise<boolean> {
    const fresh = await this.fetchOne(ticketId);
    if (!fresh) return false;

    if (status === 'Resolved') {
      return fresh.status === 'AwaitingClientConfirmation'
        || fresh.status === 'Closed'
        || fresh.status === 'Escalated';
    }

    return fresh.status === status;
  }

  private async refreshAll(): Promise<void> {
    await Promise.all([
      this.refresh(),
      this.refreshPaged()
    ]);
  }

  private async patchStatus(
    ticketId: string,
    status: TicketStatus,
    actorName: string
  ): Promise<void> {
    await firstValueFrom(
      this.http.patch<Ticket>(
        `${API_BASE_URL}/tickets/${ticketId}/status`,
        {
          status,
          actorName
        }
      )
    );
  }

  /**
   * Maps a failed status-update request to a user-facing message by HTTP
   * status code, not by echoing whatever text the server happened to send.
   * This is only ever reached after every retry lost the race AND a fresh
   * read confirmed the change did not land (see updateStatus), so the
   * conflict wording can never appear for a successful update or for an
   * unrelated failure. 404 means the ticket no longer exists; 403 means the
   * caller isn't the assigned technician; anything else is a server error.
   */
  private static describeStatusUpdateError(err: any): string {
    switch (err?.status) {
      case 409:
        return 'Could not save this change right now — the list has been refreshed, please try again.';
      case 404:
        return 'Ticket not found. It may have been removed.';
      case 403:
        return 'You are not the technician assigned to this ticket.';
      case 500:
        return 'Server error. Please try again.';
      default:
        return 'Server error. Please try again.';
    }
  }

  async confirmResolution(
    ticketId: string,
    isFixed: boolean,
    satisfactionStars?: number
  ): Promise<Ticket> {
    const ticket = await firstValueFrom(
      this.http.post<Ticket>(
        `${API_BASE_URL}/tickets/${ticketId}/confirm`,
        {
          isFixed,
          satisfactionStars
        }
      )
    );

    if (this.auth.isStaffAuthenticated()) {
      await Promise.all([
        this.refresh(),
        this.refreshPaged()
      ]);
    } else {
      await this.refreshMyTickets(ticket.clientId);
    }

    return ticket;
  }

  async uploadAttachment(
    ticketId: string,
    file: File
  ): Promise<Ticket> {
    const form = new FormData();
    form.append('file', file, file.name);

    const ticket = await firstValueFrom(
      this.http.post<Ticket>(
        `${API_BASE_URL}/tickets/${ticketId}/attachment`,
        form
      )
    );

    if (this.auth.isStaffAuthenticated()) {
      await Promise.all([
        this.refresh(),
        this.refreshPaged()
      ]);
    } else {
      await this.refreshMyTickets(ticket.clientId);
    }

    return ticket;
  }

  async downloadAttachment(ticketId: string): Promise<Blob> {
    return firstValueFrom(
      this.http.get(
        `${API_BASE_URL}/tickets/${ticketId}/attachment`,
        { responseType: 'blob' }
      )
    );
  }
}
