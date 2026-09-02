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
  /** Optional link back to the admin-managed Systems/Products catalog entry this was created from. See ProductCatalogItem. */
  catalogItemId?: string;
  /** When this client's system/product is due to expire — shown on the client dashboard. Optional. */
  expiryDate?: string; // ISO date
  /** Set once a Trainer has submitted their training checklist — see SystemProductService.submitTraining. Undefined until then. */
  trainingSubmittedAt?: string; // ISO datetime
}

/** Admin-managed catalog entry describing a kind of system/product DAFTECH deploys (e.g. "Branch POS System") — configurable from Settings, no code change required. */
export interface ProductCatalogItem {
  id: string;
  name: string;
  description?: string;
  /** False once an Admin retires this entry — hidden from new selections but still resolvable by Id for anything already pointing at it. */
  isActive: boolean;
}

/** Admin-managed lookup — Support and Training always exist (see AgreementTypeNames on the backend); an Admin can add further custom types. Training is kept as a lookup value only — the training workflow itself lives on SystemProduct, not as an Agreement. */
export interface AgreementType {
  id: string;
  name: string;
  description?: string;
  /** True for the built-in Support/Training types — the UI hides the delete action for these. */
  isSystemDefined: boolean;
  /** When true, this type appears as a checklist item in the Trainer's training workflow (see AgreementType.IsTrainingItem on the backend). */
  isTrainingItem: boolean;
  /** Only meaningful when isTrainingItem is true — whether logging this item is required before a system/product's training can be marked Completed. */
  isRequiredForCompletion: boolean;
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
 * There is no submit/approve step per record — the Trainer saves each one
 * as they finish it, then hits Submit once every item on their checklist
 * is logged (see SystemProductService.submitTraining); Admin's review of
 * the accumulated set happens separately via Mark Training Completed.
 *
 * agreementTypeId/-Name is which admin-configured checklist item (e.g.
 * "Attendance") this session is for — see AgreementType. startDateTime/
 * endDateTime are both optional: some items have no real duration worth
 * recording. A same-day training just carries the same date on both with
 * different times; a multi-day one carries different dates.
 */
export interface TrainingRecord {
  id: string;
  systemProductId: string;
  systemProductName: string;
  clientId: string;
  clientName: string;
  trainerEmployeeId: string;
  trainerEmployeeName: string;
  agreementTypeId: string;
  agreementTypeName: string;
  trainingDate: string; // ISO date
  startDateTime?: string; // ISO datetime
  endDateTime?: string; // ISO datetime
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
  /** Which of the client's systems/products this issue is about. Undefined only for tickets submitted before this field existed. */
  systemProductId?: string;
  systemProductName?: string;
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
  /** Snapshot of the client's IT support contact taken at submission. Undefined only for tickets submitted before this field existed. */
  itSupportContact?: string;
  /** Specialty required to work this ticket, resolved once at submission. Undefined if no specialty restriction applied. */
  requiredSpecialization?: string;
  /** Set once, when a technician marked this ticket Resolved. */
  completedAt?: string; // ISO datetime
  completedByEmployeeId?: string;
  completedByEmployeeName?: string;
  /** Working minutes from assignment to completion (excludes lunch/off-hours/weekends). Undefined until completed. */
  workingMinutesToComplete?: number;
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
  performedByEmployeeName: string;
  status: MaintenanceStatus;
  remarks?: string;
  /** Optional links so a record can show up in a client's or system/product's Maintenance History tab. Undefined on records logged before these links existed. */
  clientId?: string;
  clientName?: string;
  systemProductId?: string;
  systemProductName?: string;
  ticketId?: string;
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
 * One 1-5 rating a client gave to one admin-authored survey question.
 * QuestionText is snapshotted at submission time, so it still displays
 * correctly even if an admin later edits or deletes the question.
 */
export interface SurveyAnswer {
  questionId?: string | null;
  questionText: string;
  displayOrder: number;
  rating: number; // 1-5
}

/**
 * The optional, admin-configurable client satisfaction survey — separate
 * from the 1-5 star Confirm Resolution rating that gates ticket closure.
 * The question set itself lives in SurveyQuestion, managed from
 * Settings → Configuration → Satisfaction Survey.
 */
export interface SatisfactionSurvey {
  id: string;
  ticketId: string;
  clientId: string;
  submittedAt: string;
  answers: SurveyAnswer[];
  /** The client's own words describing their experience — a short free-text paragraph, optional. */
  satisfactionComment?: string;
}

/** An admin-authored satisfaction survey question. Fully dynamic — admins add/edit/reorder/delete these; there is no fixed question set. */
export interface SurveyQuestion {
  id: string;
  text: string;
  displayOrder: number;
  isActive: boolean;
}

/** Label shown alongside each 1-5 rating value on the satisfaction survey. */
export const SATISFACTION_RATING_LABELS: Record<number, string> = {
  1: 'Poor',
  2: 'Satisfactory',
  3: 'Good',
  4: 'Very good',
  5: 'Excellent',
};

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
  valueType: 'int' | 'bool' | 'string' | 'time';
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

/**
 * One admin-managed dropdown/checklist option (Region, Zone, City, Woreda,
 * Specialization, or CustomRole). Region/Zone/Woreda chain through
 * parentId: a Zone's parentId is its owning Region's id, a Woreda's
 * parentId is its owning Zone's id. City/Specialization/CustomRole and
 * top-level Region rows always have parentId undefined.
 */
export interface LocationEntry {
  id: string;
  type: LocationType;
  name: string;
  parentId?: string;
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
  /** Optional free-text specialty (matches the Employee Specialization field) required to work tickets of this failure type. Undefined means no specialty restriction. */
  requiredSpecialization?: string;
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

export type ReportType =
  | 'customer-support' | 'employee-performance' | 'regional' | 'failure-type' | 'resolution-time' | 'customer-rating'
  | 'support-expiration' | 'client-report';

export const REPORT_TYPE_LABELS: Record<ReportType, string> = {
  'customer-support': 'Customer / Support',
  'employee-performance': 'Employee Performance',
  regional: 'Regional',
  'failure-type': 'Failure Type',
  'resolution-time': 'Resolution Time',
  'customer-rating': 'Customer Rating',
  'support-expiration': 'Support & Expiration',
  'client-report': 'Overall Client Report',
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

// --- Overall Client Report (admin Reports page — one client's full history) ---

export interface ClientReportTicket {
  id: string;
  description: string;
  category: TicketCategory;
  failureTypeName?: string;
  dateSubmitted: string;
  assignedEmployeeName?: string;
  status: TicketStatus;
  supportPhase: SupportPhase;
  chargeable: boolean;
  chargeAmount?: number;
  resolvedAt?: string;
  satisfactionStars?: number;
  satisfactionScore?: number;
  closureReason?: ClosureReason;
  attachmentFileName?: string;
  voiceNoteFileName?: string;
}

export interface ClientReportAgreement {
  id: string;
  agreementTypeName: string;
  documentNumber: string;
  signDate: string;
  expiryDate: string;
  supportWindowMonths: number;
  status: AgreementStatus;
  billingTier: BillingTier;
}

export interface ClientReportTrainingRecord {
  id: string;
  trainerEmployeeName: string;
  trainingDate: string;
  description: string;
  fileName?: string;
}

export interface ClientReportSystemProduct {
  id: string;
  referenceNumber: string;
  name: string;
  description?: string;
  deploymentDate?: string;
  trainingCompletionStatus: TrainingCompletionStatus;
  agreements: ClientReportAgreement[];
  trainingRecords: ClientReportTrainingRecord[];
}

export interface ClientReportSurveyAnswer {
  questionText: string;
  rating: number;
}

export interface ClientReportSurvey {
  id: string;
  ticketId: string;
  submittedAt: string;
  answers: ClientReportSurveyAnswer[];
  satisfactionComment?: string;
}

export interface ClientReportSummary {
  systemProductCount: number;
  activeAgreementCount: number;
  totalTicketCount: number;
  openTicketCount: number;
  resolvedTicketCount: number;
  averageSatisfactionScore?: number;
  surveyCount: number;
}

/** Everything about one client in a single call — profile, systems/products with agreements and training history, every ticket, every satisfaction survey, and a summary block. See ReportService.getOverallClientReport. */
export interface OverallClientReport {
  clientId: string;
  clientName: string;
  accountRefId: string;
  phoneNumber: string;
  email: string;
  office: string;
  location: string;
  region?: string;
  zone?: string;
  city?: string;
  woreda?: string;
  accountStatus: AccountStatus;
  onboardingDate: string;
  systemProducts: ClientReportSystemProduct[];
  tickets: ClientReportTicket[];
  satisfactionSurveys: ClientReportSurvey[];
  summary: ClientReportSummary;
}

/** Outcome for one row of a client bulk-import CSV — see ClientImportService (Angular) and the backend's ClientImportService. */
export interface ClientImportRowResult {
  rowNumber: number;
  clientName: string;
  systemProductName: string;
  success: boolean;
  /** Null on success. Names the specific problem with this row. */
  error?: string;
  /** True when this row's client name matched an existing client and was held for manual review instead of being imported. */
  flaggedAsDuplicate: boolean;
  clientId?: string;
  systemProductId?: string;
  agreementId?: string;
  /** The login username issued for a newly created client — null for rows that attached to a client created earlier in the same import (see the row that actually created the client for its username), and for failed/flagged rows. */
  issuedUsername?: string;
  /** The plaintext one-time password issued for a newly created client on this row — the only place it's ever surfaced; it isn't retained anywhere after this response. Null for a row that attached to an earlier-created client, or a failed/flagged row. */
  issuedOneTimePassword?: string;
  /** Only set on the row that actually created the client (same null pattern as issuedOneTimePassword) — the email and location fields the import saved onto the client record. */
  email?: string;
  region?: string;
  zone?: string;
  city?: string;
  woreda?: string;
}

/** Full report returned after a bulk import run. */
export interface ClientImportResult {
  totalRows: number;
  succeededCount: number;
  failedCount: number;
  flaggedDuplicateCount: number;
  rows: ClientImportRowResult[];
}

