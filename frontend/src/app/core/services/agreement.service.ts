import { Injectable, signal } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { Agreement, BillingTier, PagedResult, TrainingSession, TrainingCompletionStatus, TrainerAssignmentRecommendation } from '../models';
import { API_BASE_URL } from './api-base';
import { AuthService } from './auth.service';

@Injectable({ providedIn: 'root' })
export class AgreementService {
  private readonly _agreements = signal<Agreement[]>([]);
  readonly agreements = this._agreements.asReadonly();

  // A logged-in client's own agreements, fetched via the client-scoped
  // endpoint. Kept separate from _agreements (the staff-only full list,
  // which 403s for a client token) so the portal never depends on that
  // call succeeding.
  private readonly _myAgreements = signal<Agreement[]>([]);
  readonly myAgreements = this._myAgreements.asReadonly();

  // Paged state for the Agreements table. Kept separate from the full-list
  // cache above, which forClient()/expiringSoon() still rely on.
  private readonly _page = signal(1);
  private readonly _pageSize = signal(20);
  private readonly _totalCount = signal(0);
  private readonly _totalPages = signal(0);
  private readonly _pagedAgreements = signal<Agreement[]>([]);
  readonly pagedAgreements = this._pagedAgreements.asReadonly();
  readonly page = this._page.asReadonly();
  readonly pageSize = this._pageSize.asReadonly();
  readonly totalCount = this._totalCount.asReadonly();
  readonly totalPages = this._totalPages.asReadonly();

  constructor(private http: HttpClient, private auth: AuthService) {
    // /agreements and /agreements/paged are staff-only (AnyEmployee) on the
    // API — calling them with a client token always 403s. Only auto-fetch
    // the full list for a logged-in employee; the client portal fetches
    // its own agreements explicitly via refreshMyAgreements() instead.
    if (this.auth.isStaffAuthenticated()) {
      void this.refresh();
      void this.refreshPaged();
    }
  }

  async refresh(): Promise<void> {
    const list = await firstValueFrom(this.http.get<Agreement[]>(`${API_BASE_URL}/agreements`));
    this._agreements.set(list);
  }

