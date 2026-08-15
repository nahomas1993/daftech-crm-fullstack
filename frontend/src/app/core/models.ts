export type AccountStatus = 'Pending' | 'Approved' | 'Rejected';
export type EmployeeAccountStatus = 'Active' | 'Disabled';
export type AgreementStatus = 'Active' | 'Expired' | 'Pending';
export type BillingTier = 'Basic' | 'Intermediate' | 'Advanced';

/** Matches Domain.Enums.TicketCategory — note SqlDatabaseError, not the slash-form display string. */
export type TicketCategory = 'SqlDatabaseError' | 'Bug' | 'Other';

/**
 * Matches Domain.Enums.TicketStatus. Assignment is automatic (no manual
 * "assign" step from the Admin) and Resolved no longer means done — it
 * routes through AwaitingClientConfirmation before Closed or Escalated.
 */
export type TicketStatus =
  | 'Submitted'
  | 'Forwarded'
  | 'Assigned'
  | 'InProgress'
  | 'Resolved'
  | 'AwaitingClientConfirmation'
  | 'Escalated'
  | 'Closed';

export type ClosureReason = 'ClientConfirmedSatisfied' | 'AutoClosedNoResponse';

export type MaintenanceCategory =
  | 'SQL/Database error'
  | 'Front-end error'
  | 'Back-end/server error'
  | 'Security patch'
  | 'Performance update'
  | string;
export type MaintenanceStatus = 'Resolved' | 'InProgress' | 'Recurring';

/**
 * Matches Domain.Enums.EmployeeRole — API uses ItSupport/EmployeeTechnician
 * (no slash/space). ItSupport is retired (Admin absorbs that scope; see
 * TicketService.SubmitFromClientAsync on the backend) and is never
 * assigned to new employees — kept in this union only so an existing
 * employee record that still has it deserializes/displays without error.
 */
export type EmployeeRole = 'Admin' | 'ItSupport' | 'EmployeeTechnician';

export type DeviceType = 'Laptop' | 'Pc' | 'Tablet' | 'Other';
export type DeviceAccessStatus = 'Allowed' | 'Revoked';
export type NotificationRecipientType = 'Admin' | 'ItSupport' | 'Employee' | 'Client';

/** Display helpers — the API uses PascalCase enum names without spaces/slashes; these map back to the spec's human-readable labels. */
export const TICKET_CATEGORY_LABELS: Record<TicketCategory, string> = {
  SqlDatabaseError: 'SQL/Database error',
  Bug: 'Bug',
  Other: 'Other',
};

export const EMPLOYEE_ROLE_LABELS: Record<EmployeeRole, string> = {
  Admin: 'Admin',
  ItSupport: 'IT Support',
  EmployeeTechnician: 'Employee/Technician',
};

export interface Client {
  id: string;
  name: string;
  idNumber: string;
  phoneNumber: string;
  email: string;
  office: string;
  location: string;
  region?: string;
  city?: string;
  woreda?: string;
  kycType: string;
  kycContact: string;
  itSupportContact?: string;
  accountStatus: AccountStatus;
  onboardingDate: string; // ISO date
  rejectionReason?: string;
  username?: string;
  mustChangePassword: boolean;
  /** Permanent display id — "DAF-CLI-####". Set once at creation, never changes. See AccountReferenceIdService. */
  accountRefId: string;
}

/** Returned once, immediately after Admin registers a new client. Never retrievable again after this response. */
export interface ClientRegisteredResult {
  client: Client;
  username: string;
  oneTimePassword: string;
  emailSent: boolean;
  emailError?: string;
}

export interface AgreementTraining {
  id: string;
  clientId: string;
  /** Null until a support agreement is signed for this client — training is recorded before any agreement exists. */
  agreementId: string | null;
  description?: string;
  startDate?: string; // ISO date
  /** Set once training finishes — this is what "training complete" means. Stays editable afterward (e.g. to push it out if training runs long). */
  endDate?: string; // ISO date
  scanFileName?: string;
}

export interface Agreement {
  id: string;
  clientId: string;
  documentNumber: string;
  scannedFileUrl?: string;
  agreementPlace: string;
  /** Admin-entered: the date the agreement was signed. Creating an agreement IS the signing act — the server always sets this to today and rejects creation unless the client has a completed training. */
  signDate: string; // ISO date
  expiryDate: string; // ISO date
  supportWindowMonths: number;
  status: AgreementStatus;
  billingTier: BillingTier;
  /** The client's trainings that were completed as of signing — not the client's full training history (see AgreementService.getTrainingsForClient for that). */
  trainings: AgreementTraining[];
}

