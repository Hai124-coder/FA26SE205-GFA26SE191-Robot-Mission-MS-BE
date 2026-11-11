using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RobotMissionMS.DataAccess.Entities;
using RobotMissionMS.DataAccess.Enums;
using RobotMissionMS.DataAccess.Persistence;

namespace RobotMissionMS.Seeder;

public sealed class ReferenceDataSeeder(RobotMissionDbContext db)
{
    private readonly Dictionary<string, (int Added, int Skipped)> counts = new();

    public async Task RunAsync()
    {
        foreach (var name in new[] { "ADMIN", "OPERATOR", "SUPERVISOR" })
            await EnsureAsync(new Role { Id = Guid.NewGuid(), Name = name, Description = ReferenceCatalog.Marker }, x => x.Name, name);

        var robotTypes = new Dictionary<string, RobotType>();
        foreach (var (code, name, slots, color) in new[] {
            ("MED_CART", "Xe thuốc", 4, "#2563EB"), ("SUPPLY_CART", "Xe vật tư", 3, "#16A34A"),
            ("WASTE_CART", "Xe gom rác", 0, "#D97706"), ("CLEAN_BOT", "Robot lau hành lang", 0, "#9333EA") })
            robotTypes[code] = await EnsureAsync(new RobotType { Id = Guid.NewGuid(), TypeCode = code, Name = name, Slots = slots, ColorHex = color, Description = ReferenceCatalog.Marker }, x => x.TypeCode, code);

        var missions = new Dictionary<string, MissionType>();
        foreach (var (code, name, robot, pickup, wait) in new (string, string, string, LocationKind?, int)[] {
            ("MEDICINE_DELIVERY", "Giao thuốc", "MED_CART", LocationKind.Pharmacy, 180),
            ("SUPPLY_DELIVERY", "Giao vật tư", "SUPPLY_CART", LocationKind.Storage, 300),
            ("SPECIMEN_TRANSPORT", "Chở mẫu xét nghiệm", "MED_CART", LocationKind.Specimen, 120),
            ("WASTE_COLLECTION", "Gom rác", "WASTE_CART", null, 0),
            ("CORRIDOR_CLEANING", "Lau hành lang", "CLEAN_BOT", null, 0) })
        {
            var batch = pickup is not null;
            var mission = await EnsureAsync(new MissionType { Id = Guid.NewGuid(), Code = code, Name = name,
                RobotTypeId = robotTypes[robot].Id, Mode = batch ? MissionMode.Batch : MissionMode.Schedule,
                PickupKind = pickup, UsesSlotSensor = batch, DepartDelaySec = batch ? 15 : 0,
                WaitTimeoutSec = wait, BasePriority = 10 }, x => x.Code, code);
            Require(mission.RobotTypeId == robotTypes[robot].Id && mission.Mode == (batch ? MissionMode.Batch : MissionMode.Schedule)
                && mission.PickupKind == pickup, $"Mission type {code} has incompatible relationships.");
            missions[code] = mission;
        }

        foreach (var item in ReferenceCatalog.Items)
        {
            var existing = await EnsureAsync(new Item { Id = Guid.NewGuid(), Code = item.Code, Name = item.Name,
                MissionTypeId = missions[item.Mission].Id, Unit = item.Unit, MaxQtyPerSlot = item.Capacity,
                IsActive = true, Description = ReferenceCatalog.Marker }, x => x.Code, item.Code);
            Require(existing.MissionTypeId == missions[item.Mission].Id, $"Item {item.Code} belongs to a different mission type.");
        }

        foreach (var (code, name) in new[] { ("F2", "Tầng 2 – Khối Nội"), ("F3", "Tầng 3 – Khối Ngoại") })
        {
            var zone = await EnsureAsync(new Zone { Id = Guid.NewGuid(), Code = code, Name = name,
                IsActive = true, OperationMode = ZoneMode.Normal, Description = ReferenceCatalog.Marker }, x => x.Code, code);
            await SeedFloorAsync(zone);
        }
        foreach (var (key, value) in ReferenceCatalog.Settings)
            await EnsureAsync(new SystemSetting { Key = key, Value = value, Description = ReferenceCatalog.Marker,
                UpdatedAt = DateTime.UtcNow }, x => x.Key, key);

        await db.SaveChangesAsync();
        foreach (var (table, count) in counts)
            Console.WriteLine($"{table}: added={count.Added}, skipped={count.Skipped}");
        Console.WriteLine($"TOTAL: added={counts.Values.Sum(x => x.Added)}, skipped={counts.Values.Sum(x => x.Skipped)}");
    }