  async refreshPaged(page = this._page(), pageSize = this._pageSize()): Promise<void> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    const result = await firstValueFrom(
      this.http.get<PagedResult<Agreement>>(`${API_BASE_URL}/agreements/paged`, { params })
    );
    this._page.set(result.page);
    this._pageSize.set(result.pageSize);
    this._totalCount.set(result.totalCount);
    this._totalPages.set(result.totalPages);
    this._pagedAgreements.set(result.items);
  }

  async goToPage(page: number): Promise<void> {
    await this.refreshPaged(page);
  }

  getById(id: string): Agreement | undefined {
    return this._agreements().find(a => a.id === id) ?? this._myAgreements().find(a => a.id === id);
  }

  /** Fetches a single agreement directly from the API — use when it may not be in either cached list yet (e.g. right after creating it and navigating straight to its detail page). */
  async fetchById(id: string): Promise<Agreement> {
    return firstValueFrom(this.http.get<Agreement>(`${API_BASE_URL}/agreements/${id}`));
  }

  /**
   * Fetches the logged-in client's own agreements via the client-scoped
   * API endpoint (GET /agreements/client/{id}), which any authenticated
   * client may call. Call this from the client portal instead of relying
   * on forClient() over the staff-only full list.
   */
  async refreshMyAgreements(clientId: string): Promise<void> {
    const list = await firstValueFrom(this.http.get<Agreement[]>(`${API_BASE_URL}/agreements/client/${clientId}`));
    this._myAgreements.set(list);
  }

  /** Client-side filter over myAgreements() — call refreshMyAgreements() first to populate it. Intended for the client portal. */
  forClient(clientId: string): Agreement[] {
    return this._myAgreements().filter(a => a.clientId === clientId);
  }

  /**
   * Client-side filter over the staff-only full agreements list
   * (agreements()/refresh() above) — use this from staff pages like Client
   * Detail, where the current user is an Employee, not the Client.
   */
  forClientStaffView(clientId: string): Agreement[] {
    return this._agreements().filter(a => a.clientId === clientId);
  }

  /** All agreements for one system/product, straight from the API (not filtered from a cached list — the System/Product detail page uses this directly). */
  async fetchForSystemProduct(systemProductId: string): Promise<Agreement[]> {
    return firstValueFrom(this.http.get<Agreement[]>(`${API_BASE_URL}/agreements/system-product/${systemProductId}`));
  }

  /** Agreements expiring within 30 days, or already past expiry. */
  expiringSoon(): Agreement[] {
    const now = Date.now();
    const in30 = now + 30 * 24 * 3_600_000;
    return this._agreements().filter(a => new Date(a.expiryDate).getTime() <= in30);
  }

  /** Client-side mirror of the server's Agreement.IsWithinSupportWindow — used for optimistic UI only; the server is the source of truth for Chargeable. */
  isWithinSupportWindow(agreement: Agreement, atDate: Date = new Date()): boolean {
    const start = new Date(agreement.signDate);
    const windowEnd = new Date(start);
    windowEnd.setMonth(windowEnd.getMonth() + agreement.supportWindowMonths);
    return atDate >= start && atDate <= windowEnd;
  }

  /** Whether the given system/product has a Training agreement with an End Date set — the precondition for signing a Support agreement for that SAME system/product. Check this before showing/enabling "New Support Agreement". */
  async systemProductHasCompletedTraining(systemProductId: string): Promise<boolean> {
    return firstValueFrom(this.http.get<boolean>(`${API_BASE_URL}/agreements/system-product/${systemProductId}/training-complete`));
  }

  /**
   * Creates (signs) an agreement for a client's system/product, under the
   * given agreement type. signDate is admin-entered (defaults to today at
   * the call site, but the admin can back-date it). Rejected with 409 if
   * agreementTypeId resolves to Support but the same system/product has no
   * completed Training agreement yet — callers should catch that and show
   * a clear message. Never overwrites an existing agreement — always
   * creates a new row.
   */
  async createAgreement(data: {
    systemProductId: string; agreementTypeId: string; agreementPlace: string; signDate: string;
    expiryDate?: string; supportWindowMonths: number; billingTier: BillingTier; details?: string;
  }): Promise<Agreement> {
    const agreement = await firstValueFrom(this.http.post<Agreement>(`${API_BASE_URL}/agreements`, data));
    await Promise.all([this.refresh(), this.refreshPaged()]);
    return agreement;
  }

  /**
   * Uploads (or replaces) the scanned document for an already-created
   * agreement. Real multipart upload to the API's file storage.
   */
  async uploadScannedFile(agreementId: string, file: File): Promise<Agreement> {
    const form = new FormData();
    form.append('file', file, file.name);
    const updated = await firstValueFrom(
      this.http.post<Agreement>(`${API_BASE_URL}/agreements/${agreementId}/scanned-file`, form)
    );
    await Promise.all([this.refresh(), this.refreshPaged()]);
    return updated;
  }

  /** URL the browser can navigate to / open in a new tab to download the scanned document. The auth interceptor attaches the bearer token automatically for same-origin API calls; for a plain <a> tag use downloadScannedFile() instead. */
  scannedFileUrl(agreementId: string): string {
    return `${API_BASE_URL}/agreements/${agreementId}/scanned-file`;
  }

  /** Fetches the scanned document as a Blob (so it can be opened via an object URL) — needed because direct <a href> downloads wouldn't carry the Authorization header. */
  async downloadScannedFile(agreementId: string): Promise<Blob> {
    return firstValueFrom(
      this.http.get(`${API_BASE_URL}/agreements/${agreementId}/scanned-file`, { responseType: 'blob' })
    );
  }

  /** Every eligible Trainer's current workload plus a recommendation — shown to Admin when assigning a Training agreement's trainer. See TrainingSessionDetailComponent. */
  async getTrainerWorkload(): Promise<TrainerAssignmentRecommendation> {
    return firstValueFrom(this.http.get<TrainerAssignmentRecommendation>(`${API_BASE_URL}/agreements/trainer-workload`));
  }

  /** The training session for a Training-type agreement. Reachable the same way from the Client, SystemProduct, and Agreement detail pages — all call this by agreement id. Throws (404) if the agreement isn't a Training agreement. */
  async getTrainingSession(agreementId: string): Promise<TrainingSession> {
    return firstValueFrom(this.http.get<TrainingSession>(`${API_BASE_URL}/agreements/${agreementId}/training-session`));
  }

  /** Sets/updates the training session's fields — trainer, dates, location, participants, attendance, topics, issues, trainer comments, client rep confirmation, completion status, follow-up. All fields optional/incremental. */
  async saveTrainingSession(agreementId: string, data: {
    trainerEmployeeId?: string; startDate?: string; endDate?: string; location?: string;
    participants?: string; attendance?: string; topicsCovered?: string; issuesOrQuestions?: string;
    trainerComments?: string; clientRepresentativeConfirmation?: string; clientRepresentativeComments?: string;
    completionStatus: TrainingCompletionStatus; followUpRequired: boolean; followUpNotes?: string;
  }): Promise<TrainingSession> {
    return firstValueFrom(this.http.put<TrainingSession>(`${API_BASE_URL}/agreements/${agreementId}/training-session`, data));
  }

  /** Uploads (or replaces) the scanned document (e.g. sign-in sheet) for a training session. */
  async uploadTrainingScan(agreementId: string, file: File): Promise<TrainingSession> {
    const form = new FormData();
    form.append('file', file, file.name);
    return firstValueFrom(
      this.http.post<TrainingSession>(`${API_BASE_URL}/agreements/${agreementId}/training-session/scan`, form)
    );
  }

  /** Fetches a training session's scan as a Blob — same reasoning as downloadScannedFile above. */
  async downloadTrainingScan(agreementId: string): Promise<Blob> {
    return firstValueFrom(
      this.http.get(`${API_BASE_URL}/agreements/${agreementId}/training-session/scan`, { responseType: 'blob' })
    );
  }
}
