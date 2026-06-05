namespace olx_api.Services
{
    public interface IEmailService
    {
        Task SendPasswordResetOtpAsync(string email, string fullName, string otp);
        Task SendRegistrationOtpAsync(string email, string fullName, string otp);
    }
}
