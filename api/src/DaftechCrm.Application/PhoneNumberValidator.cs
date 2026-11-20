using System.Text.RegularExpressions;

namespace DaftechCrm.Application;

/// <summary>
/// Ethiopian phone numbers, one rule for every entry point (client and
/// employee forms, CSV import). Accepts mobile and landline/company
/// numbers in any of these spellings, with or without spaces, dashes,
/// dots or brackets:
///   0911223344        local mobile
///   0111136187        local landline (Addis Ababa 011)
///   +251911223344     international mobile
///   +251111136187     international landline / company number
///   251111136187      international without the plus
/// Every accepted number is STORED as +251XXXXXXXXX (nine digits after the
/// country code), so the same number is never saved two different ways and
/// searching stays predictable.
///
/// The nine digits start with 1-5 (landline area codes such as 11, 22, 25,
/// 58) or 7 / 9 (mobile).
/// </summary>
public static partial class PhoneNumberValidator
{
    [GeneratedRegex(@"^(?:\+251|251|0)([1-579]\d{8})$")]
    private static partial Regex EthiopianNumber();

    [GeneratedRegex(@"[\s\-\.\(\)]")]
    private static partial Regex Separators();

    /// <summary>Returns the number as +251XXXXXXXXX, or null when it isn't a valid Ethiopian number.</summary>
    public static string? TryNormalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var compact = Separators().Replace(raw.Trim(), "");
        var match = EthiopianNumber().Match(compact);
        return match.Success ? "+251" + match.Groups[1].Value : null;
    }

    /// <summary>Same as TryNormalize but throws a ValidationException (shown to the user as-is) for a missing or invalid number.</summary>
    public static string Normalize(string? raw, string fieldLabel = "Phone Number")
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new ValidationException($"{fieldLabel} is required.");

        return TryNormalize(raw)
            ?? throw new ValidationException(
                $"{fieldLabel} is not valid. Use an Ethiopian number such as 0911223344 or +251111136187.");
    }
}
