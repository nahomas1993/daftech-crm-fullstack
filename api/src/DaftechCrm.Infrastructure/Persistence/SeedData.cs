using DaftechCrm.Application.Services;
using DaftechCrm.Domain.Entities;
using DaftechCrm.Domain.Enums;

namespace DaftechCrm.Infrastructure.Persistence;

/// <summary>
/// Deterministic seed data — same fixed Guids/dates every run so migrations
/// stay reproducible. Mirrors src/app/core/mock-data.ts on the Angular side
/// so a fresh dev database tells the same demo story as the frontend mocks.
///
/// Seeded accounts use known dev credentials (documented in the backend
/// README) so the demo can be logged into immediately without depending on
/// SMTP being configured. In a real deployment, every account is created
/// through Employees/Clients registration instead, which issues a random
/// one-time password and emails it via MailKit.
/// </summary>
public static class SeedData
{
    public static readonly Guid Emp1Admin = Guid.Parse("11111111-0000-0000-0000-000000000001");

    /// <summary>
    /// The single dedicated testing employee account. Previously there were
    /// four seeded employees (2 more active technicians + 1 disabled); those
    /// were trimmed to keep exactly one Admin + one Employee for login
    /// testing, per the account cleanup requirement. Nothing referenced
    /// their Guids elsewhere (no seeded tickets/assignments pointed at
    /// them), so removing them here is safe and doesn't touch client or
    /// agreement data.
    /// </summary>
    public static readonly Guid Emp2Tech = Guid.Parse("11111111-0000-0000-0000-000000000002");

    public static readonly Guid Client1 = Guid.Parse("22222222-0000-0000-0000-000000000001");
    public static readonly Guid Client2 = Guid.Parse("22222222-0000-0000-0000-000000000002");

    /// <summary>One demo SystemProduct per demo client — the layer that now sits between Client and Agreement (see SystemProduct).</summary>
    public static readonly Guid Client1System = Guid.Parse("55555555-0000-0000-0000-000000000001");
    public static readonly Guid Client2System = Guid.Parse("55555555-0000-0000-0000-000000000002");

    /// <summary>Fixed ids for the two always-present AgreementTypes — see AgreementTypeNames.</summary>
    public static readonly Guid SupportAgreementType = Guid.Parse("66666666-0000-0000-0000-000000000001");
    public static readonly Guid TrainingAgreementType = Guid.Parse("66666666-0000-0000-0000-000000000002");

    public static readonly Guid Agreement1 = Guid.Parse("33333333-0000-0000-0000-000000000001");
    public static readonly Guid Agreement2 = Guid.Parse("33333333-0000-0000-0000-000000000002");

    /// <summary>Dev-only known password for every seeded account. Never used outside seed data.</summary>
    public const string SeedPassword = "DaftechDemo1!";

    public static IEnumerable<Employee> Employees()
    {
        yield return new Employee
        {
            Id = Emp1Admin, FullName = "Nahom Alehegne", Email = "nahom@daftech.et", PhoneNumber = "+251911000001",
            Specialization = "Back-end",
            Roles = new() { EmployeeRole.Admin }, AccountStatus = EmployeeAccountStatus.Active,
            AllowedIpAddresses = new() { "196.188.20.10" },
            AccountRefId = "DAF-ADMIN-1001",
            Username = "na1001", PasswordHash = PasswordHasher.Hash(SeedPassword), MustChangePassword = false,
        };
        yield return new Employee
        {
            Id = Emp2Tech, FullName = "Nebil Sherefa", Email = "nebil@daftech.et", PhoneNumber = "+251911000002",
            Specialization = "Front-end",
            // Carries both responsibilities so the demo data exercises the
            // dynamic multi-responsibility model (an Employee can be both
            // Technician and Trainer — see EmployeeRole.Trainer).
            Roles = new() { EmployeeRole.EmployeeTechnician, EmployeeRole.Trainer }, AccountStatus = EmployeeAccountStatus.Active,
            AllowedIpAddresses = new(),
            AccountRefId = "DAF-EMP-1002",
            Username = "ns1002", PasswordHash = PasswordHasher.Hash(SeedPassword), MustChangePassword = false,
        };
    }

