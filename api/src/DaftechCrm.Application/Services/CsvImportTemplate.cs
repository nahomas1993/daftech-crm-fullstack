using System.Text;

namespace DaftechCrm.Application.Services;

/// <summary>
/// Generates the starting-point CSV file an Admin downloads before
/// transcribing paper client records — see ClientImportController and
/// CsvImportParser (which reads whatever comes back from this template,
/// filled in). Two example rows are included: one client with an
/// existing Support agreement, one with just a system/product and no
/// agreement yet — the two shapes a real paper record is likely to take.
///
/// Also includes the repeating Training1.../Training2... column groups
/// (see CsvImportParser.MaxTrainingSlots) so historical training sessions
/// can be transcribed alongside the client/product/agreement in the same
/// row. TrainingXName must match a configured Training Item exactly (see
/// Settings); TrainingXTrainerName is optional, since these paper records
/// are almost always being entered well after the session happened and
/// often don't say who conducted it.
/// </summary>
public static class CsvImportTemplate
{
    public static string Generate()
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", CsvImportParser.ExpectedColumns.Concat(TrainingColumnNames())));

        // Row 1: a client with training already done on paper (two
        // sessions, one with a named trainer and one without — the paper
        // record didn't always note who conducted it) and an existing
        // signed Support agreement.
        AppendRow(sb,
            new[] {
                "Example Trading PLC", "0911223344", "example.trading@email.com", "Bole Branch", "Addis Ababa",
                "Addis Ababa", "Bole", "Addis Ababa", "Woreda 03",
                "Business License", "Ato Example — 0911223344", "",
                "Branch POS System", "Point-of-sale system for the Bole branch", "2023-05-01", "",
                "Yes",
                "Support", "Addis Ababa Head Office", "2023-05-15", "2025-05-15", "12", "Intermediate", "",
                "Paper folder 14, page 3",
            },
            TrainingSlot("Attendance", "Selam Tesfaye", "2023-05-10", "Covered POS attendance workflow with branch staff."),
            TrainingSlot("System Walkthrough", "", "2023-05-12", "Walked through the full POS system end to end; paper record didn't note the trainer's name.")
        );

        // Row 2: a client with a system/product on paper but no
        // agreement recorded yet (leave the agreement columns blank —
        // AgreementType blank means "skip creating an agreement for this
        // row", it can be added later from the Client Detail page).
        // Region/Zone/City/Woreda are still required even though the
        // agreement columns are blank. No trainings on this row either —
        // leave every TrainingXName blank to skip that slot entirely.
        AppendRow(sb,
            new[] {
                "Sample Retailers", "0922334455", "sample.retailers@email.com", "Merkato Branch", "Addis Ababa",
                "Addis Ababa", "Addis Ketema", "Addis Ababa", "Woreda 07",
                "Business License", "W/ro Sample — 0922334455", "",
                "HR Portal", "", "2024-01-10", "",
                "No",
                "", "", "", "", "", "", "",
                "Paper folder 14, page 7",
            }
        );

        return sb.ToString();
    }

    private static IEnumerable<string> TrainingColumnNames()
    {
        for (var slot = 1; slot <= CsvImportParser.MaxTrainingSlots; slot++)
        {
            yield return $"Training{slot}Name";
            yield return $"Training{slot}TrainerName";
            yield return $"Training{slot}Date";
            yield return $"Training{slot}Description";
        }
    }

    /// <summary>One filled training slot's four cell values, in Name/TrainerName/Date/Description order — pass however many are actually used to AppendRow, the rest are padded blank.</summary>
    private static string[] TrainingSlot(string name, string trainerName, string date, string description) =>
        [name, trainerName, date, description];

    /// <summary>
    /// Writes one row: the fixed client/product/agreement columns plus up
    /// to CsvImportParser.MaxTrainingSlots training slots (from
    /// TrainingSlot(...) calls) — any slots not passed are padded with
    /// four blank cells each so every row lines up under the header's
    /// fixed Training{N}... column count.
    /// </summary>
    private static void AppendRow(StringBuilder sb, string[] baseFields, params string[][] trainingSlots)
    {
        var allFields = new List<string>(baseFields);
        for (var slot = 0; slot < CsvImportParser.MaxTrainingSlots; slot++)
        {
            allFields.AddRange(slot < trainingSlots.Length ? trainingSlots[slot] : ["", "", "", ""]);
        }
        sb.AppendLine(string.Join(",", allFields.Select(Escape)));
    }

    private static string Escape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
