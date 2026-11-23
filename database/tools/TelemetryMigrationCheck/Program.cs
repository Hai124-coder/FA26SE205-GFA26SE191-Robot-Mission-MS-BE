using RobotMissionMS.DataAccess.Persistence;

var cs = Environment.GetEnvironmentVariable("SECTION9_CONNECTION") ?? throw new InvalidOperationException("SECTION9_CONNECTION is required.");
await using var source = DatabaseDataSource.Create(cs);
await using var connection = await source.OpenConnectionAsync();
var script = await File.ReadAllTextAsync(Path.Combine(Directory.GetCurrentDirectory(), "database", "scripts", "012_extend_telemetry_state.sql"));
await using (var apply = connection.CreateCommand()) { apply.CommandText = script; await apply.ExecuteNonQueryAsync(); }
await using var check = connection.CreateCommand();
check.CommandText = "SELECT count(*) FROM information_schema.columns WHERE table_schema='public' AND table_name='TelemetrySnapshots' AND column_name IN ('HeadingRad','ReportedStatus','OrderId','NextNodeCode','AtStopSeq','ReceivedAt');";
var columns = Convert.ToInt32(await check.ExecuteScalarAsync());
if (columns != 6) throw new InvalidOperationException($"Expected 6 telemetry columns, found {columns}.");
Console.WriteLine("telemetry-migration=verified columns=6 applied=1");
