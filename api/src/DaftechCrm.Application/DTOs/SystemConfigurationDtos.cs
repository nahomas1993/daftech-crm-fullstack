namespace DaftechCrm.Application.DTOs;

/// <summary>One configurable value shown on the Admin Configuration page.</summary>
public record SystemSettingDto(
    string Key,
    string Value,
    string Category,
    string Label,
    string Description,
    string ValueType, // "int" | "bool" | "string" — tells the frontend which input control to render
    DateTimeOffset? UpdatedAt,
    string? UpdatedByName
);

public record UpdateSystemSettingRequest(string Key, string Value);

/// <summary>Batch update — the Configuration page saves a whole section at once.</summary>
public record UpdateSystemSettingsRequest(List<UpdateSystemSettingRequest> Settings);
