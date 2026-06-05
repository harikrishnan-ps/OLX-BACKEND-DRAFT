// Extensions/ApplicationServiceExtensions.cs
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using olx_api.Repositories;
using olx_api.Services;

namespace olx_api.Extensions
{
    public static class ApplicationServiceExtensions
    {
        // 1. Registers all custom Business Services and Repositories (Dependency Injection)
        public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration config)
        {
            // Register Repositories
            services.AddScoped<IListingRepository, ListingRepository>();
            services.AddScoped<IMessageRepository, MessageRepository>();
            // AdminRepository implementation not found; remove or replace once available
            // services.AddScoped<IAdminRepository, AdminRepository>();
            // LocationRepository implementation not found; remove or replace once available
            // services.AddScoped<ILocationRepository, LocationRepository>();

            services.AddScoped<ITokenService, TokenService>();
            services.AddHttpClient<IEmailService, BrevoEmailService>();

            return services;
        }

        // 2. Extracts messy Identity & JWT configuration out of Program.cs
        public static IServiceCollection AddIdentityServices(this IServiceCollection services, IConfiguration config)
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(GetJwtKey(config))),
                        ValidateIssuer = false,
                        ValidateAudience = false
                    };

                    // Wire up authentication for real-time SignalR hubs
                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            var accessToken = context.Request.Query["access_token"];
                            var path = context.HttpContext.Request.Path;
                            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/chathub"))
                            {
                                context.Token = accessToken;
                            }
                            return Task.CompletedTask;
                        }
                    };
                });

            services.AddAuthorization();
            return services;
        }

        private static string GetJwtKey(IConfiguration config)
        {
            var key = Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
                ?? config["Jwt:Key"];

            if (string.IsNullOrWhiteSpace(key) || key == "JWT_SECRET_KEY")
            {
                key = "dev-only-change-this-jwt-secret-key-32";
            }

            return key;
        }
    }
}