export interface Ticket {
  id: string;
  clientId: string;
  clientName: string;
  agreementId: string;
  description: string;
  category: TicketCategory;
  failureTypeId?: string;
  failureTypeName?: string;
  dateSubmitted: string; // ISO datetime
  forwardedByEmployeeId?: string;
  assignedEmployeeId?: string;
  assignedEmployeeName?: string;
  assignedAt?: string; // ISO datetime — timer start; SLA deadline is assignedAt + failure type duration
  /** assignedAt + the ticket's failure type duration. Undefined until assigned, or if no failure type was chosen. */
  expectedResolutionBy?: string; // ISO datetime
  chargeable: boolean;
  status: TicketStatus;
  resolvedAt?: string;
  clientConfirmationDeadline?: string;
  satisfactionStars?: number; // 1-5, set once the client confirms
  satisfactionScore?: number; // stars * 20, out of 100
  closureReason?: ClosureReason;
  /** Original filename of the optional attachment (screenshot/document), or undefined if none was uploaded. Fetch/upload via TicketService's attachment methods — this field is display-only. */
  attachmentFileName?: string;
  /** Original filename of the optional voice-note recording, or undefined if none was recorded. Fetch/upload via TicketService's voice note methods — this field is display-only. */
  voiceNoteFileName?: string;
  auditTrail: TicketAuditEntry[];
}

export interface TicketAuditEntry {
  timestamp: string;
  actor: string;
  action: string;
}

/**
 * A single login event captured for an employee — this is how the
 * "employee IP address at login" requirement is recorded, distinct
 * from the longer-lived DeviceSession allow/revoke record below.
 */
export interface LoginRecord {
  id: string;
  employeeId: string;
  timestamp: string; // ISO datetime
  ipAddress: string;
  deviceType: DeviceType;
  deviceIdentifier: string;
  allowed: boolean; // false if blocked by IP allow-list or disabled account
  reason?: string; // populated when allowed = false
}

export interface DeviceSession {
  id: string;
  employeeId: string;
  deviceType: DeviceType;
  deviceIdentifier: string;
  ipAddress: string;
  lastSeen: string; // ISO datetime
  accessStatus: DeviceAccessStatus;
}

export interface Employee {
  id: string;
  fullName: string;
  email: string;
  phoneNumber: string;
  specialization: string;
  roles: EmployeeRole[];
  /** Additional, purely-descriptive role labels an Admin has defined and assigned — carry no permission meaning. */
  extraRoleLabels: string[];
  accountStatus: EmployeeAccountStatus;
  allowedIpAddresses: string[]; // empty = no IP restriction
  disabledAt?: string;
  disabledReason?: string;
  openTicketCount: number;
  /** Average of SatisfactionScore across this employee's rated tickets (auto-closed/unrated tickets excluded). Null if never rated. */
  averageSatisfactionScore?: number;
  username: string;
  mustChangePassword: boolean;
  /** Permanent display id — "DAF-ADMIN-####" or "DAF-EMP-####". Set once at creation, never changes. See AccountReferenceIdService. */
  accountRefId: string;
}

/** Returned once, immediately after Admin registers a new employee. Never retrievable again after this response. */
export interface EmployeeRegisteredResult {
  employee: Employee;
  username: string;
  oneTimePassword: string;
  emailSent: boolean;
  emailError?: string;
}

export interface TimeLog {
  id: string;
  employeeId: string;
  date: string; // ISO date
  startTime?: string; // ISO datetime
  finishTime?: string; // ISO datetime
  totalHours?: number;
}

export interface MaintenanceRecord {
  id: string;
  date: string;
  category: MaintenanceCategory;
  description: string;
  performedByEmployeeId: string;
  status: MaintenanceStatus;
  remarks?: string;
}

export interface AppNotification {
  id: string;
  recipientType: NotificationRecipientType;
  recipientId: string; // employeeId, clientId, or 'ALL_ADMIN' / 'ALL_IT_SUPPORT'
  eventType: string;
  message: string;
  dateSent: string;
  readStatus: boolean;
}

export interface EmployeeOnTimeStats {
  employeeId: string;
  employeeName: string;
  onTimeCount: number;
  lateCount: number;
  totalResolved: number;
  onTimeRate: number; // 0-100
}

