using Microsoft.EntityFrameworkCore;
using olx_api.Data;
using olx_api.Models;

namespace olx_api.Services
{
    public static class DatabaseSeeder
    {
        public static async Task ApplyMigrationsAndSeedAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseSeeder");

            if (IsEnabled(Environment.GetEnvironmentVariable("AUTO_APPLY_MIGRATIONS") ?? configuration["Database:AutoApplyMigrations"]))
            {
                await context.Database.MigrateAsync();
            }

            await SeedCountriesAndStatesAsync(context, logger);
            await SeedCitiesAsync(context, logger);
            await SeedAdminUserAsync(context, logger);
        }

        private static async Task SeedCountriesAndStatesAsync(ApplicationDbContext context, ILogger logger)
        {
            // Check if India already exists
            var india = await context.Countries.FirstOrDefaultAsync(c => c.Name == "India");
            if (india is not null)
            {
                return;
            }

            india = new Country { Name = "India" };
            context.Countries.Add(india);
            await context.SaveChangesAsync();

            // List of Indian states
            var states = new[]
            {
                "Andhra Pradesh",
                "Arunachal Pradesh",
                "Assam",
                "Bihar",
                "Chhattisgarh",
                "Goa",
                "Gujarat",
                "Haryana",
                "Himachal Pradesh",
                "Jharkhand",
                "Karnataka",
                "Kerala",
                "Madhya Pradesh",
                "Maharashtra",
                "Manipur",
                "Meghalaya",
                "Mizoram",
                "Nagaland",
                "Odisha",
                "Punjab",
                "Rajasthan",
                "Sikkim",
                "Tamil Nadu",
                "Telangana",
                "Tripura",
                "Uttar Pradesh",
                "Uttarakhand",
                "West Bengal",
                "Andaman and Nicobar Islands",
                "Chandigarh",
                "Dadra and Nagar Haveli and Daman and Diu",
                "Lakshadweep",
                "Delhi",
                "Puducherry"
            };

            foreach (var stateName in states)
            {
                var state = new State { Name = stateName, CountryId = india.Id };
                context.States.Add(state);
            }

            await context.SaveChangesAsync();
            logger.LogInformation("Seeded India with {StateCount} states", states.Length);
        }

