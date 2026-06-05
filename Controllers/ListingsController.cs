using Microsoft.AspNetCore.Mvc;
using olx_api.DTOs;
using olx_api.Models;
using olx_api.Repositories;

namespace olx_api.Controllers
{
    [ApiController]
    [Route("api/listings")]
    public class ListingsController : ControllerBase
    {
        private readonly IListingRepository _listingRepo;

        public ListingsController(IListingRepository listingRepo)
        {
            _listingRepo = listingRepo;
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
