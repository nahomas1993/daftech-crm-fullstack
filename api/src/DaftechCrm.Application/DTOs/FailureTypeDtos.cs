using DaftechCrm.Domain.Entities;

namespace DaftechCrm.Application.DTOs;

public record FailureTypeDto(Guid Id, string Name, string? Description, int DurationValue, DurationUnit DurationUnit);

public record CreateFailureTypeRequest(string Name, string? Description, int DurationValue, DurationUnit DurationUnit);
public record UpdateFailureTypeRequest(string Name, string? Description, int DurationValue, DurationUnit DurationUnit);