        private static async Task SeedCitiesAsync(ApplicationDbContext context, ILogger logger)
        {
            // Check if cities already exist
            if (await context.Cities.AnyAsync())
            {
                return;
            }

            var citiesByState = new Dictionary<string, string[]>
            {
                { "Andhra Pradesh", new[] { "Visakhapatnam", "Vijayawada", "Guntur", "Tirupati", "Nellore", "Kakinada", "Rajahmundry", "Eluru" } },
                { "Arunachal Pradesh", new[] { "Itanagar", "Naharlagun", "Pasighat", "Tezu", "Ziro" } },
                { "Assam", new[] { "Guwahati", "Silchar", "Dibrugarh", "Nagaon", "Barpeta", "Tezpur", "Jorhat" } },
                { "Bihar", new[] { "Patna", "Gaya", "Bhagalpur", "Muzaffarpur", "Darbhanga", "Arrah", "Motihari", "Purnia" } },
                { "Chhattisgarh", new[] { "Raipur", "Bilaspur", "Durg", "Bhilai", "Rajnandgaon", "Raigarh" } },
                { "Goa", new[] { "Panaji", "Margao", "Vasco da Gama", "Mormugao", "Candolim", "Ponda" } },
                { "Gujarat", new[] { "Ahmedabad", "Surat", "Vadodara", "Rajkot", "Bhavnagar", "Jamnagar", "Gandhinagar", "Anand" } },
                { "Haryana", new[] { "Faridabad", "Gurgaon", "Hisar", "Rohtak", "Panipat", "Ambala", "Yamunanagar", "Sonepat" } },
                { "Himachal Pradesh", new[] { "Shimla", "Solan", "Mandi", "Kangra", "Hamirpur", "Bilaspur", "Palampur" } },
                { "Jharkhand", new[] { "Ranchi", "Dhanbad", "Giridih", "Jamshedpur", "Hazaribagh", "Bokaro", "Deoghar" } },
                { "Karnataka", new[] { "Bangalore", "Mysore", "Mangalore", "Belgaum", "Hubli", "Davanagere", "Shimoga", "Udupi" } },
                { "Kerala", new[] { "Kochi", "Thiruvananthapuram", "Kozhikode", "Thrissur", "Kottayam", "Alappuzha", "Kannur", "Palakkad" } },
                { "Madhya Pradesh", new[] { "Indore", "Bhopal", "Gwalior", "Jabalpur", "Ujjain", "Sagar", "Satna", "Ratlam" } },
                { "Maharashtra", new[] { "Mumbai", "Pune", "Nagpur", "Thane", "Aurangabad", "Nashik", "Kolhapur", "Solapur" } },
                { "Manipur", new[] { "Imphal", "Thoubal", "Bishnupur", "Senapati", "Ukhrul" } },
                { "Meghalaya", new[] { "Shillong", "Tura", "Cherrapunji", "Jowai", "Nongpoh" } },
                { "Mizoram", new[] { "Aizawl", "Lunglei", "Saiha", "Champhai", "Kolasib" } },
                { "Nagaland", new[] { "Kohima", "Dimapur", "Mokokchung", "Wokha", "Zunheboto" } },
                { "Odisha", new[] { "Bhubaneswar", "Cuttack", "Rourkela", "Sambalpur", "Berhampur", "Balasore", "Dhenkanal" } },
                { "Punjab", new[] { "Ludhiana", "Amritsar", "Chandigarh", "Patiala", "Jalandhar", "Bathinda", "Hoshiarpur" } },
                { "Rajasthan", new[] { "Jaipur", "Jodhpur", "Kota", "Bikaner", "Ajmer", "Udaipur", "Alwar", "Pali" } },
                { "Sikkim", new[] { "Gangtok", "Namchi", "Singtam", "Mangan", "Geyzing" } },
                { "Tamil Nadu", new[] { "Chennai", "Coimbatore", "Madurai", "Salem", "Trichy", "Erode", "Tiruppur", "Cuddalore" } },
                { "Telangana", new[] { "Hyderabad", "Secunderabad", "Warangal", "Karimnagar", "Nizamabad", "Ramagundam" } },
                { "Tripura", new[] { "Agartala", "Udaipur", "Dharmanagar", "Kailashahar", "Silchar" } },
                { "Uttar Pradesh", new[] { "Lucknow", "Kanpur", "Agra", "Varanasi", "Meerut", "Ghaziabad", "Aligarh", "Gorakhpur" } },
                { "Uttarakhand", new[] { "Dehradun", "Haldwani", "Nainital", "Garhwal", "Almora", "Pithoragarh" } },
                { "West Bengal", new[] { "Kolkata", "Howrah", "Darjeeling", "Siliguri", "Asansol", "Durgapur", "Jalpaiguri" } },
                { "Andaman and Nicobar Islands", new[] { "Port Blair", "Car Nicobar", "Havelock Island", "Neil Island" } },
                { "Chandigarh", new[] { "Chandigarh", "Panchkula", "Mohali" } },
                { "Dadra and Nagar Haveli and Daman and Diu", new[] { "Silvassa", "Daman", "Diu", "Amli" } },
                { "Lakshadweep", new[] { "Kavaratti", "Agatti", "Minicoy", "Androth" } },
                { "Delhi", new[] { "New Delhi", "Delhi", "Dwarka", "Rohini", "Pitampura", "Noida", "Gurgaon" } },
                { "Puducherry", new[] { "Puducherry", "Yanam", "Karaikal", "Mahe", "Ooty" } }
            };

            var stateDict = await context.States.ToDictionaryAsync(s => s.Name, s => s.Id);

            foreach (var (stateName, cities) in citiesByState)
            {
                if (stateDict.TryGetValue(stateName, out var stateId))
                {
                    foreach (var cityName in cities)
                    {
                        var city = new City { Name = cityName, StateId = stateId };
                        context.Cities.Add(city);
                    }
                }
            }

            await context.SaveChangesAsync();
            logger.LogInformation("Seeded cities for Indian states");
        }

        private static async Task SeedAdminUserAsync(ApplicationDbContext context, ILogger logger)
        {
            var email = Environment.GetEnvironmentVariable("ADMIN_EMAIL")?.Trim().ToLowerInvariant();
            var password = Environment.GetEnvironmentVariable("ADMIN_PASSWORD");

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                return;
            }

            var existingAdmin = await context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (existingAdmin is not null)
            {
                if (!string.Equals(existingAdmin.Role, "Admin", StringComparison.OrdinalIgnoreCase))
                {
                    existingAdmin.Role = "Admin";
                    await context.SaveChangesAsync();
                }

                return;
            }

            var admin = new User
            {
                FullName = Environment.GetEnvironmentVariable("ADMIN_FULL_NAME")?.Trim() ?? "Platform Admin",
                Email = email,
                PhoneNumber = Environment.GetEnvironmentVariable("ADMIN_PHONE_NUMBER")?.Trim() ?? string.Empty,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = "Admin",
                IsVerified = true,
                AdQuotaRemaining = 0
            };

            context.Users.Add(admin);
            await context.SaveChangesAsync();
            logger.LogInformation("Seeded admin user {Email}", email);
        }

        private static bool IsEnabled(string? value)
        {
            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
        }
    }
}
