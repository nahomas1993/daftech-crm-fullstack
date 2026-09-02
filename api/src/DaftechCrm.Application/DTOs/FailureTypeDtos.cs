using DaftechCrm.Domain.Entities;

namespace DaftechCrm.Application.DTOs;

public record FailureTypeDto(Guid Id, DaftechCrm.Domain.Enums.TicketCategory Category, string Name, string? Description, decimal BasePrice, int DurationValue, DurationUnit DurationUnit, string? RequiredSpecialization, TicketPriority DefaultPriority);

public record CreateFailureTypeRequest(DaftechCrm.Domain.Enums.TicketCategory Category, string Name, string? Description, decimal BasePrice, int DurationValue, DurationUnit DurationUnit, string? RequiredSpecialization, TicketPriority DefaultPriority);
public record UpdateFailureTypeRequest(DaftechCrm.Domain.Enums.TicketCategory Category, string Name, string? Description, decimal BasePrice, int DurationValue, DurationUnit DurationUnit, string? RequiredSpecialization, TicketPriority DefaultPriority);
