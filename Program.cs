// Program.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using olx_api.Data;
using olx_api.Extensions;
using olx_api.Hubs;
using olx_api.Services;

var builder = WebApplication.CreateBuilder(args);

DotNetEnv.Env.Load();
builder.Configuration.AddEnvironmentVariables();

// 1. Add Core Controllers & Route Formatting
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Prevents infinite loops when serializing models with bidirectional relationships
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

// 2. Configure Swagger API Documentation Tools
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme. \r\n\r\n Enter 'Bearer' [space] and then your token in the text input below.\r\n\r\nExample: \"Bearer 1safsfsdfdfd\""
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});
builder.Services.AddSignalR();

var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION")
    ?? builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString) || connectionString == "DB_CONNECTION")
{
    throw new InvalidOperationException("Database connection string is missing. Set DB_CONNECTION in .env or configuration.");
}

// 3. Setup Database Connection (MySQL)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 30)));
});

// 4. Inject Modular Custom Configurations (JWT Setup, Repositories, etc.)
builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddIdentityServices(builder.Configuration);

// 5. Configure Cross-Origin Resource Sharing (CORS) for your Frontend Apps
builder.Services.AddCors(options =>
{
    options.AddPolicy("OlxCorsPolicy", policy =>
    {
        var origins = (Environment.GetEnvironmentVariable("CORS_ORIGINS")
                       ?? builder.Configuration["Cors:Origins"]
                       ?? "http://localhost:3000,http://localhost:4200,http://localhost:19006")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        policy.WithOrigins(origins) // Web and React Native ports
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Required for SignalR real-time chats
    });
});

// 6. Build the Application Pipeline
var app = builder.Build();

await DatabaseSeeder.ApplyMigrationsAndSeedAsync(app.Services);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// 7. Core Middleware Routing Order (Crucial: CORS -> Auth -> Routing)
app.UseCors("OlxCorsPolicy");

app.UseAuthentication(); // Validates who is calling the API via JWT Tokens
app.UseAuthorization();  // Verifies what the caller is allowed to access

app.MapControllers();

app.MapHub<ChatHub>("/chathub");

app.Run();

public partial class Program { }
