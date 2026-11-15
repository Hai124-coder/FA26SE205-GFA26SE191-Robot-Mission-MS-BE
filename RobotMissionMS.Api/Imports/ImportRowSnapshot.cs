using System.Globalization;
using System.Text.Json;

namespace RobotMissionMS.Api.Imports;

/// <summary>Immutable values used to preview one valid import row.</summary>
public sealed record ImportRowSnapshot(
    IReadOnlyList<int> Quantities,
    int MaxQtyPerSlot,
    decimal KgPerUnit,
    Guid PickupLocationId,
    DateTime? DeadlineAt)
{
    public string ToJson() => JsonSerializer.Serialize(this);

    public static bool TryRead(string json, out ImportRowSnapshot? snapshot)
    {
        try
        {
            snapshot = JsonSerializer.Deserialize<ImportRowSnapshot>(json);
            return snapshot is not null;
        }
        catch (JsonException)
        {
            snapshot = null;
            return false;
        }
    }

    public static IReadOnlyList<int> ReadQuantities(string json)
    {
        if (TryRead(json, out var snapshot)) return snapshot!.Quantities ?? [];
        try { return JsonSerializer.Deserialize<int[]>(json) ?? []; }
        catch (JsonException) { return []; }
    }
}

public static class ImportDeadline
{
    public static bool TryGetUtc(string? deadline, out DateTime? deadlineAt)
    {
        deadlineAt = null;
        if (string.IsNullOrWhiteSpace(deadline)) return true;
        if (!TimeOnly.TryParseExact(deadline, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)) return false;
        var timeZone = GetVietnamTimeZone();
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
        var localDeadline = DateTime.SpecifyKind(localNow.Date.Add(time.ToTimeSpan()), DateTimeKind.Unspecified);
        deadlineAt = TimeZoneInfo.ConvertTimeToUtc(localDeadline, timeZone);
        return true;
    }

    private static TimeZoneInfo GetVietnamTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
    }
}
