using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using olx_api.Data;
using olx_api.DTOs;
using olx_api.Models;

namespace olx_api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/reviews")]
    public class ReviewsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ReviewsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<ActionResult> CreateReview(CreateReviewDto dto)
        {
            var reviewerId = GetCurrentUserId();
            if (reviewerId is null)
            {
                return Unauthorized();
            }

            if (dto.Rating is < 1 or > 5)
            {
                return BadRequest("Rating must be between 1 and 5.");
            }

            if (reviewerId.Value == dto.TargetUserId)
            {
                return BadRequest("You cannot review your own account.");
            }

            var targetUserExists = await _context.Users.AnyAsync(u => u.Id == dto.TargetUserId && !u.IsBlocked);
            if (!targetUserExists)
            {
                return NotFound("Target seller was not found.");
            }

            var review = new Review
            {
                ReviewerId = reviewerId.Value,
                TargetUserId = dto.TargetUserId,
                Rating = dto.Rating,
                Comment = dto.Comment?.Trim() ?? string.Empty
            };

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(CreateReview), new { id = review.Id }, new
            {
                review.Id,
                review.TargetUserId,
                review.Rating,
                review.Comment,
                review.CreatedAt
            });
        }

        [AllowAnonymous]
        [HttpGet("seller/{sellerId:guid}")]
        public async Task<ActionResult<object>> GetSellerReviews(Guid sellerId)
        {
            var reviews = await _context.Reviews
                .Include(r => r.Reviewer)
                .Where(r => r.TargetUserId == sellerId)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new
                {
                    r.Id,
                    r.Rating,
                    r.Comment,
                    r.CreatedAt,
                    r.ReviewerId,
                    ReviewerName = r.Reviewer.FullName,
                    ReviewerProfilePicture = r.Reviewer.ProfilePictureUrl
                })
                .ToListAsync();

            var averageRating = reviews.Any() ? Math.Round(reviews.Average(r => r.Rating), 1) : 0.0;

            return Ok(new
            {
                SellerId = sellerId,
                AverageRating = averageRating,
                TotalReviews = reviews.Count,
                Reviews = reviews
            });
        }

        private Guid? GetCurrentUserId()
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(userIdValue, out var userId) ? userId : null;
        }
    }
}
