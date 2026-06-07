using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using olx_api.Data;
using olx_api.DTOs;
using olx_api.Models;
using olx_api.Repositories;
using System.Security.Claims;

namespace olx_api.Controllers
{
    [ApiController]
    [Route("api/listings")]
    public class ListingsController : ControllerBase
    {
        private readonly IListingRepository _listingRepo;
        private readonly ApplicationDbContext _context;

        public ListingsController(IListingRepository listingRepo, ApplicationDbContext context)
        {
            _listingRepo = listingRepo;
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<PagedResultDto<ListingResponseDto>>> GetListings([FromQuery] ListingQueryDto query)
        {
            var page = Math.Max(query.Page, 1);
            var pageSize = Math.Clamp(query.PageSize, 1, 100);

            var (items, totalCount) = await _listingRepo.GetAllAsync(
                query.Search,
                query.CategoryId,
                query.CityId,
                query.StateId,
                query.MinPrice,
                query.MaxPrice,
                query.Condition,
                query.Status,
                query.Specifications,
                query.DatePosted,
                page,
                pageSize
            );

            return Ok(new PagedResultDto<ListingResponseDto>(
                items.Select(MapListing),
                page,
                pageSize,
                totalCount
            ));
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ListingResponseDto>> GetListing(Guid id)
        {
            var listing = await _listingRepo.GetByIdAsync(id);
            if (listing == null)
                return NotFound();

            return Ok(MapListing(listing));
        }

        [HttpGet("{id:guid}/similar")]
        public async Task<ActionResult<IEnumerable<ListingResponseDto>>> GetSimilarListings(Guid id, [FromQuery] int limit = 12)
        {
            if (await _listingRepo.GetByIdAsync(id) == null)
                return NotFound();

            var listings = await _listingRepo.GetSimilarAsync(id, Math.Clamp(limit, 1, 50));
            return Ok(listings.Select(MapListing));
        }

        [Authorize]
        [HttpGet("my")]
        public async Task<ActionResult<IEnumerable<ListingResponseDto>>> GetMyListings()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var listings = await _context.Listings
                .Include(l => l.Images)
                .Include(l => l.Category)
                .Include(l => l.User)
                .Include(l => l.City)
                    .ThenInclude(c => c.State)
                        .ThenInclude(s => s.Country)
                .Where(l => l.UserId == userId.Value && l.Status != "Deleted")
                .OrderByDescending(l => l.CreatedAt)
                .ToListAsync();

            return Ok(listings.Select(MapListing));
        }

        [Authorize]
        [HttpPatch("{id:guid}/renew")]
        public async Task<ActionResult<ListingResponseDto>> RenewListing(Guid id)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var listing = await _listingRepo.GetByIdAsync(id);
            if (listing == null)
                return NotFound();

            if (listing.UserId != userId.Value)
                return Forbid();

            listing.CreatedAt = DateTime.UtcNow;
            listing.LastBoostedAt = DateTime.UtcNow;
            if (listing.Status == "Expired" || listing.Status == "Sold")
            {
                listing.Status = "Active";
            }

            await _listingRepo.UpdateAsync(listing);
            await _listingRepo.SaveChangesAsync();

            var updated = await _listingRepo.GetByIdAsync(id);
            return Ok(MapListing(updated!));
        }

        [HttpPost]
        public async Task<ActionResult<ListingResponseDto>> CreateListing(CreateListingDto dto)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var user = await _context.Users.FindAsync(userId.Value);
            if (user == null)
                return Unauthorized();

            var status = string.Equals(dto.Status, "Draft", StringComparison.OrdinalIgnoreCase) ? "Draft" : "Active";
            if (status == "Active")
            {
                return BadRequest("New listings must be created as 'Draft'. You can publish (set to Active) after uploading at least 1 image.");
            }

            if (user.AdQuotaRemaining <= 0)
                return BadRequest("Ad quota exhausted.");

            if (!await _context.Cities.AnyAsync(c => c.Id == dto.CityId))
                return BadRequest("Invalid city.");

            if (!await _context.Categories.AnyAsync(c => c.Id == dto.CategoryId))
                return BadRequest("Invalid category.");

            var listing = new Listing
            {
                Title = dto.Title.Trim(),
                Description = dto.Description.Trim(),
                Price = dto.Price,
                IsNegotiable = dto.IsNegotiable,
                CityId = dto.CityId,
                CategoryId = dto.CategoryId,
                Condition = dto.Condition.Trim(),
                SpecificationsJson = dto.SpecificationsJson,
                Status = status,
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow,
                LastBoostedAt = DateTime.UtcNow
            };

            await _listingRepo.AddAsync(listing);
            await _listingRepo.SaveChangesAsync();

            var created = await _listingRepo.GetByIdAsync(listing.Id);
            return CreatedAtAction(nameof(GetListing), new { id = listing.Id }, MapListing(created!));
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<ListingResponseDto>> UpdateListing(Guid id, UpdateListingDto dto)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var listing = await _listingRepo.GetByIdAsync(id);
            if (listing == null)
                return NotFound();

            if (listing.UserId != userId.Value)
                return Forbid();

            // if (string.Equals(dto.Status, "Active", StringComparison.OrdinalIgnoreCase) && (listing.Images == null || !listing.Images.Any()))
            // {
            //     return BadRequest("A listing must have at least 1 image to be published (Active).");
            // }

