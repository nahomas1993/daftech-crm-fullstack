export type AccountStatus = 'Pending' | 'Approved' | 'Rejected';
export type EmployeeAccountStatus = 'Active' | 'Disabled';
export type AgreementStatus = 'Active' | 'Expired' | 'Pending';
export type BillingTier = 'Basic' | 'Intermediate' | 'Advanced';

/** Matches Domain.Enums.TicketCategory. */
export type TicketCategory = 'Frontend' | 'Backend' | 'Database';

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

/**
 * Matches Domain.Entities.SupportPhase — a coarser grouping of
 * TicketStatus for reporting/filtering only (see Reports module). Always
 * derived server-side from the ticket's Status; never set independently.
 */
export type SupportPhase = 'Intake' | 'Diagnosis' | 'Repair' | 'Verification' | 'Closed';

/** Matches Domain.Entities.TicketPriority. Any employee may set this on a ticket; feeds workload-aware Trainer assignment's "high-priority tickets" dimension. */
export type TicketPriority = 'Low' | 'Medium' | 'High';

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
 * Trainer is a dynamically assignable responsibility, not a separate
 * account type — an employee can hold both EmployeeTechnician and Trainer
 * at once (Roles is a list), assigned/changed by an Admin at any time.
 */
export type EmployeeRole = 'Admin' | 'ItSupport' | 'EmployeeTechnician' | 'Trainer';

export type DeviceType = 'Laptop' | 'Pc' | 'Tablet' | 'Other';
export type DeviceAccessStatus = 'Allowed' | 'Revoked';
export type NotificationRecipientType = 'Admin' | 'ItSupport' | 'Employee' | 'Client';

/** Display helpers — the API uses PascalCase enum names without spaces/slashes; these map back to the spec's human-readable labels. */
export const TICKET_CATEGORY_LABELS: Record<TicketCategory, string> = {
  Frontend: 'Frontend',
  Backend: 'Backend',
  Database: 'Database',
};

