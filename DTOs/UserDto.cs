namespace olx_api.DTOs
{
    public record RegisterDto(string FullName, string Email, string Password, string PhoneNumber);
    public record LoginDto(string Identifier, string Password);
    public record RefreshTokenRequestDto(string Token, string RefreshToken);
    public record ForgotPasswordDto(string Email);
    public record ResetPasswordDto(string Email, string Otp, string NewPassword);
    public record AuthResponseDto(string Token, string RefreshToken, DateTime TokenExpiresAt, string Email, string FullName, string Role);
    public record UserProfileDto(Guid Id, string FullName, string Email, string? PhoneNumber, string? ProfilePictureUrl, string Role, string UserTier, int AdQuotaRemaining, DateTime CreatedAt);
    public record UpdateProfileDto(string FullName, string PhoneNumber, string? ProfilePictureUrl);
    public record ChangePasswordDto(string CurrentPassword, string NewPassword);
    public record VerifyOtpDto(string Email, string Otp);
    public record ResendOtpDto(string Email);
}
