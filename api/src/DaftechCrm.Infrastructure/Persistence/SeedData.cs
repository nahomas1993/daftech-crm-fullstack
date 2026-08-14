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

    public static readonly Guid Agreement1 = Guid.Parse("33333333-0000-0000-0000-000000000001");
    public static readonly Guid Agreement2 = Guid.Parse("33333333-0000-0000-0000-000000000002");
    public static readonly Guid Agreement1Training = Guid.Parse("44444444-0000-0000-0000-000000000001");
    public static readonly Guid Agreement2Training = Guid.Parse("44444444-0000-0000-0000-000000000002");

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
            Roles = new() { EmployeeRole.EmployeeTechnician }, AccountStatus = EmployeeAccountStatus.Active,
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
    /// Demo trainings, seeded independently of any agreement — training now
    /// happens (and must finish) BEFORE a support agreement can exist at
    /// all:
    ///  - Client1 (Abyssinia Traders): training already completed
    ///    (EndDate set) — this is what makes Agreements() below able to
    ///    sign an agreement for this client.
    ///  - Client2 (Merkato Micro-Finance): training started but not yet
    ///    finished (no EndDate) — deliberately has NO agreement in
    ///    Agreements(), since signing one for this client would violate
    ///    the same mandatory-training rule the app enforces.
    /// </summary>
    public static IEnumerable<AgreementTraining> Trainings()
    {
        yield return new AgreementTraining
        {
            Id = Agreement1Training, ClientId = Client1, AgreementId = Agreement1,
            Description = "On-site system training for front-desk and finance staff.",
            StartDate = DateOnly.Parse("2025-01-20"), EndDate = DateOnly.Parse("2025-02-10"),
        };
        yield return new AgreementTraining
        {
            Id = Agreement2Training, ClientId = Client2, AgreementId = null,
            Description = "Branch staff onboarding — in progress, end date not yet confirmed.",
            StartDate = DateOnly.Parse("2025-01-15"), EndDate = null,
        };
    }

    /// <summary>
    /// Demo agreements. Only Client1 gets one, because only Client1 has a
    /// completed training (see Trainings() above) — an agreement can't be
    /// signed for Client2 until its training finishes, matching
    /// AgreementService.CreateAsync's hard-block. SignDate is admin-entered
    /// (the date the agreement was signed), not derived.
    /// </summary>
    public static IEnumerable<Agreement> Agreements()
    {
        yield return new Agreement
        {
            Id = Agreement1, ClientId = Client1, DocumentNumber = "DAF-AGR-2025-0001",
            AgreementPlace = "Addis Ababa", SignDate = DateOnly.Parse("2025-02-10"),
            ExpiryDate = DateOnly.Parse("2027-02-10"),
            SupportWindowMonths = 12, Status = AgreementStatus.Active, BillingTier = BillingTier.Intermediate,
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