    private async Task SeedFloorAsync(Zone zone)
    {
        var specs = ReferenceCatalog.Locations(zone.Code).ToArray();
        var map = await db.MapVersions.SingleOrDefaultAsync(x => x.ZoneId == zone.Id && x.VersionNo == 1);
        if (map is null)
        {
            Require(!await db.MapVersions.AnyAsync(x => x.ZoneId == zone.Id), $"{zone.Code} already has a map. Refusing to introduce a sample map.");
            var nodes = specs.Select((s, i) => new MapNode { Id = Guid.NewGuid(), Code = s.Code, CoordX = 2 + i * 2, CoordY = 5,
                NodeType = s.Kind is LocationKind.Charger or LocationKind.CleanStation ? NodeType.Charger : NodeType.Stop }).ToArray();
            var edges = nodes.Zip(nodes.Skip(1), (a, b) => new MapEdge { Id = Guid.NewGuid(), FromNodeId = a.Id, ToNodeId = b.Id,
                DistanceMeters = 2, IsBidirectional = true }).ToArray();
            map = new MapVersion { Id = Guid.NewGuid(), ZoneId = zone.Id, VersionNo = 1, WidthM = 60, HeightM = 10,
                Status = MapVersionStatus.Published, ImportedAt = DateTime.UtcNow, PublishedAt = DateTime.UtcNow,
                SourceJson = JsonSerializer.Serialize(new { seed = ReferenceCatalog.Marker, widthM = 60, heightM = 10,
                    nodes = nodes.Select(n => new { code = n.Code, x = n.CoordX, y = n.CoordY }),
                    edges = specs.Zip(specs.Skip(1), (a, b) => new { from = a.Code, to = b.Code, distanceMeters = 2, bidirectional = true }) }),
                MapNodes = nodes, MapEdges = edges };
            db.MapVersions.Add(map);
            Count(nameof(MapVersion), true);
            foreach (var _ in nodes) Count(nameof(MapNode), true);
            foreach (var _ in edges) Count(nameof(MapEdge), true);
            await db.SaveChangesAsync();
        }
        else
        {
            using var source = JsonDocument.Parse(map.SourceJson);
            Require(source.RootElement.TryGetProperty("seed", out var marker) && marker.GetString() == ReferenceCatalog.Marker,
                $"{zone.Code} map v1 is not a seed map. Refusing to attach sample locations.");
            Require(map.Status == MapVersionStatus.Published, $"{zone.Code} seed map is no longer published.");
            Count(nameof(MapVersion), false);
            var nodes = await db.MapNodes.Where(x => x.MapVersionId == map.Id).ToListAsync();
            Require(specs.All(s => nodes.Any(n => n.Code == s.Code)), $"{zone.Code} seed map is missing location nodes.");
            foreach (var _ in nodes) Count(nameof(MapNode), false);
            foreach (var _ in await db.MapEdges.Where(x => x.MapVersionId == map.Id).ToListAsync()) Count(nameof(MapEdge), false);
        }

        foreach (var spec in specs)
        {
            var location = await EnsureAsync(new Location { Id = Guid.NewGuid(), ZoneId = zone.Id, Code = spec.Code,
                Name = spec.Name, MapNodeCode = spec.Code, Kind = spec.Kind, IsActive = true,
                Description = ReferenceCatalog.Marker }, x => x.Code, spec.Code);
            Require(location.ZoneId == zone.Id && location.Kind == spec.Kind && location.MapNodeCode == spec.Code,
                $"Location {spec.Code} has incompatible floor/kind/node.");
            if (spec.Kind is LocationKind.Charger or LocationKind.CleanStation)
                await EnsureAsync(new ChargingStation { Id = Guid.NewGuid(), LocationId = location.Id, Name = $"{zone.Code} – {spec.Name}",
                    StationType = spec.Kind == LocationKind.CleanStation ? "CLEAN" : "STANDARD",
                    Capacity = spec.Code.EndsWith("CHARGE-MED") ? 2 : 1, Status = StationStatus.Operational },
                    x => x.LocationId, location.Id);
        }
    }

    private async Task<T> EnsureAsync<T, TKey>(T candidate, System.Linq.Expressions.Expression<Func<T, TKey>> keySelector, TKey key) where T : class
    {
        var predicate = System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(
            System.Linq.Expressions.Expression.Equal(keySelector.Body, System.Linq.Expressions.Expression.Constant(key, typeof(TKey))), keySelector.Parameters);
        var existing = await db.Set<T>().SingleOrDefaultAsync(predicate);
        Count(typeof(T).Name, existing is null);
        if (existing is not null) return existing;
        db.Set<T>().Add(candidate);
        await db.SaveChangesAsync();
        return candidate;
    }

    private void Count(string type, bool added)
    {
        var count = counts.GetValueOrDefault(type);
        counts[type] = (count.Added + (added ? 1 : 0), count.Skipped + (added ? 0 : 1));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message + " Existing data was not overwritten; transaction rolled back.");
    }
}
