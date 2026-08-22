namespace DaftechCrm.Application;

/// <summary>
/// Thrown by a service when caller-supplied input fails a business rule
/// (e.g. a field exceeds its length limit) — distinct from
/// InvalidOperationException, which controllers here map to 404 Not
/// Found for "record doesn't exist" cases. This maps to 400 Bad Request.
/// </summary>
public class ValidationException : Exception
{
    public ValidationException(string message) : base(message) { }
}
