namespace DaftechCrm.Application;

/// <summary>
/// Thrown when an operation targets a ticket Id that does not exist —
/// including a ticket that existed when first read but was deleted before
/// the write completed. Kept distinct from ConcurrencyConflictException so
/// TicketsController can map it to 404 Not Found instead of 409 Conflict;
/// the two used to be indistinguishable (both raised as
/// DbUpdateConcurrencyException / a generic InvalidOperationException),
/// which is why a missing ticket could previously surface as a confusing
/// "updated by someone else" message.
/// </summary>
public class TicketNotFoundException : Exception
{
    public TicketNotFoundException(Guid ticketId)
        : base($"Ticket {ticketId} was not found.") { }
}
