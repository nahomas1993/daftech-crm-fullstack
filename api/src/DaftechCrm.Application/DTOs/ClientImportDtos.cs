namespace DaftechCrm.Application.DTOs;

/// <summary>
/// One row of the client bulk-import CSV — old paper records being
/// migrated in bulk (see ClientImportService). Every field is a plain
/// string as parsed straight from the CSV cell; validation and type
/// conversion (dates, enums, booleans) happen in the service, so a bad
/// value in one row can be reported clearly against that row instead of
/// blowing up CSV parsing for the whole file.
///
/// One row = one client's one system/product (+ optional agreement).
/// A client with three systems/products on paper becomes three rows
/// sharing the same client columns — see ClientImportService for how
/// repeated client rows are matched back to a single client record vs.
/// flagged as a possible duplicate.
/// </summary>
public record ClientImportRow(
    int RowNumber,

    // --- Client fields (required columns match RegisterClientRequest) ---
    string ClientName,
    string PhoneNumber,
    string Email,
    string Office,
    string Location,
    string Region,
    string Zone,
    string City,
    string Woreda,
    string KycType,
    string KycContact,
    string? ItSupportContact,

    // --- System/Product fields ---
    string SystemProductName,
    string? SystemProductDescription,
    string? DeploymentDate,
    string? ProductExpiryDate,
    /// <summary>"Yes"/"No" — whether training was already completed on paper. Required so the importer knows whether a Support agreement is even allowed (see AgreementService.CreateAsync's precondition).</summary>
    string TrainingCompleted,

    // --- Agreement fields (all optional together — leave AgreementType blank to skip creating an agreement for this row) ---
    string? AgreementType,
    string? AgreementPlace,
    string? SignDate,
    string? AgreementExpiryDate,
    string? SupportWindowMonths,
    string? BillingTier,
    string? AgreementDetails,

    /// <summary>Free-text pointer back to the physical paper record (folder/box/page number) — purely for audit trail, not used by any logic.</summary>
    string? PaperReferenceNote
);

/// <summary>Outcome for a single row after ClientImportService processes the file — every row gets exactly one of these, success or failure, so nothing is silently dropped.</summary>
public record ClientImportRowResult(
    int RowNumber,
    string ClientName,
    string SystemProductName,
    bool Success,
    /// <summary>Null on success. On failure, names the specific problem (matches the RequiredFieldValidator style: says exactly what's wrong with this row).</summary>
    string? Error,
    /// <summary>True when this row's client name matched an existing client or an earlier row in the same file closely enough to be flagged rather than auto-merged — see ClientImportService. The row is NOT imported when this is true; it's held for manual review.</summary>
    bool FlaggedAsDuplicate,
    Guid? ClientId = null,
    Guid? SystemProductId = null,
    Guid? AgreementId = null,
    string? IssuedUsername = null,
    /// <summary>
    /// The plaintext one-time password issued for a newly created client
    /// on this row — null for a row that attached to a client created
    /// earlier in the same run (only the client's first row actually
    /// issues credentials) or for a failed/duplicate row. This is the
    /// ONLY place the plaintext OTP is ever surfaced: the hash is what
    /// gets persisted, so if this response isn't read/saved now, the
    /// Admin has to reissue a fresh one later via "Resend credential
    /// email" rather than recover this one.
    /// </summary>
    string? IssuedOneTimePassword = null,
    /// <summary>
    /// Only set on a row that created a new client (mirrors
    /// IssuedOneTimePassword's null pattern) — the email and location
    /// fields the import saved for them, so the results table can show
    /// what actually landed on the client record without a second lookup
    /// per row.
    /// </summary>
    string? Email = null,
    string? Region = null,
    string? Zone = null,
    string? City = null,
    string? Woreda = null
);

/// <summary>Full report returned after a bulk import run — see ClientImportService.ImportAsync.</summary>
public record ClientImportResult(
    int TotalRows,
    int SucceededCount,
    int FailedCount,
    int FlaggedDuplicateCount,
    IReadOnlyList<ClientImportRowResult> Rows
);
