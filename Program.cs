// Program.cs
using Microsoft.EntityFrameworkCore;
using olx_api.Data;
using olx_api.Extensions;
using olx_api.Hubs;

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
builder.Services.AddSignalR();

// 3. Setup Database Connection (SQL Server)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION") 
                       ?? builder.Configuration.GetConnectionString("DefaultConnection");

var jwtKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY") 
             ?? builder.Configuration["Jwt:Key"];

// 4. Inject Modular Custom Configurations (JWT Setup, Repositories, etc.)
builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddIdentityServices(builder.Configuration);

// 5. Configure Cross-Origin Resource Sharing (CORS) for your Frontend Apps
builder.Services.AddCors(options =>
{
    options.AddPolicy("OlxCorsPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:19006") // Web and React Native ports
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Required for SignalR real-time chats
    });
});

// 6. Build the Application Pipeline
var app = builder.Build();

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
