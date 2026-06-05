using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using olx_api.Models;

namespace olx_api.Services
{
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _configuration;
        private DateTime _accessTokenExpiresAt;

        public TokenService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public DateTime AccessTokenExpiresAt => _accessTokenExpiresAt;

        public string CreateAccessToken(User user)
        {
            _accessTokenExpiresAt = DateTime.UtcNow.AddMinutes(GetAccessTokenMinutes());

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var credentials = new SigningCredentials(GetSigningKey(), SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                claims: claims,
                expires: _accessTokenExpiresAt,
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public string CreateRefreshToken()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        }

        public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
        {
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = GetSigningKey(),
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = false
            };

            try
            {
                return new JwtSecurityTokenHandler().ValidateToken(token, validationParameters, out _);
            }
            catch
            {
                return null;
            }
        }

        private SymmetricSecurityKey GetSigningKey()
        {
            var key = Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
                ?? _configuration["Jwt:Key"];

            if (string.IsNullOrWhiteSpace(key) || key == "JWT_SECRET_KEY")
            {
                key = "dev-only-change-this-jwt-secret-key-32";
            }

            return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        }

        private int GetAccessTokenMinutes()
        {
            return int.TryParse(_configuration["Jwt:AccessTokenMinutes"], out var minutes)
                ? minutes
                : 30;
        }
    }
}
