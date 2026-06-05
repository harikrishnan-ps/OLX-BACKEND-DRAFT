// Program.cs
using Microsoft.EntityFrameworkCore;
using olx_api.Data;
using olx_api.Extensions;
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
builder.Services.AddSwaggerGen();

var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION")
    ?? builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString) || connectionString == "DB_CONNECTION")
{
    throw new InvalidOperationException("Database connection string is missing. Set DB_CONNECTION in .env or configuration.");
}

// 3. Setup Database Connection (SQL Server)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(connectionString);
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
                       ?? "http://localhost:3000,http://localhost:19006")
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

// If implementing real-time chat hubs down the road:
// app.MapHub<ChatHub>("/chathub");

app.Run();