export const EMPLOYEE_ROLE_LABELS: Record<EmployeeRole, string> = {
  Admin: 'Admin',
  ItSupport: 'IT Support',
  EmployeeTechnician: 'Employee/Technician',
  Trainer: 'Trainer',
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
  zone?: string;
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

export type TrainingCompletionStatus = 'NotStarted' | 'InProgress' | 'Completed';

/**
 * One system/product a client has deployed — sits between Client and
 * Agreement: Client -> SystemProduct -> Agreement -> AgreementType. A
 * client can have multiple; creating a new one never replaces an existing
 * one. Training lives directly here (trainingAssignments, trainingCompletionStatus)
 * rather than as an Agreement — see AgreementType, whose "Training" value
 * is kept only as a lookup, no business rule keys off it anymore.
 */
export interface SystemProduct {
  id: string;
  clientId: string;
  /** Permanent display id — "DAF-SYS-####". */
  referenceNumber: string;
  name: string;
  description?: string;
  deploymentDate?: string; // ISO date
  /** System-derived from Admin's one-click Mark Training Completed action — not editable directly. Unlocks a Support agreement once Completed; more trainingRecords can still be added afterward. */
  trainingCompletionStatus: TrainingCompletionStatus;
  /** Who's currently assigned to train a client on this system/product — a roster, not a task list. See TrainingAssignment. */
  trainingAssignments: TrainingAssignment[];
}

/** Admin-managed lookup — Support and Training always exist (see AgreementTypeNames on the backend); an Admin can add further custom types. Training is kept as a lookup value only — the training workflow itself lives on SystemProduct, not as an Agreement. */
export interface AgreementType {
  id: string;
  name: string;
  description?: string;
  /** True for the built-in Support/Training types — the UI hides the delete action for these. */
  isSystemDefined: boolean;
}

/** One Trainer/Technician assigned to train a client on a SystemProduct — a roster entry, no lifecycle of its own. Being on this roster is what allows logging a TrainingRecord against this system/product. */
export interface TrainingAssignment {
  id: string;
  trainerEmployeeId: string;
  trainerEmployeeName: string;
  assignedAt: string; // ISO datetime
}

/**
 * One training session actually conducted, logged by the Trainer who
 * conducted it — "Add Training" on the trainer's own page. Open-ended: a
 * system/product can accumulate any number of these over time, even after
 * its trainingCompletionStatus is already Completed (e.g. a refresher).
 * There is no submit/approve step per record — Admin reviews the
 * accumulated set informally, then marks the whole SystemProduct's
 * training Completed as a separate one-click action.
 */
export interface TrainingRecord {
  id: string;
  systemProductId: string;
  systemProductName: string;
  clientId: string;
  clientName: string;
  trainerEmployeeId: string;
  trainerEmployeeName: string;
  trainingDate: string; // ISO date
  description: string;
  fileName?: string;
  createdAt: string; // ISO datetime
}

export interface Agreement {
  id: string;
  systemProductId: string;
  clientId: string;
  clientName: string;
  systemProductName: string;
  agreementTypeId: string;
  agreementTypeName: string;
  documentNumber: string;
  scannedFileUrl?: string;
  agreementPlace: string;
  /** Admin-entered: the date the agreement was signed — editable/backdatable, not forced to today. */
  signDate: string; // ISO date
  expiryDate: string; // ISO date
  supportWindowMonths: number;
  status: AgreementStatus;
  billingTier: BillingTier;
  details?: string;
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
  /** Which support type the client chose (remote, on-site...), if any. */
  supportTypeId?: string;
  supportTypeName?: string;
  /** Failure type base price + support type fee, in ETB. Only set on chargeable tickets. */
  chargeAmount?: number;
  /** True when the client ticked the acknowledgement box for a chargeable request. */
  chargeAcknowledged: boolean;
  status: TicketStatus;
  priority: TicketPriority;
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

export type LocationType = 'Region' | 'Zone' | 'City' | 'Woreda' | 'Specialization' | 'CustomRole';

/** One admin-managed dropdown/checklist option (Region, City, Woreda, Specialization, or CustomRole). */
export interface LocationEntry {
  id: string;
  type: LocationType;
  name: string;
}

/** All six option lists in one response — see LocationsController.GetAll. */
export interface LocationOptions {
  regions: LocationEntry[];
  zones: LocationEntry[];
  cities: LocationEntry[];
  woredas: LocationEntry[];
  specializations: LocationEntry[];
  customRoles: LocationEntry[];
}

export type DurationUnit = 'Hours' | 'Days' | 'Months';

/** Admin-defined kind of client-system failure with an expected resolution duration, chosen by the client on ticket submission. */
export interface FailureType {
  id: string;
  category: TicketCategory;
  name: string;
  description?: string;
  /** Charge in ETB for this failure type once the client's free support period has ended. */
  basePrice: number;
  durationValue: number;
  durationUnit: DurationUnit;
}

/** Admin-defined way support is delivered (remote, on-site, after hours...). Its fee is added to the failure type's base price on chargeable tickets. */
export interface SupportType {
  id: string;
  name: string;
  description?: string;
  additionalFee: number;
}

/** What a ticket would cost, worked out by the server before the client submits it. */
export interface TicketQuote {
  chargeable: boolean;
  basePrice: number;
  supportFee: number;
  total: number;
  freeSupportEndsOn?: string | null;
}

export interface ExpiringClient {
  clientId: string; clientName: string; agreementId: string; systemProductName: string;
  expiryDate: string; daysUntilExpiry: number;
}
export interface SupportClient {
  clientId: string; clientName: string; ticketCount: number;
}
export interface SupportOverview {
  approachingExpirationCount: number;
  freeSupportClientCount: number;
  chargeableSupportClientCount: number;
  approachingExpiration: ExpiringClient[];
  freeSupportClients: SupportClient[];
  chargeableSupportClients: SupportClient[];
}

// --- Reports module (tables only — see Dashboard for charts/KPIs) ---

/** Every field optional — an unset filter simply doesn't narrow that dimension. Matches Application.DTOs.TicketReportFilter. */
export interface TicketReportFilter {
  fromDate?: string; // ISO date (yyyy-MM-dd)
  toDate?: string;
  month?: number; // 1-12
  region?: string;
  zone?: string;
  woreda?: string;
  employeeId?: string;
  failureTypeId?: string;
  status?: TicketStatus;
  supportPhase?: SupportPhase;
  search?: string;
}

export interface TableReportResult<T> {
  rows: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface CustomerSupportReportRow {
  ticketId: string; clientName: string; region?: string; zone?: string; woreda?: string;
  systemProductName?: string; failureTypeName?: string; dateSubmitted: string;
  assignedEmployeeName?: string; status: TicketStatus; supportPhase: SupportPhase;
  chargeable: boolean; resolvedAt?: string; satisfactionScore?: number;
}

export interface EmployeePerformanceReportRow {
  employeeId: string; employeeName: string; totalAssigned: number; resolved: number; open: number;
  overdue: number; averageResolutionHours?: number; onTimeRatePercent?: number; averageSatisfactionScore?: number;
}

export interface RegionalReportRow {
  region?: string; zone?: string; woreda?: string; ticketCount: number; openCount: number; resolvedCount: number;
  averageResolutionHours?: number; averageSatisfactionScore?: number;
}

export interface FailureTypeReportRow {
  failureTypeId?: string; failureTypeName: string; ticketCount: number; onTimeCount: number; lateCount: number;
  onTimeRatePercent?: number; averageResolutionHours?: number;
}

export interface ResolutionTimeReportRow {
  ticketId: string; clientName: string; failureTypeName?: string; assignedEmployeeName?: string;
  assignedAt?: string; resolvedAt?: string; resolutionHours?: number; expectedResolutionHours?: number; wasOnTime?: boolean;
}

export interface CustomerRatingReportRow {
  ticketId: string; clientName: string; assignedEmployeeName?: string; resolvedAt?: string;
  satisfactionStars: number; satisfactionScore: number; closureReason?: ClosureReason;
}

export type ReportType = 'customer-support' | 'employee-performance' | 'regional' | 'failure-type' | 'resolution-time' | 'customer-rating';

export const REPORT_TYPE_LABELS: Record<ReportType, string> = {
  'customer-support': 'Customer / Support',
  'employee-performance': 'Employee Performance',
  regional: 'Regional',
  'failure-type': 'Failure Type',
  'resolution-time': 'Resolution Time',
  'customer-rating': 'Customer Rating',
};

// --- Workload-aware Trainer assignment (Phase 5) ---

export interface TrainerWorkload {
  employeeId: string;
  employeeName: string;
  activeTicketCount: number;
  pendingTicketCount: number;
  highPriorityTicketCount: number;
  overdueTicketCount: number;
  /** System/products this Trainer is on the training roster for whose training isn't yet Completed. */
  openTrainingAssignmentCount: number;
  workloadScore: number;
  isExcessiveWorkload: boolean;
}

export interface TrainerAssignmentRecommendation {
  eligibleTrainers: TrainerWorkload[];
  recommendedTrainerEmployeeId?: string;
}

// --- Dashboard (charts + KPIs only — see the Reports module DTOs above for tables) ---

export interface DashboardFilter {
  fromDate?: string;
  toDate?: string;
  region?: string;
}

export interface DashboardKpis {
  totalTickets: number; openTickets: number; resolvedTickets: number; overdueTickets: number;
  resolutionRatePercent: number; averageSatisfactionScore?: number;
}

export interface RegionTicketCount { region: string; ticketCount: number; }
export interface FailureTypeTicketCount { failureTypeName: string; ticketCount: number; }
export interface EmployeeTicketCount { employeeName: string; resolvedCount: number; }
export interface TicketStatusSlice { status: string; count: number; }
export interface MonthlyPoint { month: string; ticketCount: number; resolvedCount: number; onTimeRatePercent?: number; }
export interface RatingSlice { stars: number; count: number; }

export interface DashboardData {
  kpis: DashboardKpis;
  ticketsByRegion: RegionTicketCount[];
  ticketsByFailureType: FailureTypeTicketCount[];
  ticketsByEmployee: EmployeeTicketCount[];
  ticketsByStatus: TicketStatusSlice[];
  ratingDistribution: RatingSlice[];
  monthlyTrend: MonthlyPoint[];
  supportOverview: SupportOverview;
  /** Sections the API could not build on this request — the Dashboard shows a per-widget notice instead of blanking the page. */
  failedSections?: string[];
}

/** One system/product an Admin put the logged-in Trainer on the roster for — see TrainingService.getMyAssignments. */
export interface MyTrainingAssignment {
  systemProductId: string;
  systemProductName: string;
  clientId: string;
  clientName: string;
}
