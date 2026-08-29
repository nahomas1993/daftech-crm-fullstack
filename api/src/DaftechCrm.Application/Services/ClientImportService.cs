using System.Globalization;
using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Domain.Entities;
using DaftechCrm.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DaftechCrm.Application.Services;

/// <summary>
/// Bulk-imports old paper client records (client + systems/products +
/// agreements + training-completion status) from a CSV someone has
/// transcribed by hand — built for the "hundreds of existing clients,
/// currently on paper" migration, so an Admin doesn't have to re-key each
/// one through the registration UI individually.
///
/// Every row is processed independently: a bad row is reported and
/// skipped rather than failing the whole file, since a batch this size
/// will always have a handful of typos or missing fields on the first
/// pass. Real login credentials are issued exactly as they are for a
/// normal registration (see AccountCredentialService) — but the
/// credential EMAIL is deliberately NOT sent during import, to avoid
/// firing off hundreds of "your account is ready" emails in one burst;
/// each client's email can be sent individually afterward via the
/// existing "Resend credential email" action on their Client Detail page,
/// at whatever pace makes sense.
/// </summary>
public class ClientImportService
{
    private readonly IAppDbContext _db;
    private readonly AccountCredentialService _credentials;
    private readonly ReferenceNumberService _referenceNumbers;
    private readonly AccountReferenceIdService _accountRefIds;

    public ClientImportService(
        IAppDbContext db,
        AccountCredentialService credentials,
        ReferenceNumberService referenceNumbers,
        AccountReferenceIdService accountRefIds)
    {
        _db = db;
        _credentials = credentials;
        _referenceNumbers = referenceNumbers;
        _accountRefIds = accountRefIds;
    }

    /// <summary>
    /// Parses and imports every row of the given CSV. Rows are processed
    /// in file order, one at a time — later rows can match against
    /// clients created earlier in the same run (so a client's second and
    /// third system/product rows attach to the client created by their
    /// first row, rather than each row creating its own client).
    /// </summary>
    public async Task<ClientImportResult> ImportAsync(Stream csvStream, CancellationToken ct = default)
    {
        var rows = CsvImportParser.Parse(csvStream);
        var results = new List<ClientImportRowResult>();

        // Name -> (Id, Username) of clients created so far in this run, so
        // row 2 of the same client (same ClientName) attaches to the
        // client row 1 just created instead of creating a second one or
        // being wrongly flagged as a duplicate of itself.
        var clientsCreatedThisRun = new Dictionary<string, (Guid Id, string? Username)>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var pendingEntitiesThisRow = new List<object>();
            try
            {
                var result = await ImportRowAsync(row, clientsCreatedThisRun, pendingEntitiesThisRow, ct);
                results.Add(result);
            }
            catch (Exception ex) when (ex is InvalidOperationException or ValidationException)
            {
                results.Add(new ClientImportRowResult(row.RowNumber, row.ClientName, row.SystemProductName, false, ex.Message, false));
                // If this row got as far as staging a new Client (added to
                // pendingEntitiesThisRow) before a later step in the same
                // row failed validation, that Client was never actually
                // saved — undo the bookkeeping that assumed it would be,
                // so a later row for the same name creates it fresh
                // instead of pointing at a client that doesn't exist.
                if (pendingEntitiesThisRow.OfType<Client>().Any())
                {
                    clientsCreatedThisRun.Remove(row.ClientName);
                    foreach (var entity in pendingEntitiesThisRow) _db.Detach(entity);
                }
            }
            catch (DbUpdateException)
            {
                // Most commonly a unique-constraint hit (e.g. two paper
                // records happen to share an email/username collision
                // edge case not caught by the name-based duplicate check
                // above). Reported plainly rather than surfacing the raw
                // SQL error to whoever's reading this report.
                results.Add(new ClientImportRowResult(
                    row.RowNumber, row.ClientName, row.SystemProductName, false,
                    "Could not save this row — a database constraint was violated (often a duplicate email). Check for an existing client with this email and re-import this row separately.",
                    false));

                // The entity that failed to save is still tracked by the
                // context in a "poisoned" state — if left as-is, the very
                // next row's SaveChangesAsync would also fail on the same
                // stale instance. ImportRowAsync records everything it
                // adds into pendingEntitiesThisRow for exactly this
                // cleanup step. Same reasoning as above for undoing the
                // clientsCreatedThisRun bookkeeping.
                clientsCreatedThisRun.Remove(row.ClientName);
                foreach (var entity in pendingEntitiesThisRow) _db.Detach(entity);
            }
        }

