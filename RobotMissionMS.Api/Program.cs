using Microsoft.EntityFrameworkCore;
using Npgsql;
using RobotMissionMS.Api.Extensions;
using RobotMissionMS.DataAccess.Persistence;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Set ConnectionStrings:DefaultConnection in User Secrets or environment variables.");
builder.Services.AddSingleton<NpgsqlDataSource>(_ => DatabaseDataSource.Create(connectionString));
builder.Services.AddDbContext<RobotMissionDbContext>((services, options) =>
    options.UseNpgsql(services.GetRequiredService<NpgsqlDataSource>()));

builder.Services.AddControllers();
builder.Services.AddAuthServices();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapGet("/health/database", async (RobotMissionDbContext db, CancellationToken cancellationToken) =>
        await db.Database.CanConnectAsync(cancellationToken)
            ? Results.Ok(new { status = "connected" })
            : Results.StatusCode(StatusCodes.Status503ServiceUnavailable))
        .WithTags("Health");
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