            var wasActive = string.Equals(listing.Status, "Active", StringComparison.OrdinalIgnoreCase);
            var isNowActive = string.Equals(dto.Status, "Active", StringComparison.OrdinalIgnoreCase);

            if (isNowActive && !wasActive)
            {
                var user = await _context.Users.FindAsync(userId.Value);
                if (user == null)
                    return Unauthorized();

                if (user.AdQuotaRemaining <= 0)
                    return BadRequest("Ad quota exhausted.");

                user.AdQuotaRemaining--;
            }

            if (!await _context.Cities.AnyAsync(c => c.Id == dto.CityId))
                return BadRequest("Invalid city.");

            if (!await _context.Categories.AnyAsync(c => c.Id == dto.CategoryId))
                return BadRequest("Invalid category.");

            listing.Title = dto.Title.Trim();
            listing.Description = dto.Description.Trim();
            listing.Price = dto.Price;
            listing.IsNegotiable = dto.IsNegotiable;
            listing.CityId = dto.CityId;
            listing.CategoryId = dto.CategoryId;
            listing.Condition = dto.Condition.Trim();
            listing.SpecificationsJson = dto.SpecificationsJson;
            var oldStatus = listing.Status;
            listing.Status = dto.Status.Trim();

            await _listingRepo.UpdateAsync(listing);

            // Notify wishlisted users
            var wasSold = string.Equals(oldStatus, "Sold", StringComparison.OrdinalIgnoreCase);
            var isNowSold = string.Equals(dto.Status, "Sold", StringComparison.OrdinalIgnoreCase);

            var favoritedUsers = await _context.Favorites
                .Where(f => f.ListingId == id)
                .Select(f => f.UserId)
                .ToListAsync();

            foreach (var favUserId in favoritedUsers)
            {
                var notify = new InAppNotification
                {
                    UserId = favUserId,
                    Message = isNowSold && !wasSold 
                        ? $"The item '{listing.Title}' in your wishlist has been marked as Sold."
                        : $"The wishlisted item '{listing.Title}' has been updated.",
                    Type = isNowSold && !wasSold ? "ProductSold" : "WishlistProductUpdate",
                    CreatedAt = DateTime.UtcNow
                };
                await _context.InAppNotifications.AddAsync(notify);
            }

            await _listingRepo.SaveChangesAsync();

            var updated = await _listingRepo.GetByIdAsync(id);
            return Ok(MapListing(updated!));
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteListing(Guid id)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var listing = await _listingRepo.GetByIdAsync(id);
            if (listing == null)
                return NotFound();

            if (listing.UserId != userId.Value)
                return Forbid();

            listing.Status = "Deleted";
            listing.DeletedAt = DateTime.UtcNow;
            await _listingRepo.UpdateAsync(listing);
            await _listingRepo.SaveChangesAsync();

            return NoContent();
        }

        [HttpPatch("{id:guid}/sold")]
        public async Task<ActionResult<ListingResponseDto>> MarkListingSold(Guid id)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var listing = await _listingRepo.GetByIdAsync(id);
            if (listing == null)
                return NotFound();

            if (listing.UserId != userId.Value)
                return Forbid();

            listing.Status = "Sold";
            var listingMessages = await _context.Messages
                .Where(m => m.ListingId == id && !m.IsRead)
                .ToListAsync();

            foreach (var message in listingMessages)
                message.IsRead = true;

            await _listingRepo.UpdateAsync(listing);

            // Notify wishlisted users
            var favorites = await _context.Favorites
                .Where(f => f.ListingId == id)
                .ToListAsync();

            foreach (var fav in favorites)
            {
                var notify = new InAppNotification
                {
                    UserId = fav.UserId,
                    Message = $"The item '{listing.Title}' in your wishlist has been marked as Sold.",
                    Type = "ProductSold",
                    CreatedAt = DateTime.UtcNow
                };
                await _context.InAppNotifications.AddAsync(notify);
            }

            await _listingRepo.SaveChangesAsync();

            var updated = await _listingRepo.GetByIdAsync(id);
            return Ok(MapListing(updated!));
        }

        [HttpPatch("{id:guid}/boost")]
        public async Task<ActionResult<ListingResponseDto>> BoostListing(Guid id)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var listing = await _listingRepo.GetByIdAsync(id);
            if (listing == null)
                return NotFound();

            if (listing.UserId != userId.Value)
                return Forbid();

            if (listing.Status != "Active")
                return BadRequest("Only active listings can be boosted.");

            listing.LastBoostedAt = DateTime.UtcNow;
            await _listingRepo.UpdateAsync(listing);
            await _listingRepo.SaveChangesAsync();

            var updated = await _listingRepo.GetByIdAsync(id);
            return Ok(MapListing(updated!));
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
                new CategoryDto(
                    listing.Category.Id,
                    listing.Category.Name,
                    listing.Category.IconUrl,
                    listing.Category.ParentCategoryId
                ),
                listing.Images
                    .OrderByDescending(i => i.IsPrimary)
                    .Select(i => new ListingImageDto(i.Id, i.ImageUrl, i.IsPrimary))
            );
        }
    }
}
