using Microsoft.EntityFrameworkCore;
using Npgsql;
using RobotMissionMS.Api.Extensions;
using RobotMissionMS.Api.Authorization;
using RobotMissionMS.Api.Realtime;
using RobotMissionMS.Business.Features.Trips.Services;
using RobotMissionMS.Business.Features.Tasks.Services;
using RobotMissionMS.Business.Features.ActivityLogs.Services;
using RobotMissionMS.Business.Features.Incidents.Services;
using RobotMissionMS.DataAccess.Persistence;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Set ConnectionStrings:DefaultConnection in User Secrets or environment variables.");
builder.Services.AddSingleton<NpgsqlDataSource>(_ => DatabaseDataSource.Create(connectionString));
builder.Services.AddDbContext<RobotMissionDbContext>((services, options) =>
    options.UseNpgsql(services.GetRequiredService<NpgsqlDataSource>()));

builder.Services.AddControllers();
builder.Services.AddRateLimiter(options => options.AddPolicy("forgot-password", context =>
    RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(10), QueueLimit = 0, AutoReplenishment = true })));
builder.Services.AddSignalR();
builder.Services.AddSingleton<IOperationsRealtimePublisher, OperationsRealtimePublisher>();
builder.Services.AddSingleton<IRobotStateRealtimePublisher, OperationsRobotRealtimePublisher>();
builder.Services.AddScoped<ITripRealtimePublisher, OperationsTripRealtimePublisher>();
builder.Services.AddScoped<IMissionRealtimePublisher, OperationsMissionRealtimePublisher>();
builder.Services.AddScoped<IActivityRealtimePublisher, OperationsActivityRealtimePublisher>();
builder.Services.AddScoped<IIncidentRealtimePublisher, OperationsIncidentRealtimePublisher>();
builder.Services.AddAuthConfiguration(builder.Configuration);
builder.Services.AddAuthServices();
builder.Services.AddUserServices();
builder.Services.AddRobotServices();
builder.Services.AddFloorServices();
builder.Services.AddLocationServices();
builder.Services.AddChargingStationServices();
builder.Services.AddMapServices();
builder.Services.AddConfigurationServices();
builder.Services.AddItemCatalogServices();
builder.Services.AddMissionTypeServices();
builder.Services.AddRequestPreviewServices();
builder.Services.AddTaskServices();
builder.Services.AddPlanServices();
builder.Services.AddIncidentServices();
builder.Services.AddDashboardServices();
builder.Services.AddReportServices();
builder.Services.AddActivityLogServices();
builder.Services.AddScheduleServices();
builder.Services.AddMqttOutbox(builder.Configuration);
builder.Services.AddImportServices();
builder.Services.AddJwtBearerAuthentication();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen();

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer",
        new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Nhập accessToken, không thêm tiền tố Bearer."
        });

    options.AddSecurityRequirement(
        new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    {
                        Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
});

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

if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseAuthentication();
app.UseMiddleware<MustChangePasswordMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();
app.MapHub<OperationsHub>("/hubs/ops");

app.Run();
