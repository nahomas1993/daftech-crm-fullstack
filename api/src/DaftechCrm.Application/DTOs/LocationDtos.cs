using DaftechCrm.Domain.Entities;

namespace DaftechCrm.Application.DTOs;

public record LocationEntryDto(Guid Id, LocationType Type, string Name);

/// <summary>All six dropdown/checklist lists in one response, so the client and employee registration forms each load what they need in a single call.</summary>
public record LocationOptionsDto(
    IReadOnlyList<LocationEntryDto> Regions,
    IReadOnlyList<LocationEntryDto> Zones,
    IReadOnlyList<LocationEntryDto> Cities,
    IReadOnlyList<LocationEntryDto> Woredas,
    IReadOnlyList<LocationEntryDto> Specializations,
    IReadOnlyList<LocationEntryDto> CustomRoles
);

public record CreateLocationEntryRequest(LocationType Type, string Name);
public record UpdateLocationEntryRequest(string Name);
