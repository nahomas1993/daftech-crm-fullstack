using DaftechCrm.Application.DTOs;
using DaftechCrm.Domain.Enums;

namespace DaftechCrm.Application.Interfaces;

public interface IClientService
{
    Task<ClientDto> SubmitSignupAsync(CreateClientSignupRequest request, CancellationToken ct = default);

    /// <summary>Admin registers a client directly — Approved immediately, credentials issued and emailed in the same call.</summary>
    Task<ClientRegisteredResult> RegisterAsync(RegisterClientRequest request, CancellationToken ct = default);

    /// <summary>Retries sending the credential email with a freshly regenerated one-time password (SRS v2.0 §4.3.1).</summary>
    Task<ResendClientCredentialEmailResult> ResendCredentialEmailAsync(Guid clientId, CancellationToken ct = default);

    Task<ClientDto> ApproveAsync(Guid clientId, CancellationToken ct = default);
    Task<ClientDto> RejectAsync(Guid clientId, RejectClientRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<ClientDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Paged variant of <see cref="GetAllAsync"/> for the Clients table UI.</summary>
    Task<PagedResult<ClientDto>> GetAllPagedAsync(PaginationQuery query, CancellationToken ct = default);

    Task<ClientDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ClientDto>> GetPendingAsync(CancellationToken ct = default);

    /// <summary>Updates the plain profile fields. AccountStatus/credentials have their own dedicated endpoints (approve/reject/resend).</summary>
    Task<ClientDto> UpdateAsync(Guid clientId, UpdateClientRequest request, CancellationToken ct = default);

    /// <summary>Soft-deletes the account (removes it from active lists/login) — agreements/tickets/trainings it's referenced by are left intact.</summary>
    Task DeleteAsync(Guid clientId, CancellationToken ct = default);
}

public interface IEmployeeService
{
    /// <summary>Admin registers a staff account — credentials (username + one-time password) are issued, emailed, and returned once.</summary>
    Task<EmployeeRegisteredResult> RegisterAsync(CreateEmployeeRequest request, CancellationToken ct = default);

    /// <summary>Retries sending the credential email with a freshly regenerated one-time password (SRS v2.0 §4.3.1).</summary>
    Task<ResendCredentialEmailResult> ResendCredentialEmailAsync(Guid employeeId, CancellationToken ct = default);
    Task<IReadOnlyList<EmployeeDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Paged variant of <see cref="GetAllAsync"/> for the Employees table UI.</summary>
    Task<PagedResult<EmployeeDto>> GetAllPagedAsync(PaginationQuery query, CancellationToken ct = default);

    Task<EmployeeDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Disables the account, revokes all active device sessions, and blocks future logins. Historical records are untouched.</summary>
    Task<EmployeeDto> DisableAsync(Guid employeeId, DisableEmployeeRequest request, CancellationToken ct = default);
    Task<EmployeeDto> EnableAsync(Guid employeeId, CancellationToken ct = default);

    /// <summary>Updates the plain profile fields (name, email, phone, specialization). Roles/IPs/status have their own dedicated endpoints.</summary>
    Task<EmployeeDto> UpdateAsync(Guid employeeId, UpdateEmployeeRequest request, CancellationToken ct = default);

    /// <summary>
    /// Replaces this employee's full set of responsibilities (Admin,
    /// EmployeeTechnician, Trainer) with the given list — an Admin can add,
    /// remove, or change responsibilities at any time; an employee can
    /// hold Technician and Trainer simultaneously (see EmployeeRole). Does
    /// not touch profile fields, IP allowlist, or account status. Removing
    /// Trainer from an employee currently assigned to an in-progress
    /// TrainingSession does not un-assign them — see AgreementService — an
    /// Admin who wants that must reassign the training separately.
    /// </summary>
    Task<EmployeeDto> SetResponsibilitiesAsync(Guid employeeId, SetEmployeeResponsibilitiesRequest request, CancellationToken ct = default);

    /// <summary>Soft-deletes the account (removes it from active lists/login, revokes sessions) — tickets/time logs/etc. it's referenced by are left intact.</summary>
    Task DeleteAsync(Guid employeeId, CancellationToken ct = default);

