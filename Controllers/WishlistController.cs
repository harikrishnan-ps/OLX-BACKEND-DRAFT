using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using olx_api.Data;
using olx_api.DTOs;
using olx_api.Models;
using System.Security.Claims;

namespace olx_api.Controllers
{
    [ApiController]
    [Route("api/wishlist")]
    public class WishlistController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public WishlistController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ListingResponseDto>>> GetWishlist()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var listings = await _context.Favorites
                .Where(f => f.UserId == userId.Value && f.Listing.Status != "Deleted")
                .Include(f => f.Listing.Images)
                .Include(f => f.Listing.Category)
                .Include(f => f.Listing.User)
                .Include(f => f.Listing.City)
                    .ThenInclude(c => c.State)
                        .ThenInclude(s => s.Country)
                .OrderByDescending(f => f.AddedAt)
                .Select(f => f.Listing)
                .ToListAsync();

            return Ok(listings.Select(MapListing));
        }

        [HttpPost("{listingId:guid}")]
        public async Task<ActionResult<object>> ToggleWishlistItem(Guid listingId)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var listingExists = await _context.Listings.AnyAsync(l => l.Id == listingId && l.Status != "Deleted");
            if (!listingExists)
                return NotFound();

            var favorite = await _context.Favorites.FindAsync(userId.Value, listingId);
            var isWishlisted = favorite == null;

            if (favorite == null)
            {
                await _context.Favorites.AddAsync(new Favorite
                {
                    UserId = userId.Value,
                    ListingId = listingId,
                    AddedAt = DateTime.UtcNow
                });
            }
            else
            {
                _context.Favorites.Remove(favorite);
            }

            await _context.SaveChangesAsync();
            return Ok(new { listingId, isWishlisted });
        }

        private Guid? GetCurrentUserId()
        {
            var value =
                User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                User.FindFirstValue("sub") ??
                User.FindFirstValue("nameid");

            return Guid.TryParse(value, out var userId) ? userId : null;
        }

        private static ListingResponseDto MapListing(Listing listing)
        {
            var city = listing.City;
            var state = city.State;
            var country = state.Country;

            return new ListingResponseDto(
                listing.Id,
                listing.Title,
                listing.Description,
                listing.Price,
                listing.IsNegotiable,
                listing.CityId,
                city.Name,
                state.Id,
                state.Name,
                country.Id,
                country.Name,
                listing.Condition,
                listing.Status,
                listing.CreatedAt,
                listing.LastBoostedAt,
                listing.SpecificationsJson,
                listing.UserId,
                listing.User.FullName,
                listing.User.PhoneNumber,
                new CategoryDto(listing.Category.Id, listing.Category.Name, listing.Category.IconUrl, listing.Category.ParentCategoryId),
                listing.Images
                    .OrderByDescending(i => i.IsPrimary)
                    .Select(i => new ListingImageDto(i.Id, i.ImageUrl, i.IsPrimary))
            );
        }
    }
}
