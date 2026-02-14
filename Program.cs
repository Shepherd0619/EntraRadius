using EntraRadius.Models;
using EntraRadius.Services;

namespace EntraRadius
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllers();

            // Add health checks
            builder.Services.AddHealthChecks();

            // Configure EntraConfiguration from appsettings.json
            builder.Services.Configure<EntraConfiguration>(
                builder.Configuration.GetSection("EntraConfiguration"));

            // Register services
            builder.Services.AddMemoryCache();
            builder.Services.AddSingleton<IUserCacheService, UserCacheService>();
            builder.Services.AddScoped<GraphClientService>();

            var app = builder.Build();

            // Configure the HTTP request pipeline.

            app.UseHttpsRedirection();

            app.UseAuthorization();

            // Map health check endpoint
            app.MapHealthChecks("/health");

            app.MapControllers();

            app.Run();
        }
    }
}