    public static IEnumerable<Client> Clients()
    {
        yield return new Client
        {
            Id = Client1, Name = "Abyssinia Traders PLC", IdNumber = "ID-88213", PhoneNumber = "+251911223344",
            Email = "contact@abyssiniatraders.et",
            Office = "Bole Head Office", Location = "Addis Ababa", KycType = "Business License",
            KycContact = "Selam Tesfaye — +251911998877", AccountStatus = ClientAccountStatus.Approved,
            OnboardingDate = DateOnly.Parse("2025-02-10"),
            AccountRefId = "DAF-CLI-2001",
            Username = "at2001", PasswordHash = PasswordHasher.Hash(SeedPassword), MustChangePassword = false,
        };
        yield return new Client
        {
            Id = Client2, Name = "Merkato Micro-Finance", IdNumber = "ID-77012", PhoneNumber = "+251922334455",
            Email = "info@merkatomf.et",
            Office = "Merkato Branch", Location = "Addis Ababa", KycType = "Financial Institution License",
            KycContact = "Dawit Alemu — +251922112233", AccountStatus = ClientAccountStatus.Approved,
            OnboardingDate = DateOnly.Parse("2024-11-03"),
            AccountRefId = "DAF-CLI-2002",
            Username = "mm2002", PasswordHash = PasswordHasher.Hash(SeedPassword), MustChangePassword = false,
        };
    }

    /// <summary>
    /// The two AgreementTypes the app's business rules depend on by name
    /// (see AgreementTypeNames.Support/Training). Seeded idempotently on
    /// every startup — not just for a fresh database — by
    /// DependencyInjection.EnsureCoreAgreementTypesAsync, the same way
    /// EnsureDemoAccountsAsync guarantees the demo logins exist regardless
    /// of prior deploys.
    /// </summary>
    public static IEnumerable<AgreementType> CoreAgreementTypes()
    {
        yield return new AgreementType
        {
            Id = SupportAgreementType, Name = AgreementTypeNames.Support,
            Description = "Ongoing technical support for a client's system/product.",
            IsSystemDefined = true,
        };
        yield return new AgreementType
        {
            Id = TrainingAgreementType, Name = AgreementTypeNames.Training,
            Description = "Client staff training on a system/product — must be completed before a Support agreement can be signed for the same system/product.",
            IsSystemDefined = true,
        };
    }

    /// <summary>One demo SystemProduct per demo client — the layer that now sits between Client and Agreement (see SystemProduct). TrainingCompletionStatus/roster/records for each are seeded separately below (TrainingAssignments/TrainingRecords), matching the story SystemProducts() sets up here.</summary>
    public static IEnumerable<SystemProduct> SystemProducts()
    {
        yield return new SystemProduct
        {
            Id = Client1System, ClientId = Client1, ReferenceNumber = "DAF-SYS-2025-0001",
            Name = "Branch POS & Inventory System", Description = "Core point-of-sale and inventory system across all branches.",
            DeploymentDate = DateOnly.Parse("2025-01-15"),
            // Training already completed — this is what allows Agreement1
            // (the Support agreement below) to have been signed at all.
            TrainingCompletionStatus = TrainingCompletionStatus.Completed,
        };
        yield return new SystemProduct
        {
            Id = Client2System, ClientId = Client2, ReferenceNumber = "DAF-SYS-2025-0002",
            Name = "Loan Origination Portal", Description = "Client-facing loan application and origination workflow.",
            DeploymentDate = DateOnly.Parse("2024-12-01"),
            // Training still in progress — deliberately no Support
            // agreement exists for this system/product, since signing one
            // would violate the per-SystemProduct training-first rule the
            // app enforces (see AgreementService.CreateAsync).
            TrainingCompletionStatus = TrainingCompletionStatus.InProgress,
        };
    }

