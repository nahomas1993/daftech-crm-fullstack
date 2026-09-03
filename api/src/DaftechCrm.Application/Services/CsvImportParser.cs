using DaftechCrm.Application.DTOs;

namespace DaftechCrm.Application.Services;

/// <summary>
/// Small hand-written CSV reader for ClientImportService — no external
/// CSV library dependency, since this project can't be build-tested in
/// every environment it's edited in and a new NuGet package is an easy
/// way to break a deploy silently. Handles the RFC 4180 basics this
/// feature actually needs: comma-separated fields, double-quoted fields
/// (so a client name or address containing a comma still parses
/// correctly), and "" as an escaped quote inside a quoted field. Not a
/// general-purpose CSV engine — just enough for a template this specific.
/// </summary>
public static class CsvImportParser
{
    /// <summary>
    /// Column order defines what CsvImportTemplate.cs generates and what
    /// this parser expects back — see ClientImportRow for what each
    /// column means. Header row is required and matched case-insensitively
    /// against these names so a template opened/re-saved in Excel (which
    /// sometimes reorders nothing but occasionally changes casing) still
    /// works; column ORDER in the file does not need to match this list,
    /// each column is looked up by header name.
    /// </summary>
    public static readonly string[] ExpectedColumns =
    [
        "ClientName", "PhoneNumber", "Email", "Office", "Location",
        "Region", "Zone", "City", "Woreda", "KycType", "KycContact", "ItSupportContact",
        "SystemProductName", "SystemProductDescription", "DeploymentDate", "ProductExpiryDate", "TrainingCompleted",
        "AgreementType", "AgreementPlace", "SignDate", "AgreementExpiryDate", "SupportWindowMonths", "BillingTier", "AgreementDetails",
        "PaperReferenceNote",
    ];

    /// <summary>
    /// How many repeated Training{N}... column groups CsvImportTemplate.cs
    /// generates and this parser will look for (Training1Name through
    /// Training{MaxTrainingSlots}Name, etc.) — see ReadTrainingEntries.
    /// Not a hard cap on trainings per client: a client whose paper record
    /// has more sessions than this simply gets a second CSV row for the
    /// same SystemProductName with the remaining Training columns filled
    /// in and every other column repeated, same as the existing multi-row
    /// pattern for a client with several systems/products.
    /// </summary>
    public const int MaxTrainingSlots = 5;

    public static List<ClientImportRow> Parse(Stream csvStream)
    {
        using var reader = new StreamReader(csvStream);
        var lines = ReadLogicalLines(reader);

        if (lines.Count == 0)
            throw new ValidationException("The uploaded file is empty.");

        var header = SplitLine(lines[0]);
        var columnIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < header.Count; i++) columnIndex[header[i].Trim()] = i;

        var missingColumns = ExpectedColumns.Where(c => !columnIndex.ContainsKey(c)).ToList();
        if (missingColumns.Count > 0)
            throw new ValidationException(
                $"The uploaded file is missing required column(s): {string.Join(", ", missingColumns)}. " +
                "Download the template from this page and fill it in without renaming or removing columns.");

        string? Get(List<string> fields, string column)
        {
            if (!columnIndex.TryGetValue(column, out var idx)) return null; // optional column (e.g. a TrainingN group) simply absent from this file
            if (idx >= fields.Count) return null;
            var value = fields[idx].Trim();
            return value.Length == 0 ? null : value;
        }

