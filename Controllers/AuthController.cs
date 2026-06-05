using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using olx_api.Data;
using olx_api.DTOs;
using olx_api.Models;
using olx_api.Services;

namespace olx_api.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ITokenService _tokenService;
        private readonly IEmailService _emailService;

        public AuthController(ApplicationDbContext context, ITokenService tokenService, IEmailService emailService)
        {
            _context = context;
            _tokenService = tokenService;
            _emailService = emailService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterDto dto)
        {
            if (dto.Password != dto.ConfirmPassword)
            {
                return BadRequest("Password and Confirm Password do not match.");
            }

            var email = dto.Email.Trim().ToLowerInvariant();
            var phone = dto.PhoneNumber.Trim();

            if (await _context.Users.AnyAsync(u => u.Email.ToLower() == email))
            {
                return Conflict("Email is already registered.");
            }

            if (await _context.Users.AnyAsync(u => u.PhoneNumber == phone))
            {
                return Conflict("Mobile number is already registered.");
            }

            var otp = RandomNumberGenerator.GetInt32(100000, 999999).ToString();

            var user = new User
            {
                FullName = dto.FullName.Trim(),
                Email = email,
                PhoneNumber = phone,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                IsVerified = false,
                RegistrationOtp = otp,
                RegistrationOtpExpiry = DateTime.UtcNow.AddMinutes(10)
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            await _emailService.SendRegistrationOtpAsync(user.Email, user.FullName, otp);

            return Ok(new { message = "Registration successful. Please check your email for the verification OTP." });
        }

        [HttpPost("verify-otp")]
        public async Task<ActionResult<AuthResponseDto>> VerifyOtp(VerifyOtpDto dto)
        {
            var email = dto.Email.Trim().ToLowerInvariant();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email);

            if (user is null ||
                user.RegistrationOtp != dto.Otp ||
                user.RegistrationOtpExpiry is null ||
                user.RegistrationOtpExpiry < DateTime.UtcNow)
            {
                return BadRequest("Invalid or expired OTP.");
            }

            user.IsVerified = true;
            user.RegistrationOtp = null;
            user.RegistrationOtpExpiry = null;

            user.RefreshToken = _tokenService.CreateRefreshToken();
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
            await _context.SaveChangesAsync();

            return Ok(CreateAuthResponse(user));
        }

        [HttpPost("resend-otp")]
        public async Task<IActionResult> ResendOtp(ResendOtpDto dto)
        {
            var email = dto.Email.Trim().ToLowerInvariant();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email);

            if (user is not null && !user.IsVerified)
            {
                var otp = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
                user.RegistrationOtp = otp;
                user.RegistrationOtpExpiry = DateTime.UtcNow.AddMinutes(10);
                await _context.SaveChangesAsync();
                await _emailService.SendRegistrationOtpAsync(user.Email, user.FullName, otp);
            }

            return Ok(new { message = "If the email exists and is unverified, a new OTP has been sent." });
        }

        [HttpPost("login")]
        public async Task<ActionResult<AuthResponseDto>> Login(LoginDto dto)
        {
            var identifier = dto.Identifier.Trim().ToLowerInvariant();
            var user = await _context.Users.FirstOrDefaultAsync(u =>
                u.Email.ToLower() == identifier ||
                u.PhoneNumber == dto.Identifier.Trim());

            if (user is null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            {
                return Unauthorized("Invalid login credentials.");
            }

            if (!user.IsVerified)
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    "Please verify your email first. Use /api/auth/resend-otp to get a new code.");
            }

            if (user.IsBlocked)
            {
                return StatusCode(StatusCodes.Status403Forbidden, "Your account is blocked.");
            }

            user.RefreshToken = _tokenService.CreateRefreshToken();
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
            await _context.SaveChangesAsync();

            return Ok(CreateAuthResponse(user));
        }

        [HttpPost("refresh")]
        public async Task<ActionResult<AuthResponseDto>> Refresh(RefreshTokenRequestDto dto)
        {
            var principal = _tokenService.GetPrincipalFromExpiredToken(dto.Token);
            var userIdValue = principal?.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!Guid.TryParse(userIdValue, out var userId))
            {
                return Unauthorized("Invalid access token.");
            }

            var user = await _context.Users.FindAsync(userId);
            if (user is null ||
                user.RefreshToken != dto.RefreshToken ||
                user.RefreshTokenExpiry <= DateTime.UtcNow ||
                user.IsBlocked)
            {
                return Unauthorized("Invalid or expired refresh token.");
            }

            user.RefreshToken = _tokenService.CreateRefreshToken();
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
            await _context.SaveChangesAsync();

            return Ok(CreateAuthResponse(user));
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordDto dto)
        {
            var email = dto.Email.Trim().ToLowerInvariant();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email);

            if (user is not null)
            {
                user.ResetPasswordOtp = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
                user.OtpExpiry = DateTime.UtcNow.AddMinutes(10);
                await _context.SaveChangesAsync();
                await _emailService.SendPasswordResetOtpAsync(user.Email, user.FullName, user.ResetPasswordOtp);
            }

            return Ok("If the email exists, a password reset OTP has been sent.");
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(ResetPasswordDto dto)
        {
            var email = dto.Email.Trim().ToLowerInvariant();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email);

            if (user is null ||
                user.ResetPasswordOtp != dto.Otp ||
                user.OtpExpiry is null ||
                user.OtpExpiry < DateTime.UtcNow)
            {
                return BadRequest("Invalid or expired OTP.");
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            user.ResetPasswordOtp = null;
            user.OtpExpiry = null;
            user.RefreshToken = null;
            user.RefreshTokenExpiry = null;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                              User.FindFirstValue("sub") ??
                              User.FindFirstValue("nameid");

            if (!Guid.TryParse(userIdValue, out var userId))
            {
                return Unauthorized();
            }

            var user = await _context.Users.FindAsync(userId);
            if (user is not null)
            {
                user.RefreshToken = null;
                user.RefreshTokenExpiry = null;
                await _context.SaveChangesAsync();
            }

            return NoContent();
        }

        private AuthResponseDto CreateAuthResponse(User user)
        {
            var token = _tokenService.CreateAccessToken(user);
            return new AuthResponseDto(token, user.RefreshToken!, _tokenService.AccessTokenExpiresAt, user.Email, user.FullName, user.Role);
        }
    }
}