export interface OnTimeSummary {
  onTimeCount: number;
  lateCount: number;
  totalResolved: number;
  onTimeRate: number; // 0-100
  targetDays: number;
}

export interface OnTimeReport {
  summary: OnTimeSummary;
  byEmployee: EmployeeOnTimeStats[];
}

export interface TicketStatusSlice {
  status: string;
  count: number;
}

/** System-wide snapshot for the admin "Overall Operations" pie chart — live ticket-status breakdown plus headline counts. */
export interface OperationsOverview {
  ticketsByStatus: TicketStatusSlice[];
  totalTickets: number;
  activeClients: number;
  activeEmployees: number;
  openAgreements: number;
}

/**
 * The optional 5-question client satisfaction survey — separate from the
 * 1-5 star Confirm Resolution rating that gates ticket closure.
 */
export interface SatisfactionSurvey {
  id: string;
  ticketId: string;
  clientId: string;
  submittedAt: string;
  responseSpeedRating: number; // 1-5
  professionalismRating: number; // 1-5
  communicationClarityRating: number; // 1-5
  likelihoodToRecommend: number; // 1-5
  improvementFeedback?: string;
}

export type SessionAccountType = 'Employee' | 'Client';

export interface SessionActivity {
  accountType: SessionAccountType;
  accountId: string;
  accountName: string;
  onlineStatus: boolean;
  lastSeen: string;
  mostRecentIpAddress?: string;
}

export interface LoginSessionHistoryEntry {
  id: string;
  ipAddress: string;
  loginTime: string;
  logoutTime?: string;
  onlineStatus: boolean;
  lastSeen: string;
}

export interface EmployeePerformanceReport {
  employeeId: string;
  employeeName: string;
  ticketsAssigned: number;
  ticketsResolved: number;
  averageResolutionHours?: number;
  onTimeRate: number;
  averageSatisfactionScore?: number;
  totalHoursWorked: number;
  aiNarrativeAvailable: boolean;
  aiNarrative?: string;
  aiUnavailableReason?: string;
}

export interface AiSummaryResult {
  available: boolean;
  narrative?: string;
  unavailableReason?: string;
}

/** Mirrors the API's PagedResult<T> — one page of items plus metadata for rendering pager controls. */
export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

/** One admin-configurable value on the Settings → Configuration page. Mirrors SystemSettingDto. */
export interface SystemSetting {
  key: string;
  value: string;
  category: string;
  label: string;
  description: string;
  valueType: 'int' | 'bool' | 'string';
  updatedAt?: string;
  updatedByName?: string;
}

export type PasswordResetRequestStatus = 'Pending' | 'OtpIssued' | 'Dismissed';

/** A "forgot password" request awaiting Admin action — no self-service reset link, an Admin issues a fresh OTP by hand. Mirrors PasswordResetRequestDto. */
export interface PasswordResetRequest {
  id: string;
  accountType: SessionAccountType;
  accountId: string;
  username: string;
  note?: string;
  requestIpAddress: string;
  status: PasswordResetRequestStatus;
  requestedAt: string;
  resolvedAt?: string;
  resolvedByName?: string;
  dismissReason?: string;
  displayName: string;
  email: string;
}

/** Shown once, right after an Admin issues a fresh OTP — never retrievable again after this response. */
export interface PasswordResetOtpIssuedResult {
  username: string;
  oneTimePassword: string;
  emailSent: boolean;
  emailError?: string;
}

export type LocationType = 'Region' | 'City' | 'Woreda' | 'Specialization' | 'CustomRole';

/** One admin-managed dropdown/checklist option (Region, City, Woreda, Specialization, or CustomRole). */
export interface LocationEntry {
  id: string;
  type: LocationType;
  name: string;
}

/** All five option lists in one response — see LocationsController.GetAll. */
export interface LocationOptions {
  regions: LocationEntry[];
  cities: LocationEntry[];
  woredas: LocationEntry[];
  specializations: LocationEntry[];
  customRoles: LocationEntry[];
}

export type DurationUnit = 'Hours' | 'Days' | 'Months';

/** Admin-defined kind of client-system failure with an expected resolution duration, chosen by the client on ticket submission. */
export interface FailureType {
  id: string;
  name: string;
  description?: string;
  durationValue: number;
  durationUnit: DurationUnit;
}
