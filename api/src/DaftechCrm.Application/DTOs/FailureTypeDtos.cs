using DaftechCrm.Domain.Entities;

namespace DaftechCrm.Application.DTOs;

public record FailureTypeDto(Guid Id, DaftechCrm.Domain.Enums.TicketCategory Category, string Name, string? Description, int DurationValue, DurationUnit DurationUnit);

public record CreateFailureTypeRequest(DaftechCrm.Domain.Enums.TicketCategory Category, string Name, string? Description, int DurationValue, DurationUnit DurationUnit);
public record UpdateFailureTypeRequest(DaftechCrm.Domain.Enums.TicketCategory Category, string Name, string? Description, int DurationValue, DurationUnit DurationUnit);
