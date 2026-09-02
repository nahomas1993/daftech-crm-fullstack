using DaftechCrm.Domain.Entities;

namespace DaftechCrm.Application.DTOs;

/// <summary>ParentId is set only for Zone (-> owning Region) and Woreda (-> owning Zone) rows; null for every other type.</summary>
public record LocationEntryDto(Guid Id, LocationType Type, string Name, Guid? ParentId);

/// <summary>All six dropdown/checklist lists in one response, so the client and employee registration forms each load what they need in a single call. Region/Zone/Woreda entries carry ParentId so the frontend can filter Zones by selected Region and Woredas by selected Zone.</summary>
public record LocationOptionsDto(
    IReadOnlyList<LocationEntryDto> Regions,
    IReadOnlyList<LocationEntryDto> Zones,
    IReadOnlyList<LocationEntryDto> Cities,
    IReadOnlyList<LocationEntryDto> Woredas,
    IReadOnlyList<LocationEntryDto> Specializations,
    IReadOnlyList<LocationEntryDto> CustomRoles
);

/// <summary>ParentId is required when Type is Zone (must reference an existing Region) or Woreda (must reference an existing Zone), and must be omitted/null for every other type.</summary>
public record CreateLocationEntryRequest(LocationType Type, string Name, Guid? ParentId = null);
public record UpdateLocationEntryRequest(string Name);