        return new ClientImportResult(
            results.Count,
            results.Count(r => r.Success),
            results.Count(r => !r.Success && !r.FlaggedAsDuplicate),
            results.Count(r => r.FlaggedAsDuplicate),
            results
        );
    }

    private async Task<ClientImportRowResult> ImportRowAsync(
        ClientImportRow row,
        Dictionary<string, (Guid Id, string? Username)> clientsCreatedThisRun,
        List<object> pendingEntitiesThisRow,
        CancellationToken ct)
    {
        RequiredFieldValidator.EnsureAllPresent(
            ("Row's ClientName", row.ClientName),
            ("Row's PhoneNumber", row.PhoneNumber),
            ("Row's Email", row.Email),
            ("Row's Office", row.Office),
            ("Row's Location", row.Location),
            ("Row's KycType", row.KycType),
            ("Row's KycContact", row.KycContact),
            ("Row's SystemProductName", row.SystemProductName),
            ("Row's TrainingCompleted", row.TrainingCompleted),
            ("Row's Region", row.Region),
            ("Row's Zone", row.Zone),
            ("Row's City", row.City),
            ("Row's Woreda", row.Woreda)
        );

        var trainingCompleted = ParseYesNo(row.TrainingCompleted, "TrainingCompleted");

        // --- Resolve or create the Client ---
        Guid clientId;
        string? username;
        if (clientsCreatedThisRun.TryGetValue(row.ClientName, out var created))
        {
            // A later row for the same client name created earlier in
            // this same import run — attach to it, no duplicate flag.
            // This is the "client has 3 systems/products, 3 rows" case.
            clientId = created.Id;
            username = created.Username;
        }
        else
        {
            var existingMatch = await _db.Clients
                .Where(c => !c.IsDeleted && c.Name.ToLower() == row.ClientName.ToLower())
                .Select(c => c.Id)
                .FirstOrDefaultAsync(ct);

            if (existingMatch != Guid.Empty)
            {
                // Same name already exists in the system from before this
                // import — could be the same client (paper record being
                // backfilled onto an account that already exists) or a
                // genuine coincidence. Per policy this is always flagged
                // for a human to check rather than guessed at automatically.
                return new ClientImportRowResult(
                    row.RowNumber, row.ClientName, row.SystemProductName, false,
                    "A client with this name already exists in the system — review manually and, if it's the same client, add this system/product to their existing account instead of importing.",
                    FlaggedAsDuplicate: true);
            }

            var issued = await _credentials.IssueForNameAsync(row.ClientName, ct);
            var client = new Client
            {
                Name = row.ClientName,
                IdNumber = await _referenceNumbers.GenerateClientIdNumberAsync(ct),
                AccountRefId = await _accountRefIds.GenerateForClientAsync(ct),
                PhoneNumber = row.PhoneNumber,
                Email = row.Email,
                Office = row.Office,
                Location = row.Location,
                Region = row.Region,
                Zone = row.Zone,
                City = row.City,
                Woreda = row.Woreda,
                KycType = row.KycType,
                KycContact = row.KycContact,
                ItSupportContact = string.IsNullOrWhiteSpace(row.ItSupportContact) ? null : row.ItSupportContact,
                AccountStatus = ClientAccountStatus.Approved,
                OnboardingDate = DateOnly.FromDateTime(DateTime.UtcNow),
                Username = issued.Username,
                PasswordHash = PasswordHasher.Hash(issued.OneTimePassword),
                MustChangePassword = true,
            };
            // Not saved yet — added to pendingEntitiesThisRow and staged
            // via _db.Add() now, but the actual SaveChangesAsync for this
            // whole row happens once, at the very end, after the
            // SystemProduct and (optional) Agreement are staged too. Every
            // entity here uses a client-generated Guid Id (see each
            // entity's `= Guid.NewGuid()` default), so later entities can
            // safely reference client.Id / systemProduct.Id before
            // anything hits the database. This keeps one row's three
            // possible inserts atomic — either all of them land, or (on a
            // save failure) none of them do — without needing an explicit
            // transaction API that this codebase's IAppDbContext doesn't
            // expose.
            _db.Add(client);
            pendingEntitiesThisRow.Add(client);

            clientId = client.Id;
            username = issued.Username;
            clientsCreatedThisRun[row.ClientName] = (clientId, username);

            // Deliberately no SendCredentialEmailAsync call here — see
            // the class-level doc comment. The plaintext OTP isn't
            // retained anywhere either; credentials get reissued (fresh
            // OTP, same flow as any client) via the existing "Resend
            // credential email" action whenever the Admin is ready to
            // actually hand the account to that client.
        }

        // --- Create the SystemProduct ---
        var catalogItemId = await _db.ProductCatalogItems
            .Where(p => p.IsActive && p.Name.ToLower() == row.SystemProductName.ToLower())
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(ct);

        var deploymentDate = ParseOptionalDate(row.DeploymentDate, "DeploymentDate");
        var productExpiryDate = ParseOptionalDate(row.ProductExpiryDate, "ProductExpiryDate");

        var systemProduct = new SystemProduct
        {
            ClientId = clientId,
            ReferenceNumber = await _referenceNumbers.GenerateSystemProductRefAsync(ct),
            Name = row.SystemProductName,
            Description = string.IsNullOrWhiteSpace(row.SystemProductDescription) ? null : row.SystemProductDescription,
            DeploymentDate = deploymentDate,
            ExpiryDate = productExpiryDate,
            CatalogItemId = catalogItemId,
            TrainingCompletionStatus = trainingCompleted ? TrainingCompletionStatus.Completed : TrainingCompletionStatus.NotStarted,
        };
        _db.Add(systemProduct);
        pendingEntitiesThisRow.Add(systemProduct);

        // --- Optionally create the Agreement ---
        Guid? agreementId = null;
        if (!string.IsNullOrWhiteSpace(row.AgreementType))
        {
            RequiredFieldValidator.EnsureAllPresent(
                ("Row's AgreementPlace (required when AgreementType is set)", row.AgreementPlace),
                ("Row's SignDate (required when AgreementType is set)", row.SignDate),
                ("Row's SupportWindowMonths (required when AgreementType is set)", row.SupportWindowMonths),
                ("Row's BillingTier (required when AgreementType is set)", row.BillingTier)
            );

            var agreementType = await _db.AgreementTypes
                .FirstOrDefaultAsync(t => t.Name.ToLower() == row.AgreementType.ToLower(), ct)
                ?? throw new InvalidOperationException($"AgreementType \"{row.AgreementType}\" was not found — check Settings for the exact configured name.");

            if (agreementType.Name == AgreementTypeNames.Support && !trainingCompleted)
                throw new InvalidOperationException(
                    "Can't create a Support agreement — TrainingCompleted is No for this row. Either mark training completed on paper first, or import this row without an AgreementType and add the agreement manually later.");

            var signDate = ParseRequiredDate(row.SignDate, "SignDate");
            var agreementExpiryDate = ParseOptionalDate(row.AgreementExpiryDate, "AgreementExpiryDate") ?? signDate.AddYears(1);

            if (!int.TryParse(row.SupportWindowMonths, out var supportWindowMonths) || supportWindowMonths <= 0)
                throw new ValidationException($"SupportWindowMonths \"{row.SupportWindowMonths}\" must be a whole number greater than zero.");

            if (!Enum.TryParse<BillingTier>(row.BillingTier, ignoreCase: true, out var billingTier))
                throw new ValidationException($"BillingTier \"{row.BillingTier}\" must be one of: Basic, Intermediate, Advanced.");

            var agreement = new Agreement
            {
                SystemProductId = systemProduct.Id,
                AgreementTypeId = agreementType.Id,
                ScannedFileUrl = null,
                DocumentNumber = await _referenceNumbers.GenerateAgreementDocumentNumberAsync(ct),
                AgreementPlace = row.AgreementPlace!,
                SignDate = signDate,
                ExpiryDate = agreementExpiryDate,
                SupportWindowMonths = supportWindowMonths,
                BillingTier = billingTier,
                Details = string.IsNullOrWhiteSpace(row.AgreementDetails) ? row.PaperReferenceNote : row.AgreementDetails,
            };
            _db.Add(agreement);
            pendingEntitiesThisRow.Add(agreement);
            agreementId = agreement.Id;
        }

        // Single save for everything staged above — see the comment by
        // the Client's _db.Add() call for why this is deferred to here.
        await _db.SaveChangesAsync(ct);

        return new ClientImportRowResult(
            row.RowNumber, row.ClientName, row.SystemProductName, true, null, false,
            clientId, systemProduct.Id, agreementId, username);
    }

    private static bool ParseYesNo(string value, string fieldName)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "yes" or "y" or "true" or "1" => true,
            "no" or "n" or "false" or "0" => false,
            _ => throw new ValidationException($"{fieldName} \"{value}\" must be Yes or No."),
        };
    }

    private static DateOnly? ParseOptionalDate(string? value, string fieldName) =>
        string.IsNullOrWhiteSpace(value) ? null : ParseRequiredDate(value, fieldName);

    private static DateOnly ParseRequiredDate(string value, string fieldName)
    {
        if (DateOnly.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return parsed;
        if (DateOnly.TryParse(value.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
            return parsed;
        throw new ValidationException($"{fieldName} \"{value}\" isn't a valid date — use YYYY-MM-DD.");
    }
}
