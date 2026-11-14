namespace RobotMissionMS.DataAccess.Repositories.Interfaces;

public interface IRobotTypeRepository
{
    /// <summary>
    /// Returns all robot types ordered by TypeCode.
    /// </summary>
    Task<IReadOnlyList<RobotTypeRow>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a single robot type by its TypeCode, or null if not found.
    /// </summary>
    Task<RobotTypeRow?> FindByTypeCodeAsync(string typeCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the editable fields of a robot type. Returns false when no row was matched.
    /// </summary>
    Task<bool> UpdateAsync(RobotTypeUpdateData data, CancellationToken cancellationToken = default);
}

public sealed record RobotTypeRow(
    Guid    Id,
    string  TypeCode,
    string  Name,
    int     Slots,
    string  ColorHex,
    string? Description
);

public sealed record RobotTypeUpdateData(
    Guid    Id,
    string  Name,
    int     Slots,
    string  ColorHex,
    string? Description
);