        var rows = new List<ClientImportRow>();
        for (var lineIndex = 1; lineIndex < lines.Count; lineIndex++)
        {
            if (string.IsNullOrWhiteSpace(lines[lineIndex])) continue; // tolerate trailing blank lines
            var fields = SplitLine(lines[lineIndex]);
            var rowNumber = lineIndex + 1; // 1-based, matches what someone sees as the Excel row number (header is row 1)

            rows.Add(new ClientImportRow(
                rowNumber,
                Get(fields, "ClientName") ?? "",
                Get(fields, "PhoneNumber") ?? "",
                Get(fields, "Email") ?? "",
                Get(fields, "Office") ?? "",
                Get(fields, "Location") ?? "",
                Get(fields, "Region") ?? "",
                Get(fields, "Zone") ?? "",
                Get(fields, "City") ?? "",
                Get(fields, "Woreda") ?? "",
                Get(fields, "KycType") ?? "",
                Get(fields, "KycContact") ?? "",
                Get(fields, "ItSupportContact"),
                Get(fields, "SystemProductName") ?? "",
                Get(fields, "SystemProductDescription"),
                Get(fields, "DeploymentDate"),
                Get(fields, "ProductExpiryDate"),
                Get(fields, "TrainingCompleted") ?? "",
                Get(fields, "AgreementType"),
                Get(fields, "AgreementPlace"),
                Get(fields, "SignDate"),
                Get(fields, "AgreementExpiryDate"),
                Get(fields, "SupportWindowMonths"),
                Get(fields, "BillingTier"),
                Get(fields, "AgreementDetails"),
                Get(fields, "PaperReferenceNote"),
                ReadTrainingEntries(fields, Get)
            ));
        }

        return rows;
    }

    /// <summary>
    /// Gathers this row's Training1Name/Training1TrainerName/Training1Date/
    /// Training1Description through Training{MaxTrainingSlots}... into a
    /// list — see TrainingImportEntry. A slot only produces an entry when
    /// its Name column is filled in; TrainerName, Date, and Description
    /// within a used slot are validated by ClientImportService, not here
    /// (same division of labor as every other field: this parser only
    /// reads cells, the service applies business rules row by row so one
    /// bad slot's error names the specific row and training number).
    /// Slots don't need to be contiguous — Training1 and Training3 filled
    /// in with Training2 blank still reads two entries — so deleting a
    /// row's middle training later doesn't require renumbering the rest.
    /// </summary>
    private static List<TrainingImportEntry> ReadTrainingEntries(List<string> fields, Func<List<string>, string, string?> get)
    {
        var entries = new List<TrainingImportEntry>();
        for (var slot = 1; slot <= MaxTrainingSlots; slot++)
        {
            var name = get(fields, $"Training{slot}Name");
            if (name is null) continue; // empty slot — not every row uses all MaxTrainingSlots

            entries.Add(new TrainingImportEntry(
                name,
                get(fields, $"Training{slot}TrainerName"),
                get(fields, $"Training{slot}Date") ?? "",
                get(fields, $"Training{slot}Description") ?? ""
            ));
        }
        return entries;
    }

    /// <summary>
    /// Joins raw file lines back into logical CSV rows first, because a
    /// quoted field is allowed to contain a literal newline (e.g. a
    /// multi-line AgreementDetails note) — a naive line-by-line read
    /// would otherwise split that single row in two.
    /// </summary>
    private static List<string> ReadLogicalLines(StreamReader reader)
    {
        var content = reader.ReadToEnd();
        var logicalLines = new List<string>();
        var current = new System.Text.StringBuilder();
        var insideQuotes = false;

        for (var i = 0; i < content.Length; i++)
        {
            var ch = content[i];
            if (ch == '"') insideQuotes = !insideQuotes;

            if (ch == '\n' && !insideQuotes)
            {
                logicalLines.Add(current.ToString().TrimEnd('\r'));
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }
        if (current.Length > 0) logicalLines.Add(current.ToString().TrimEnd('\r'));

        return logicalLines;
    }

    private static List<string> SplitLine(string line)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        var insideQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];

            if (insideQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"'); // escaped quote
                        i++;
                    }
                    else
                    {
                        insideQuotes = false;
                    }
                }
                else
                {
                    current.Append(ch);
                }
            }
            else if (ch == '"')
            {
                insideQuotes = true;
            }
            else if (ch == ',')
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }
        fields.Add(current.ToString());

        return fields;
    }
}
