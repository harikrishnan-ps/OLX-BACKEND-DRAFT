using System.Security.Claims;
using olx_api.Models;

namespace olx_api.Services
{
    public interface ITokenService
    {
        string CreateAccessToken(User user);
        string CreateRefreshToken();
        ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
        DateTime AccessTokenExpiresAt { get; }
    }
}
