namespace RobotMissionMS.DataAccess.Repositories.Interfaces;

public interface IRobotTypeRepository
{
    /// <summary>
    /// Returns all robot types ordered by TypeCode.
    /// </summary>
    Task<IReadOnlyList<RobotTypeRow>> GetAllAsync(CancellationToken cancellationToken = default);
}

public sealed record RobotTypeRow(
    Guid    Id,
    string  TypeCode,
    string  Name,
    int     Slots,
    string  ColorHex,
    string? Description
);
