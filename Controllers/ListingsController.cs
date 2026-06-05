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

        [HttpPost]
        public async Task<ActionResult<ListingResponseDto>> CreateListing(CreateListingDto dto)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var user = await _context.Users.FindAsync(userId.Value);
            if (user == null)
                return Unauthorized();

            if (!string.Equals(dto.Status, "Draft", StringComparison.OrdinalIgnoreCase) && user.AdQuotaRemaining <= 0)
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
                Status = string.Equals(dto.Status, "Draft", StringComparison.OrdinalIgnoreCase) ? "Draft" : "Active",
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow,
                LastBoostedAt = DateTime.UtcNow
            };

            await _listingRepo.AddAsync(listing);

            if (listing.Status == "Active")
                user.AdQuotaRemaining--;

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
            listing.Status = dto.Status.Trim();

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
