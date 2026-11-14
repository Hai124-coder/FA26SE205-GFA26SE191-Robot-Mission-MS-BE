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
    /// Updates the editable fields of a robot type. Returns true if the row was found and updated.
    /// </summary>
    Task<bool> UpdateAsync(
        Guid id,
        string name,
        int slots,
        string colorHex,
        string? description,
        CancellationToken cancellationToken = default);
}

public sealed record RobotTypeRow(
    Guid    Id,
    string  TypeCode,
    string  Name,
    int     Slots,
    string  ColorHex,
    string? Description
);
