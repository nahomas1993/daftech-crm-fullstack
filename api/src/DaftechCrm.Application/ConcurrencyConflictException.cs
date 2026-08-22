namespace DaftechCrm.Application;

/// <summary>
/// Thrown when a write genuinely conflicts with another concurrent write
/// to the same record — distinct from InvalidOperationException, which
/// controllers here map to 404 Not Found for "record doesn't exist"
/// cases. Conflating the two made a write conflict look identical to the
/// record having vanished, which was confusing and outright wrong for
/// callers reacting to the status code. This maps to 409 Conflict.
/// </summary>
public class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message) : base(message) { }
}
