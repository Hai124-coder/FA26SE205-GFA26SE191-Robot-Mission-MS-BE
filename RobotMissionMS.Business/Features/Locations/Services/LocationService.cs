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

    public async Task<CreateLocationResult> CreateLocationAsync(Guid createdByUserId, CreateLocationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (createdByUserId == Guid.Empty || !IsValid(request) || !TryParseKind(request.Kind, out var kind))
            return CreateLocationResult.Failure(CreateLocationError.InvalidRequest);
        var code = request.Code.Trim();
        if ((await repository.FindByCodeAsync(code, cancellationToken)).Count != 0)
            return CreateLocationResult.Failure(CreateLocationError.DuplicateCode);

        var floor = await repository.FindFloorAsync(request.Floor.Trim(), cancellationToken);
        if (floor is null) return CreateLocationResult.Failure(CreateLocationError.FloorNotFound);
        if (!floor.IsActive) return CreateLocationResult.Failure(CreateLocationError.FloorInactive);
        if (!await repository.PublishedMapContainsNodeAsync(floor.Id, request.MapNodeCode.Trim(), cancellationToken))
            return CreateLocationResult.Failure(CreateLocationError.MapNodeNotFound);

        var row = await repository.CreateLocationAsync(code, request.Name.Trim(), request.Description?.Trim(), floor.Id,
            floor.Code, floor.Name, kind, request.MapNodeCode.Trim(), request.IsRestrictedArea, request.RestrictedHours?.Trim(),
            createdByUserId, DateTime.UtcNow, cancellationToken);
        return CreateLocationResult.Success(ToResponse(row));
    }

    public async Task<UpdateLocationResult> UpdateLocationAsync(Guid updatedByUserId, string locationCode,
        UpdateLocationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (updatedByUserId == Guid.Empty || string.IsNullOrWhiteSpace(locationCode) || !IsValid(request)
            || !TryParseKind(request.Kind, out var kind)) return UpdateLocationResult.Failure(UpdateLocationError.InvalidRequest);
        var matches = await repository.FindByCodeAsync(locationCode.Trim(), cancellationToken);
        if (matches.Count == 0) return UpdateLocationResult.Failure(UpdateLocationError.NotFound);
        if (matches.Count > 1) return UpdateLocationResult.Failure(UpdateLocationError.AmbiguousCode);
        var existing = matches[0];
        if (!string.Equals(existing.FloorCode, request.Floor.Trim(), StringComparison.Ordinal))
            return UpdateLocationResult.Failure(UpdateLocationError.FloorChangeNotAllowed);
        if (!await repository.PublishedMapContainsNodeAsync(existing.FloorId, request.MapNodeCode.Trim(), cancellationToken))
            return UpdateLocationResult.Failure(UpdateLocationError.MapNodeNotFound);

        var row = await repository.UpdateLocationAsync(existing.Id, request.Name.Trim(), request.Description?.Trim(), kind,
            request.MapNodeCode.Trim(), request.IsRestrictedArea, request.RestrictedHours?.Trim(), updatedByUserId, DateTime.UtcNow,
            cancellationToken);
        return UpdateLocationResult.Success(ToResponse(row));
    }

    public async Task<UpdateLocationStatusResult> UpdateLocationStatusAsync(Guid updatedByUserId, string locationCode,
        UpdateLocationStatusRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (updatedByUserId == Guid.Empty || string.IsNullOrWhiteSpace(locationCode)
            || !Validator.TryValidateObject(request, new ValidationContext(request), null, true) || request.Active is null)
            return UpdateLocationStatusResult.Failure(UpdateLocationStatusError.InvalidRequest);
        var matches = await repository.FindByCodeAsync(locationCode.Trim(), cancellationToken);
        if (matches.Count == 0) return UpdateLocationStatusResult.Failure(UpdateLocationStatusError.NotFound);
        if (matches.Count > 1) return UpdateLocationStatusResult.Failure(UpdateLocationStatusError.AmbiguousCode);
        var existing = matches[0];
        if (!request.Active.Value)
        {
            var usage = await repository.GetUsageAsync(existing.Id, cancellationToken);
            if (usage.PendingMissionCodes.Count != 0 || usage.ActiveScheduleNames.Count != 0 || usage.ActiveChargingSessions != 0)
                return UpdateLocationStatusResult.Failure(UpdateLocationStatusError.InUse,
                    new LocationUsage(usage.PendingMissionCodes, usage.ActiveScheduleNames, usage.ActiveChargingSessions));
        }

        var row = await repository.UpdateLocationStatusAsync(existing.Id, request.Active.Value, updatedByUserId, DateTime.UtcNow,
            cancellationToken);
        return UpdateLocationStatusResult.Success(ToResponse(row));
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

    private static bool IsValid(object request) => Validator.TryValidateObject(request, new ValidationContext(request), null, true);

    private static LocationResponse ToResponse(LocationManagementRow row) => new()
    {
        Id = row.Id, Code = row.Code, Name = row.Name, Description = row.Description, Floor = row.FloorCode,
        FloorName = row.FloorName, Kind = ToDatabaseName(row.Kind), MapNodeCode = row.MapNodeCode,
        IsRestrictedArea = row.IsRestrictedArea, RestrictedHours = row.RestrictedHours, IsActive = row.IsActive
    };
}