    Task<EmployeeDto> AddAllowedIpAsync(Guid employeeId, AddAllowedIpRequest request, CancellationToken ct = default);
    Task<EmployeeDto> RemoveAllowedIpAsync(Guid employeeId, string ip, CancellationToken ct = default);

    Task<IReadOnlyList<DeviceSessionDto>> GetDevicesAsync(Guid employeeId, CancellationToken ct = default);
    Task RevokeDeviceAsync(Guid deviceSessionId, CancellationToken ct = default);
    Task<IReadOnlyList<LoginRecordDto>> GetLoginHistoryAsync(Guid employeeId, CancellationToken ct = default);
}

/// <summary>
/// Login and password-change for both staff and clients. Captures IP/device
/// on every staff login attempt and enforces disabled-account + IP-allow-list
/// rules before authenticating. Also enforces the forced-change-on-first-login
/// rule: while MustChangePassword is true, only ChangePassword succeeds —
/// every other authenticated action should be blocked by the caller (the
/// frontend routes straight to the change-password screen; the API additionally
/// refuses to clear the flag except via a valid current-password match).
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Single entry point for the unified login page. Tries the username
    /// against Employees first, then Clients, and delegates to the
    /// existing, already-hardened LoginEmployeeAsync / LoginClientAsync
    /// logic for whichever one matches — so password checks, account
    /// status checks, OTP expiry, and login-record/session bookkeeping
    /// behave identically to the original two endpoints. A client cannot
    /// influence which branch runs; that's decided purely by which table
    /// the username exists in.
    /// </summary>
    Task<UnifiedLoginResult> LoginAsync(UnifiedLoginRequest request, CancellationToken ct = default);

    Task<EmployeeLoginResult> LoginEmployeeAsync(EmployeeLoginRequest request, CancellationToken ct = default);
    Task ChangeEmployeePasswordAsync(Guid employeeId, ChangePasswordRequest request, CancellationToken ct = default);

    Task<ClientLoginResult> LoginClientAsync(ClientLoginRequest request, CancellationToken ct = default);
    Task ChangeClientPasswordAsync(Guid clientId, ClientChangePasswordRequest request, CancellationToken ct = default);

    /// <summary>Exchanges a valid, unexpired, unrevoked refresh token for a new access/refresh pair. Rotates the refresh token (old one is revoked).</summary>
    Task<AuthTokenResult> RefreshAsync(RefreshTokenRequest request, string ipAddress, CancellationToken ct = default);

    /// <summary>Revokes a single refresh token (logout on one device). Idempotent — revoking an already-revoked or unknown token is not an error.</summary>
    Task RevokeRefreshTokenAsync(RevokeTokenRequest request, string ipAddress, CancellationToken ct = default);
}

/// <summary>
/// "Forgot password" for both staff and clients. There is no self-service
/// reset link — every credential in this system is admin-issued and
/// hand-delivered by email (AccountCredentialService), so a forgotten
/// password just queues a request for an Admin to review and action with a
/// fresh one-time password, the same as onboarding a new hire.
/// </summary>
public interface IPasswordResetService
{
    /// <summary>Anonymous — a requester who forgot their password isn't logged in. Always succeeds from the caller's perspective, even for an unknown username, to avoid username enumeration.</summary>
    Task<PasswordResetRequestSubmittedResult> SubmitAsync(SubmitPasswordResetRequest request, string ipAddress, CancellationToken ct = default);

    Task<IReadOnlyList<PasswordResetRequestDto>> GetPendingAsync(CancellationToken ct = default);
    Task<IReadOnlyList<PasswordResetRequestDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Generates a fresh OTP, emails it, and sets MustChangePassword — mirrors ResendCredentialEmailAsync for a fresh hire.</summary>
    Task<PasswordResetOtpIssuedResult> IssueOtpAsync(Guid requestId, string resolvedByName, CancellationToken ct = default);

    Task<PasswordResetRequestDto> DismissAsync(Guid requestId, string resolvedByName, DismissPasswordResetRequest request, CancellationToken ct = default);
}

