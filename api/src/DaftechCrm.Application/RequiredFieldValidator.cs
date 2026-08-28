namespace DaftechCrm.Application;

/// <summary>
/// Small shared helper for "this field is required" checks across
/// Application services (client registration, employee registration,
/// system/product creation, agreements, tickets, etc.). Centralized so
/// every form's server-side validation raises the same
/// <see cref="ValidationException"/> (-> HTTP 400 with a clear message)
/// instead of each service reinventing its own null/whitespace checks —
/// and so the message text stays consistent no matter which form it came
/// from.
///
/// This is a defense-in-depth backstop: the Angular forms this backs
/// (client registration, employee registration, system/product creation,
/// agreements, ticket submission, etc.) already block submission
/// client-side and name the missing field inline — see each component's
/// own required-field checks. This validator exists so the same rule
/// holds even if a request reaches the API directly.
/// </summary>
public static class RequiredFieldValidator
{
    /// <summary>
    /// Throws a <see cref="ValidationException"/> naming the first blank
    /// required field, if any. Pass fields in the order they appear on the
    /// form, so the message matches what the person would see reading
    /// top-to-bottom.
    /// </summary>
    /// <param name="fields">(display label, value) pairs for every required field on the form.</param>
    public static void EnsureAllPresent(params (string Label, string? Value)[] fields)
    {
        foreach (var (label, value) in fields)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ValidationException($"{label} is required.");
        }
    }

    /// <summary>
    /// Client and employee registration/edit currently only accept
    /// @gmail.com addresses — matches the live "Invalid email" hint shown
    /// under the email box on both forms in the Angular app (see
    /// core/email-validation.ts). Kept as its own check, separate from
    /// EnsureAllPresent, since this validates shape rather than presence
    /// — call EnsureAllPresent first so a blank email reports as
    /// "required" rather than "invalid".
    /// </summary>
    public static void EnsureGmailAddress(string email)
    {
        if (!email.Trim().EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("Invalid email — please use a @gmail.com address.");
    }
}
