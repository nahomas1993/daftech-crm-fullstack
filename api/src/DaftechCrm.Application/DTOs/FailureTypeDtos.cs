using DaftechCrm.Domain.Entities;

namespace DaftechCrm.Application.DTOs;

public record FailureTypeDto(Guid Id, string Name, int DurationValue, DurationUnit DurationUnit);

public record CreateFailureTypeRequest(string Name, int DurationValue, DurationUnit DurationUnit);
public record UpdateFailureTypeRequest(string Name, int DurationValue, DurationUnit DurationUnit);