/// <summary>
/// Issues and validates JWT access tokens, and manages refresh-token
/// persistence/rotation. Kept separate from IAuthService so the
/// credential-checking logic (passwords, IP allow-lists) stays decoupled
/// from the token mechanics.
/// </summary>
public interface ITokenService
{
    /// <summary>Creates a new access token + refresh token pair and persists the refresh token's hash.</summary>
    Task<IssuedTokenPair> IssueTokenPairAsync(TokenSubject subject, string ipAddress, CancellationToken ct = default);

    /// <summary>
    /// Validates the raw refresh token against stored hashes, rotates it
    /// (revokes the old one, issues a new pair), and returns the new pair.
    /// The account type is read from the stored token record itself — the
    /// caller doesn't need to know it in advance. Throws
    /// InvalidOperationException if the token is missing, expired, already
    /// revoked, or already replaced (possible token-theft reuse).
    /// </summary>
    Task<IssuedTokenPair> RefreshAsync(string rawRefreshToken, string ipAddress, CancellationToken ct = default);

    Task RevokeAsync(string rawRefreshToken, string ipAddress, CancellationToken ct = default);
}

/// <summary>Manages the SystemProduct layer between Client and Agreement — see SystemProduct.</summary>
public interface IAgreementService
{
    /// <summary>
    /// Creates (signs) an agreement for a Client → SystemProduct, under the
    /// given AgreementType. SignDate is admin-entered (not forced to
    /// today). If AgreementTypeId resolves to the Support type, throws
    /// InvalidOperationException unless the same SystemProduct already has
    /// a Training agreement with TrainingSession.EndDate set — training
    /// must finish first, per-SystemProduct. Always inserts a new row —
    /// never updates or replaces an existing agreement, even a prior one
    /// for the same SystemProduct/AgreementType.
    /// </summary>
    Task<AgreementDto> CreateAsync(CreateAgreementRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<AgreementDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Paged variant of <see cref="GetAllAsync"/> for the Agreements table UI.</summary>
    Task<PagedResult<AgreementDto>> GetAllPagedAsync(PaginationQuery query, CancellationToken ct = default);

    Task<IReadOnlyList<AgreementDto>> GetForClientAsync(Guid clientId, CancellationToken ct = default);
    Task<IReadOnlyList<AgreementDto>> GetForSystemProductAsync(Guid systemProductId, CancellationToken ct = default);
    Task<IReadOnlyList<AgreementDto>> GetExpiringSoonAsync(CancellationToken ct = default);
    Task<AgreementDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>True if the given SystemProduct has a Training agreement with EndDate set — the precondition for signing a Support agreement for that SAME system/product. Lets the UI disable/explain the "New Agreement" action before the user even tries.</summary>
    Task<bool> SystemProductHasCompletedTrainingAsync(Guid systemProductId, CancellationToken ct = default);

    /// <summary>
    /// Uploads and attaches a scanned document to the agreement. If the
    /// agreement already has a file attached, the old one is deleted after
    /// the new one is successfully saved (never before — a failed upload
    /// should never destroy the existing file).
    /// </summary>
    Task<AgreementDto> UploadScannedFileAsync(Guid agreementId, Stream content, string fileName, string contentType, CancellationToken ct = default);

    /// <summary>Retrieves the agreement's attached scanned file, or null if none is attached or the agreement doesn't exist.</summary>
    Task<RetrievedFile?> DownloadScannedFileAsync(Guid agreementId, CancellationToken ct = default);

    /// <summary>The TrainingSession for a Training-type agreement, or null if the agreement isn't a Training agreement or doesn't exist.</summary>
    Task<TrainingSessionDto?> GetTrainingSessionAsync(Guid agreementId, CancellationToken ct = default);

    /// <summary>Sets/updates the TrainingSession fields for a Training-type agreement (schedule, attendance, topics, etc — NOT the trainer roster or CompletionStatus, which have their own dedicated methods below). All fields optional/incremental. Throws if the agreement isn't a Training agreement.</summary>
    Task<TrainingSessionDto> SaveTrainingSessionAsync(Guid agreementId, SaveTrainingSessionRequest request, CancellationToken ct = default);

    /// <summary>Manually adds one more Trainer to a Training agreement's session, on top of whatever auto-assignment already placed there. Throws if the employee doesn't hold the Trainer responsibility, or is already assigned to this session.</summary>
    Task<TrainingSessionDto> AddTrainingAssignmentAsync(Guid agreementId, Guid trainerEmployeeId, CancellationToken ct = default);

    /// <summary>Removes a Trainer from a Training agreement's session. Throws if the assignment has already been Approved — an approved assignment is part of the completed training record and shouldn't be silently dropped.</summary>
    Task<TrainingSessionDto> RemoveTrainingAssignmentAsync(Guid agreementId, Guid assignmentId, CancellationToken ct = default);

    /// <summary>
    /// The trainer submits their own assignment for Admin review: work
    /// description plus (optionally, via UploadTrainingAssignmentFileAsync)
    /// an evidence file. Moves the assignment from Assigned or
    /// RejectedNeedsRework to Submitted. Throws if the assignment is
    /// already Submitted or Approved, or if callerEmployeeId isn't the
    /// trainer on this assignment.
    /// </summary>
    Task<TrainingAssignmentDto> SubmitTrainingAssignmentAsync(Guid assignmentId, Guid callerEmployeeId, SubmitTrainingAssignmentRequest request, CancellationToken ct = default);

    /// <summary>Uploads (or replaces) the trainer's evidence file for their own assignment. Can be called before or after Submit — re-uploading after a rejection doesn't itself resubmit; call SubmitTrainingAssignmentAsync again for that.</summary>
    Task<TrainingAssignmentDto> UploadTrainingAssignmentFileAsync(Guid assignmentId, Guid callerEmployeeId, Stream content, string fileName, string contentType, CancellationToken ct = default);

    /// <summary>Retrieves a trainer's uploaded evidence file for one assignment, or null if none is attached or the assignment doesn't exist.</summary>
    Task<RetrievedFile?> DownloadTrainingAssignmentFileAsync(Guid assignmentId, CancellationToken ct = default);

    /// <summary>
    /// Admin reviews a Submitted assignment. Approving sets Status=Approved;
    /// once every assignment on the session is Approved, the session's own
    /// CompletionStatus is automatically advanced to Completed (and EndDate
    /// set to today if not already set) — this is what unlocks signing a
    /// Support agreement for the same system/product. Rejecting sets
    /// Status=RejectedNeedsRework and leaves the session's CompletionStatus
    /// untouched, so the trainer can revise and resubmit. Throws if the
    /// assignment isn't currently Submitted.
    /// </summary>
    Task<TrainingSessionDto> ReviewTrainingAssignmentAsync(Guid assignmentId, ReviewTrainingAssignmentRequest request, string reviewedByName, CancellationToken ct = default);

    /// <summary>Every TrainingAssignment currently held by the given Trainer, across all Training agreements — the "My Trainings" list for that employee. Newest-assigned first.</summary>
    Task<IReadOnlyList<TrainingAssignmentDto>> GetAssignmentsForTrainerAsync(Guid trainerEmployeeId, CancellationToken ct = default);

    /// <summary>Uploads/replaces the scanned document (e.g. sign-in sheet) for a Training agreement's session.</summary>
    Task<TrainingSessionDto> UploadTrainingScanAsync(Guid agreementId, Stream content, string fileName, string contentType, CancellationToken ct = default);

    /// <summary>Retrieves a training session's attached scan, or null if none is attached or the agreement doesn't exist.</summary>
    Task<RetrievedFile?> DownloadTrainingScanAsync(Guid agreementId, CancellationToken ct = default);
}

/// <summary>Manages the SystemProduct layer between Client and Agreement — see SystemProduct.</summary>
public interface ISystemProductService
{
    /// <summary>Creates a new system/product for a client. Never overwrites or replaces an existing one — a client accumulates as many as it has.</summary>
    Task<SystemProductDto> CreateAsync(CreateSystemProductRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<SystemProductDto>> GetForClientAsync(Guid clientId, CancellationToken ct = default);
    Task<SystemProductDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<SystemProductDto> UpdateAsync(Guid id, UpdateSystemProductRequest request, CancellationToken ct = default);

    /// <summary>Soft-deletes — agreements under this system/product are left intact for history.</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

/// <summary>Manages the admin-editable AgreementType lookup (Support/Training always present — see AgreementTypeNames — plus any custom types an Admin adds).</summary>
/// <summary>
/// Workload-aware Trainer assignment — surfaces every eligible Trainer's
/// current workload (open/pending/high-priority/overdue tickets, plus
/// existing training assignments) and recommends the one with the most
/// reasonable workload, without enforcing that choice. See
/// TrainerWorkloadDtos.cs for the exact fields and weighting rationale.
/// </summary>
public interface ITrainerWorkloadService
{
    /// <summary>
    /// Every Employee currently holding the Trainer responsibility (see
    /// EmployeeRole.Trainer), each with their current workload snapshot,
    /// plus which one the system recommends (the lowest WorkloadScore).
    /// Disabled employees are excluded even if they still have the
    /// Trainer role — they can't be assigned regardless. Returns an empty
    /// EligibleTrainers list (RecommendedTrainerEmployeeId = null) if no
    /// active employee currently holds the Trainer responsibility.
    /// </summary>
    Task<TrainerAssignmentRecommendationDto> GetEligibleTrainersAsync(CancellationToken ct = default);

    /// <summary>
    /// Picks up to <paramref name="count"/> distinct Trainers for a new
    /// TrainingSession, ordered by the same WorkloadScore as
    /// GetEligibleTrainersAsync (lowest/least-loaded first) — the
    /// auto-assignment counterpart to that recommend-only call. Returns
    /// fewer than <paramref name="count"/> if there aren't enough eligible
    /// Trainers (never throws for a short supply); returns an empty list
    /// if none are eligible at all. Each pick is computed against a
    /// snapshot taken once at the start of the call, so picking trainer 2
    /// isn't affected by trainer 1 having just been notionally assigned
    /// within this same call — ActiveTrainingAssignmentCount only reflects
    /// commitments that existed in the database before this call started.
    /// </summary>
    Task<IReadOnlyList<Guid>> SelectTrainersForAssignmentAsync(int count, CancellationToken ct = default);
}

public interface IAgreementTypeService
{
    Task<IReadOnlyList<AgreementTypeDto>> GetAllAsync(CancellationToken ct = default);
    Task<AgreementTypeDto> CreateAsync(CreateAgreementTypeRequest request, CancellationToken ct = default);
    Task<AgreementTypeDto> UpdateAsync(Guid id, UpdateAgreementTypeRequest request, CancellationToken ct = default);

    /// <summary>Throws if the type is system-defined (Support/Training) or still has agreements referencing it.</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// Table-only reports for the Reports module (see TicketReportFilter and
/// the six *ReportRow DTOs) — deliberately separate from IReportService,
/// which backs the Dashboard's charts/KPIs. This split mirrors the
/// product requirement that Reports = tables and Dashboard = charts/KPIs,
/// and keeps the two from silently drifting onto shared query logic that
/// would make the split hard to enforce later.
/// </summary>
public interface ITicketReportService
{
    Task<TableReportResult<CustomerSupportReportRow>> GetCustomerSupportReportAsync(TicketReportFilter filter, PaginationQuery paging, CancellationToken ct = default);
    Task<TableReportResult<EmployeePerformanceReportRow>> GetEmployeePerformanceReportAsync(TicketReportFilter filter, PaginationQuery paging, CancellationToken ct = default);
    Task<TableReportResult<RegionalReportRow>> GetRegionalReportAsync(TicketReportFilter filter, PaginationQuery paging, CancellationToken ct = default);
    Task<TableReportResult<FailureTypeReportRow>> GetFailureTypeReportAsync(TicketReportFilter filter, PaginationQuery paging, CancellationToken ct = default);
    Task<TableReportResult<ResolutionTimeReportRow>> GetResolutionTimeReportAsync(TicketReportFilter filter, PaginationQuery paging, CancellationToken ct = default);
    Task<TableReportResult<CustomerRatingReportRow>> GetCustomerRatingReportAsync(TicketReportFilter filter, PaginationQuery paging, CancellationToken ct = default);

    /// <summary>Renders any one of the six reports as a PDF, given the same filter/rows the table view would show. reportType selects which of the six (case-insensitive: "customer-support", "employee-performance", "regional", "failure-type", "resolution-time", "customer-rating").</summary>
    Task<byte[]> ExportPdfAsync(string reportType, TicketReportFilter filter, CancellationToken ct = default);

    /// <summary>Renders any one of the six reports as CSV — the same filtered/unpaged row set as ExportPdfAsync, for spreadsheet use.</summary>
    Task<string> ExportCsvAsync(string reportType, TicketReportFilter filter, CancellationToken ct = default);
}

public interface IMaintenanceService
{
    Task<MaintenanceRecordDto> CreateAsync(CreateMaintenanceRecordRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<MaintenanceRecordDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Paged variant of <see cref="GetAllAsync"/> for the Maintenance table UI.</summary>
    Task<PagedResult<MaintenanceRecordDto>> GetAllPagedAsync(PaginationQuery query, CancellationToken ct = default);
}

public interface ITimeLogService
{
    Task ClockInAsync(Guid employeeId, CancellationToken ct = default);
    Task ClockOutAsync(Guid employeeId, CancellationToken ct = default);
    Task<IReadOnlyList<TimeLogDto>> GetAllAsync(Guid? employeeId = null, CancellationToken ct = default);

    /// <summary>Paged variant of <see cref="GetAllAsync"/> for the Time Tracking table UI.</summary>
    Task<PagedResult<TimeLogDto>> GetAllPagedAsync(Guid? employeeId, PaginationQuery query, CancellationToken ct = default);
}

public interface INotificationService
{
    Task NotifyAsync(NotificationRecipientType recipientType, string recipientId, string eventType, string message, CancellationToken ct = default);
    Task<IReadOnlyList<NotificationDto>> GetForRecipientAsync(NotificationRecipientType recipientType, string recipientId, CancellationToken ct = default);
    /// <returns>false if the notification doesn't exist or doesn't belong to the caller — the controller maps that to a 404, same as a genuinely missing id, so a caller probing other accounts' notification ids can't distinguish "not mine" from "doesn't exist."</returns>
    Task<bool> MarkReadAsync(Guid notificationId, SessionAccountType callerType, Guid callerId, bool callerIsAdmin, CancellationToken ct = default);
    Task MarkAllReadAsync(NotificationRecipientType recipientType, string recipientId, CancellationToken ct = default);
}

public interface IReportService
{
    /// <summary>On-time vs late resolution stats, overall and per employee, for the Reports bar/donut charts.</summary>
    Task<OnTimeReportDto> GetOnTimeResolutionReportAsync(CancellationToken ct = default);

    /// <summary>
    /// Written/graphical performance metrics for one employee, with an
    /// optional AI-generated narrative summary attached (SRS v2.0 §4.10).
    /// The narrative is always best-effort — see IAiNarrativeReportService.
    /// </summary>
    Task<EmployeePerformanceReportDto> GetEmployeePerformanceReportAsync(Guid employeeId, bool includeAiNarrative, CancellationToken ct = default);

    /// <summary>
    /// AI narrative summary for any report table already built and shown
    /// on the Reports page. Always best-effort — see
    /// IAiNarrativeReportService for the degrade-gracefully contract.
    /// </summary>
    Task<AiPerformanceSummaryResult> SummarizeTabularReportAsync(TabularReportData data, CancellationToken ct = default);

    /// <summary>System-wide snapshot for the admin Reports page's "Overall Operations" pie chart — every ticket by current status, plus headline counts.</summary>
    Task<OperationsOverviewDto> GetOperationsOverviewAsync(CancellationToken ct = default);

    /// <summary>
    /// Everything the Dashboard's charts/KPI cards need in one call — bar
    /// charts (region, failure type, employee performance), donut charts
    /// (ticket status, rating distribution), line chart (monthly tickets/
    /// resolved/on-time rate), and KPI cards. All computed live from
    /// current ticket data and scoped by the given filter. Deliberately
    /// separate from ITicketReportService (the Reports module's six
    /// table-only reports) per the product's Reports-vs-Dashboard split.
    /// </summary>
    Task<DashboardDataDto> GetDashboardDataAsync(DashboardFilter filter, CancellationToken ct = default);
}

public interface ISatisfactionSurveyService
{
    /// <summary>Submits the optional 5-question survey. One per ticket — resubmitting overwrites the previous answers.</summary>
    Task<SatisfactionSurveyDto> SubmitAsync(SubmitSatisfactionSurveyRequest request, CancellationToken ct = default);
    Task<SatisfactionSurveyDto?> GetForTicketAsync(Guid ticketId, CancellationToken ct = default);
    Task<IReadOnlyList<SatisfactionSurveyDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Paged variant of <see cref="GetAllAsync"/> for the Satisfaction Surveys table UI.</summary>
    Task<PagedResult<SatisfactionSurveyDto>> GetAllPagedAsync(PaginationQuery query, CancellationToken ct = default);
}

/// <summary>
/// SRS v2.0 §4.8: session/presence tracking. Every login opens a session;
/// a heartbeat ping from the frontend keeps it "online" and updates
/// LastSeen; logging out (or a background sweep after inactivity) marks it
/// offline. Works across both Employee and Client accounts.
/// </summary>
public interface ISessionService
{
    Task<Guid> OpenSessionAsync(SessionAccountType accountType, Guid accountId, string ipAddress, CancellationToken ct = default);
    Task CloseSessionAsync(SessionAccountType accountType, Guid accountId, CancellationToken ct = default);

    /// <summary>Heartbeat — called periodically by the frontend while a user is active. Updates LastSeen and flips OnlineStatus back on if it had lapsed.</summary>
    Task TouchAsync(SessionAccountType accountType, Guid accountId, CancellationToken ct = default);

    /// <summary>Marks any session with no heartbeat within the configured window as offline. Intended for a background sweep.</summary>
    Task<int> MarkStaleSessionsOfflineAsync(CancellationToken ct = default);

    /// <summary>Admin's Session Activity page — current status, last-seen, and most recent IP per account.</summary>
    Task<IReadOnlyList<SessionActivityDto>> GetSessionActivityAsync(CancellationToken ct = default);

    Task<IReadOnlyList<LoginSessionDto>> GetHistoryForAccountAsync(SessionAccountType accountType, Guid accountId, CancellationToken ct = default);
}

/// <summary>
/// Admin-editable system configuration (the Settings → Configuration page).
/// Backed by SystemSetting rows in the DB, overlaid on top of the
/// appsettings.json defaults — a setting only has a DB row once an Admin
/// has actually changed it. Other services (TicketService, ReportService,
/// SessionService, etc.) call the typed Get*Async accessors instead of
/// reading IOptions directly, so a change here takes effect immediately
/// without a redeploy.
/// </summary>
public interface ISystemConfigurationService
{
    /// <summary>All settings across every category, each filled in with its current effective value (DB override if present, else the appsettings.json default).</summary>
    Task<IReadOnlyList<SystemSettingDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Saves one or more settings in a single request. Unknown keys are rejected.</summary>
    Task<IReadOnlyList<SystemSettingDto>> UpdateAsync(UpdateSystemSettingsRequest request, string updatedByName, CancellationToken ct = default);

    Task<int> GetIntAsync(string key, CancellationToken ct = default);
    Task<bool> GetBoolAsync(string key, CancellationToken ct = default);
}

/// <summary>
/// Admin-managed dropdown/checklist options: Region / City / Woreda
/// (client forms), Specialization and CustomRole (employee form). Five
/// independent flat lists — not a hierarchy.
/// </summary>
public interface ILocationService
{
    /// <summary>All five lists at once, alphabetized. Public — the self-signup portal is unauthenticated.</summary>
    Task<LocationOptionsDto> GetAllAsync(CancellationToken ct = default);

    Task<LocationEntryDto> CreateAsync(CreateLocationEntryRequest request, CancellationToken ct = default);
    Task<LocationEntryDto> UpdateAsync(Guid id, UpdateLocationEntryRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// Admin-managed list of client-system failure types, each with an
/// expected resolution duration (hours/days/months). Clients pick one on
/// ticket submission; the on-time/late report uses that ticket's duration
/// instead of the global OnTimeResolutionTargetDays when set.
/// </summary>
public interface IFailureTypeService
{
    Task<IReadOnlyList<FailureTypeDto>> GetAllAsync(CancellationToken ct = default);
    Task<FailureTypeDto> CreateAsync(CreateFailureTypeRequest request, CancellationToken ct = default);
    Task<FailureTypeDto> UpdateAsync(Guid id, UpdateFailureTypeRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
