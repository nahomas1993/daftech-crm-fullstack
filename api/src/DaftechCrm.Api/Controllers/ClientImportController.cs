using DaftechCrm.Api.Auth;
using DaftechCrm.Application;
using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DaftechCrm.Api.Controllers;

/// <summary>
/// Bulk-imports old, paper-based client records from a CSV — for
/// migrating the hundreds of existing clients that predate this system,
/// rather than re-keying each one through the registration UI by hand.
/// See ClientImportService for the full behavior (duplicate handling,
/// training-completion precondition on Support agreements, credential
/// issuance without auto-sending emails).
/// </summary>
[ApiController]
[Route("api/client-import")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class ClientImportController : ControllerBase
{
    private readonly ClientImportService _importer;
    public ClientImportController(ClientImportService importer) => _importer = importer;

    /// <summary>
    /// Downloadable starting-point CSV with the exact expected column
    /// headers and two example rows (one with an agreement, one without)
    /// — so whoever is transcribing paper records has a concrete template
    /// to fill in rather than needing to know the column names by heart.
    /// </summary>
    [HttpGet("template")]
    public IActionResult DownloadTemplate()
    {
        var csv = CsvImportTemplate.Generate();
        var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
        return File(bytes, "text/csv", "daftech-client-import-template.csv");
    }

    /// <summary>
    /// Imports every row of the uploaded CSV. Always returns 200 with a
    /// per-row report (see ClientImportResult) — a row failing doesn't
    /// fail the request, so the response is the actual audit trail of
    /// what happened to each of potentially hundreds of rows in one go.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<ClientImportResult>> Import(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest("No file was provided.");

        if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Please upload a .csv file — export from Excel/Google Sheets as CSV first.");

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await _importer.ImportAsync(stream, ct);
            return Ok(result);
        }
        catch (ValidationException ex)
        {
            // Only reached for file-level problems (missing columns, empty
            // file) — per-row problems are captured in the 200 response
            // above instead of thrown, see ClientImportService.
            return BadRequest(ex.Message);
        }
    }
}
