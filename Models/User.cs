using System.ComponentModel.DataAnnotations;

namespace olx_api.Models
{
    public class User
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        [Required, MaxLength(100)]
        public string FullName { get; set; }
        [Required, EmailAddress, MaxLength(150)]
        public string Email { get; set; }
        [Required]
        public string PasswordHash { get; set; }
        [MaxLength(20)]
        public string PhoneNumber { get; set; }
        public string Role { get; set; } = "User"; // User, Admin, Moderator
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsVerified { get; set; } = false;
        public string? ProfilePictureUrl { get; set; }
        public bool IsBlocked { get; set; } = false;
        public string UserTier { get; set; } = "Free"; // Free, Elite, Dealer
        public int AdQuotaRemaining { get; set; } = 5; // Free tier limit

        public string? ResetPasswordOtp { get; set; }
        public DateTime? OtpExpiry { get; set; }

        // Registration OTP fields
        public string? RegistrationOtp { get; set; }
        public DateTime? RegistrationOtpExpiry { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiry { get; set; }

        // Navigation Properties
        public ICollection<Listing> Listings { get; set; }
        public ICollection<Favorite> Favorites { get; set; }
        public ICollection<Message> SentMessages { get; set; }
        public ICollection<Message> ReceivedMessages { get; set; }
    }
}