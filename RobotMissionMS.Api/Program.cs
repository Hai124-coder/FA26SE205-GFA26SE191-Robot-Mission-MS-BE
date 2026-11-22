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
app.UseAuthorization();

app.MapControllers();

app.Run();
