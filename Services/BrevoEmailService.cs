using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace olx_api.Services
{
    public class BrevoEmailService : IEmailService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<BrevoEmailService> _logger;

        public BrevoEmailService(HttpClient httpClient, IConfiguration configuration, ILogger<BrevoEmailService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendPasswordResetOtpAsync(string email, string fullName, string otp)
        {
            var apiKey = Environment.GetEnvironmentVariable("BREVO_API_KEY") ?? _configuration["Brevo:ApiKey"];
            var senderEmail = Environment.GetEnvironmentVariable("BREVO_SENDER_EMAIL") ?? _configuration["Brevo:SenderEmail"];
            var senderName = Environment.GetEnvironmentVariable("BREVO_SENDER_NAME") ?? _configuration["Brevo:SenderName"] ?? "OLX Clone";

            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(senderEmail))
            {
                _logger.LogInformation("Password reset OTP for {Email}: {Otp}", email, otp);
                return;
            }

            var payload = new
            {
                sender = new { name = senderName, email = senderEmail },
                to = new[] { new { email, name = fullName } },
                subject = "Your password reset OTP",
                htmlContent = $"<p>Hello {fullName},</p><p>Your password reset OTP is <strong>{otp}</strong>. It expires in 10 minutes.</p>"
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Headers.Add("xapikey", apiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Brevo email failed for {Email}. Status: {Status}. Body: {Body}", email, response.StatusCode, body);
            }
        }

        public async Task SendRegistrationOtpAsync(string email, string fullName, string otp)
        {
            var apiKey = Environment.GetEnvironmentVariable("BREVO_API_KEY") ?? _configuration["Brevo:ApiKey"];
            var senderEmail = Environment.GetEnvironmentVariable("BREVO_SENDER_EMAIL") ?? _configuration["Brevo:SenderEmail"];
            var senderName = Environment.GetEnvironmentVariable("BREVO_SENDER_NAME") ?? _configuration["Brevo:SenderName"] ?? "OLX Clone";

            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(senderEmail))
            {
                _logger.LogInformation("Registration OTP for {Email}: {Otp}", email, otp);
                return;
            }

            var payload = new
            {
                sender = new { name = senderName, email = senderEmail },
                to = new[] { new { email, name = fullName } },
                subject = "Verify your OLX account",
                htmlContent = $"<p>Hello {fullName},</p><p>Welcome to OLX! Your verification OTP is <strong>{otp}</strong>. It expires in 10 minutes.</p><p>Please enter this code to activate your account.</p>"
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Headers.Add("xapikey", apiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Brevo registration OTP email failed for {Email}. Status: {Status}. Body: {Body}", email, response.StatusCode, body);
            }
        }
    }
}
