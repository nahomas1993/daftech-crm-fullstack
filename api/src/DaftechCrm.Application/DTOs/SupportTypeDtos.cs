namespace DaftechCrm.Application.DTOs;

public record SupportTypeDto(Guid Id, string Name, string? Description, decimal AdditionalFee);

public record CreateSupportTypeRequest(string Name, string? Description, decimal AdditionalFee);

public record UpdateSupportTypeRequest(string Name, string? Description, decimal AdditionalFee);
