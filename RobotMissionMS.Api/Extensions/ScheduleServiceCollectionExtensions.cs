using RobotMissionMS.Business.Features.Schedules.Services;
namespace RobotMissionMS.Api.Extensions; public static class ScheduleServiceCollectionExtensions { public static IServiceCollection AddScheduleServices(this IServiceCollection s){s.AddScoped<IScheduleService,ScheduleService>();return s;} }
