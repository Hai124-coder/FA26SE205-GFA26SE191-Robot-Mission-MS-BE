using System.ComponentModel.DataAnnotations;
using RobotMissionMS.Business.Features.Locations.DTOs;
using RobotMissionMS.DataAccess.Enums;
using RobotMissionMS.DataAccess.Repositories.Interfaces;

namespace RobotMissionMS.Business.Features.Locations.Services;

public sealed class LocationService(ILocationRepository repository) : ILocationService
{
    public async Task<LocationListResult> GetLocationsAsync(LocationListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!Validator.TryValidateObject(query, new ValidationContext(query), null, validateAllProperties: true))
            return LocationListResult.Failure(LocationListError.InvalidQuery);

        LocationKind? kind = null;
        if (!string.IsNullOrWhiteSpace(query.Kind))
        {
            if (!TryParseKind(query.Kind, out var parsedKind))
                return LocationListResult.Failure(LocationListError.InvalidQuery);

            kind = parsedKind;
        }

        var rows = await repository.GetLocationsAsync(query.Floor?.Trim(), kind, cancellationToken);
        return LocationListResult.Success(rows.Select(row => new LocationResponse
        {
            Id = row.Id,
            Code = row.Code,
            Name = row.Name,
            Description = row.Description,
            Floor = row.FloorCode,
            FloorName = row.FloorName,
            Kind = ToDatabaseName(row.Kind),
            MapNodeCode = row.MapNodeCode,
            IsRestrictedArea = row.IsRestrictedArea,
            RestrictedHours = row.RestrictedHours,
            IsActive = row.IsActive
        }).ToList());
    }

    private static bool TryParseKind(string value, out LocationKind kind)
    {
        kind = value.Trim().ToUpperInvariant() switch
        {
            "PHARMACY" => LocationKind.Pharmacy,
            "STORAGE" => LocationKind.Storage,
            "SPECIMEN" => LocationKind.Specimen,
            "DEPT" => LocationKind.Dept,
            "WASTE_STATION" => LocationKind.WasteStation,
            "WASTE_POINT" => LocationKind.WastePoint,
            "CHARGER" => LocationKind.Charger,
            "CLEAN_STATION" => LocationKind.CleanStation,
            "CORRIDOR" => LocationKind.Corridor,
            _ => default
        };

        return value.Trim().ToUpperInvariant() is "PHARMACY" or "STORAGE" or "SPECIMEN" or "DEPT"
            or "WASTE_STATION" or "WASTE_POINT" or "CHARGER" or "CLEAN_STATION" or "CORRIDOR";
    }

    private static string ToDatabaseName(LocationKind kind) => kind switch
    {
        LocationKind.Pharmacy => "PHARMACY",
        LocationKind.Storage => "STORAGE",
        LocationKind.Specimen => "SPECIMEN",
        LocationKind.Dept => "DEPT",
        LocationKind.WasteStation => "WASTE_STATION",
        LocationKind.WastePoint => "WASTE_POINT",
        LocationKind.Charger => "CHARGER",
        LocationKind.CleanStation => "CLEAN_STATION",
        LocationKind.Corridor => "CORRIDOR",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown location kind.")
    };
}
