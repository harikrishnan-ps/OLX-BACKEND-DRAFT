using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using olx_api.Data;
using olx_api.DTOs;

namespace olx_api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/profile")]
    public class ProfileController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ProfileController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<UserProfileDto>> GetProfile()
        {
            var user = await GetCurrentUserAsync();
            if (user is null)
            {
                return Unauthorized();
            }

            return Ok(ToProfileDto(user));
        }

        [HttpPut]
        public async Task<ActionResult<UserProfileDto>> UpdateProfile(UpdateProfileDto dto)
        {
            var user = await GetCurrentUserAsync();
            if (user is null)
            {
                return Unauthorized();
            }

            var phone = dto.PhoneNumber.Trim();
            if (await _context.Users.AnyAsync(u => u.Id != user.Id && u.PhoneNumber == phone))
            {
                return Conflict("Mobile number is already used by another account.");
            }

            user.FullName = dto.FullName.Trim();
            user.PhoneNumber = phone;
            user.ProfilePictureUrl = string.IsNullOrWhiteSpace(dto.ProfilePictureUrl)
                ? null
                : dto.ProfilePictureUrl.Trim();

            await _context.SaveChangesAsync();
            return Ok(ToProfileDto(user));
        }

        [HttpPut("change-password")]
        public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
        {
            var user = await GetCurrentUserAsync();
            if (user is null)
            {
                return Unauthorized();
            }

            if (dto.NewPassword != dto.ConfirmPassword)
            {
                return BadRequest("New Password and Confirm Password do not match.");
            }

            if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash))
            {
                return BadRequest("Current password is incorrect.");
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            user.RefreshToken = null;
            user.RefreshTokenExpiry = null;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        private async Task<olx_api.Models.User?> GetCurrentUserAsync()
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(userIdValue, out var userId)
                ? await _context.Users.FindAsync(userId)
                : null;
        }

        private static UserProfileDto ToProfileDto(olx_api.Models.User user)
        {
            return new UserProfileDto(
                user.Id,
                user.FullName,
                user.Email,
                user.PhoneNumber,
                user.ProfilePictureUrl,
                user.Role,
                user.UserTier,
                user.AdQuotaRemaining,
                user.CreatedAt);
        }
    }
}
