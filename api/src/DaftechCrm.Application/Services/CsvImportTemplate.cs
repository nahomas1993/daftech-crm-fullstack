using System.Text;

namespace DaftechCrm.Application.Services;

/// <summary>
/// Generates the starting-point CSV file an Admin downloads before
/// transcribing paper client records — see ClientImportController and
/// CsvImportParser (which reads whatever comes back from this template,
/// filled in). Two example rows are included: one client with an
/// existing Support agreement, one with just a system/product and no
/// agreement yet — the two shapes a real paper record is likely to take.
/// </summary>
public static class CsvImportTemplate
{
    public static string Generate()
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", CsvImportParser.ExpectedColumns));

        // Row 1: a client with training already done on paper and an
        // existing signed Support agreement.
        AppendRow(sb,
            "Example Trading PLC", "0911223344", "example.trading@email.com", "Bole Branch", "Addis Ababa",
            "Addis Ababa", "Bole", "Addis Ababa", "Woreda 03",
            "Business License", "Ato Example — 0911223344", "",
            "Branch POS System", "Point-of-sale system for the Bole branch", "2023-05-01", "",
            "Yes",
            "Support", "Addis Ababa Head Office", "2023-05-15", "2025-05-15", "12", "Intermediate", "",
            "Paper folder 14, page 3"
        );

        // Row 2: a client with a system/product on paper but no
        // agreement recorded yet (leave the agreement columns blank —
        // AgreementType blank means "skip creating an agreement for this
        // row", it can be added later from the Client Detail page).
        // Region/Zone/City/Woreda are still required even though the
        // agreement columns are blank.
        AppendRow(sb,
            "Sample Retailers", "0922334455", "sample.retailers@email.com", "Merkato Branch", "Addis Ababa",
            "Addis Ababa", "Addis Ketema", "Addis Ababa", "Woreda 07",
            "Business License", "W/ro Sample — 0922334455", "",
            "HR Portal", "", "2024-01-10", "",
            "No",
            "", "", "", "", "", "", "",
            "Paper folder 14, page 7"
        );

        return sb.ToString();
    }

    private static void AppendRow(StringBuilder sb, params string[] fields)
    {
        sb.AppendLine(string.Join(",", fields.Select(Escape)));
    }

    private static string Escape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