    /// <summary>
    /// Demo agreements — Support only now (training is no longer modeled
    /// as an Agreement at all — see SystemProduct.TrainingAssignments/
    /// TrainingRecords/TrainingCompletionStatus, and TrainingAssignments()/
    /// TrainingRecords() below):
    ///  - Client1: one Support agreement, allowed only because
    ///    Client1System.TrainingCompletionStatus is already Completed.
    ///  - Client2: no Support agreement — its SystemProduct's training is
    ///    still InProgress, so signing one would be rejected.
    /// </summary>
    public static IEnumerable<Agreement> Agreements()
    {
        yield return new Agreement
        {
            Id = Agreement1, SystemProductId = Client1System, AgreementTypeId = SupportAgreementType,
            DocumentNumber = "DAF-AGR-2025-0002", AgreementPlace = "Addis Ababa", SignDate = DateOnly.Parse("2025-02-10"),
            ExpiryDate = DateOnly.Parse("2027-02-10"),
            SupportWindowMonths = 12, Status = AgreementStatus.Active, BillingTier = BillingTier.Intermediate,
        };
    }

    public static readonly Guid Client1TrainingAssignment = Guid.Parse("77777777-0000-0000-0000-000000000001");
    public static readonly Guid Client2TrainingAssignment = Guid.Parse("77777777-0000-0000-0000-000000000002");

    /// <summary>Training roster for the two demo SystemProducts above — Nebil (the one seeded Trainer) assigned to both.</summary>
    public static IEnumerable<TrainingAssignment> TrainingAssignments()
    {
        yield return new TrainingAssignment
        {
            Id = Client1TrainingAssignment, SystemProductId = Client1System, TrainerEmployeeId = Emp2Tech,
            AssignedAt = DateTimeOffset.Parse("2025-01-18T09:00:00Z"),
        };
        yield return new TrainingAssignment
        {
            Id = Client2TrainingAssignment, SystemProductId = Client2System, TrainerEmployeeId = Emp2Tech,
            AssignedAt = DateTimeOffset.Parse("2025-01-14T09:00:00Z"),
        };
    }

    /// <summary>
    /// The open-ended training log for the two demo SystemProducts —
    /// Client1 shows two sessions (matching its Completed status: Admin
    /// judged this was enough and marked it Completed), Client2 shows one
    /// so far (matching its InProgress status: still ongoing).
    /// </summary>
    public static IEnumerable<TrainingRecord> TrainingRecords()
    {
        yield return new TrainingRecord
        {
            SystemProductId = Client1System, TrainerEmployeeId = Emp2Tech,
            TrainingDate = DateOnly.Parse("2025-01-20"),
            Description = "POS entry and end-of-day reconciliation walkthrough with front-desk and finance staff. 8 of 9 invited staff attended.",
            CreatedAt = DateTimeOffset.Parse("2025-01-20T15:00:00Z"),
        };
        yield return new TrainingRecord
        {
            SystemProductId = Client1System, TrainerEmployeeId = Emp2Tech,
            TrainingDate = DateOnly.Parse("2025-02-10"),
            Description = "Follow-up session covering inventory adjustments and stock transfer entry.",
            CreatedAt = DateTimeOffset.Parse("2025-02-10T15:30:00Z"),
        };
        yield return new TrainingRecord
        {
            SystemProductId = Client2System, TrainerEmployeeId = Emp2Tech,
            TrainingDate = DateOnly.Parse("2025-01-15"),
            Description = "Branch staff onboarding session — loan application intake and document upload workflow.",
            CreatedAt = DateTimeOffset.Parse("2025-01-15T14:00:00Z"),
        };
    }

    /// <summary>
    /// Same two accounts as Employees() above — exposed under a separate
    /// name because DependencyInjection.EnsureDemoAccountsAsync calls this
    /// on every single startup (upserting by Username), not just once when
    /// the database is empty like Employees()/Clients() are. Kept as a
    /// direct alias rather than a duplicate list so there's one source of
    /// truth for what the demo accounts actually are.
    /// </summary>
    public static IEnumerable<Employee> DemoEmployees() => Employees();

    /// <summary>Same two accounts as Clients() above — see DemoEmployees() for why this exists as a separate name.</summary>
    public static IEnumerable<Client> DemoClients() => Clients();
}
