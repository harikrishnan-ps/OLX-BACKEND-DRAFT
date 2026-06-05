using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using olx_api.Data;
using olx_api.DTOs;
using olx_api.Models;

namespace olx_api.Controllers
{
    [ApiController]
    [Authorize(Roles = "Admin,Moderator")]
    [Route("api/admin")]
    public class AdminController : ControllerBase
    {
        private static readonly HashSet<string> AllowedListingStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "Active",
            "Pending",
            "Rejected",
            "Draft",
            "Sold"
        };

        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("dashboard/stats")]
        public async Task<ActionResult<AdminDashboardStatsDto>> GetDashboardStats()
        {
            var stats = new AdminDashboardStatsDto(
                await _context.Users.CountAsync(),
                await _context.Listings.CountAsync(l => l.Status == "Active"),
                await _context.Listings.CountAsync(l => l.Status == "Pending"),
                await _context.Reports.CountAsync(),
                await _context.Users.CountAsync(u => u.IsBlocked),
                await _context.Listings.CountAsync(l => l.IsFeatured));

            return Ok(stats);
        }

        [HttpPatch("users/{id:guid}/block")]
        public async Task<IActionResult> SetUserBlocked(Guid id, BlockUserDto dto)
        {
            var user = await _context.Users.FindAsync(id);
            if (user is null)
            {
                return NotFound("User was not found.");
            }

            user.IsBlocked = dto.IsBlocked;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpPatch("listings/{id:guid}/status")]
        public async Task<IActionResult> UpdateListingStatus(Guid id, UpdateListingStatusDto dto)
        {
            var listing = await _context.Listings.FindAsync(id);
            if (listing is null)
            {
                return NotFound("Listing was not found.");
            }

            var status = dto.Status.Trim();
            if (!AllowedListingStatuses.Contains(status))
            {
                return BadRequest("Status must be Active, Pending, Rejected, Draft, or Sold.");
            }

            listing.Status = AllowedListingStatuses.First(s => string.Equals(s, status, StringComparison.OrdinalIgnoreCase));
            if (dto.IsFeatured.HasValue)
            {
                listing.IsFeatured = dto.IsFeatured.Value;
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpGet("reports")]
        public async Task<ActionResult> GetReportsFeed()
        {
            var reports = await _context.Reports
                .Include(r => r.Reporter)
                .Include(r => r.ReportedListing)
                .Where(r => r.Status == "Pending")
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new
                {
                    r.Id,
                    r.Reason,
                    r.Status,
                    r.CreatedAt,
                    r.ReporterId,
                    ReporterName = r.Reporter.FullName,
                    r.ReportedListingId,
                    ListingTitle = r.ReportedListing.Title
                })
                .ToListAsync();

            return Ok(reports);
        }

        [HttpPost("categories")]
        public async Task<ActionResult> CreateCategory(UpsertCategoryDto dto)
        {
            if (dto.ParentCategoryId.HasValue &&
                !await _context.Categories.AnyAsync(c => c.Id == dto.ParentCategoryId.Value))
            {
                return BadRequest("Parent category was not found.");
            }

            var category = new Category
            {
                Name = dto.Name.Trim(),
                IconUrl = dto.IconUrl?.Trim() ?? string.Empty,
                ParentCategoryId = dto.ParentCategoryId
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(CreateCategory), new { id = category.Id }, new
            {
                category.Id,
                category.Name,
                category.IconUrl,
                category.ParentCategoryId
            });
        }

        [HttpPut("categories/{id:int}")]
        public async Task<IActionResult> UpdateCategory(int id, UpsertCategoryDto dto)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category is null)
            {
                return NotFound("Category was not found.");
            }

            if (dto.ParentCategoryId == id)
            {
                return BadRequest("Category cannot be its own parent.");
            }

            if (dto.ParentCategoryId.HasValue &&
                !await _context.Categories.AnyAsync(c => c.Id == dto.ParentCategoryId.Value))
            {
                return BadRequest("Parent category was not found.");
            }

            category.Name = dto.Name.Trim();
            category.IconUrl = dto.IconUrl?.Trim() ?? string.Empty;
            category.ParentCategoryId = dto.ParentCategoryId;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("categories/{id:int}")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var category = await _context.Categories
                .Include(c => c.SubCategories)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category is null)
            {
                return NotFound("Category was not found.");
            }

            var hasListings = await _context.Listings.AnyAsync(l => l.CategoryId == id);
            if (hasListings || category.SubCategories.Any())
            {
                return BadRequest("Category cannot be deleted while it has listings or sub-categories.");
            }

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpPut("cms/{slug}")]
        public async Task<IActionResult> UpdateCmsPage(string slug, UpsertStaticPageDto dto)
        {
            var normalizedSlug = slug.Trim().ToLowerInvariant();
            var page = await _context.StaticPages.FirstOrDefaultAsync(p => p.Slug == normalizedSlug);

            if (page is null)
            {
                page = new global::StaticPage
                {
                    Slug = normalizedSlug,
                    Title = dto.Title.Trim(),
                    HtmlContent = dto.HtmlContent
                };
                _context.StaticPages.Add(page);
            }
            else
            {
                page.Title = dto.Title.Trim();
                page.HtmlContent = dto.HtmlContent;
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost("banners")]
        public async Task<ActionResult<BannerDto>> CreateBanner(CreateBannerDto dto)
        {
            var banner = new global::Banner
            {
                ImageUrl = dto.ImageUrl.Trim(),
                PlacementType = dto.PlacementType.Trim(),
                IsActive = dto.IsActive
            };

            _context.Banners.Add(banner);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(CreateBanner), new { id = banner.Id }, ToBannerDto(banner));
        }

        [HttpDelete("banners/{id:int}")]
        public async Task<IActionResult> DeleteBanner(int id)
        {
            var banner = await _context.Banners.FindAsync(id);
            if (banner is null)
            {
                return NotFound("Banner was not found.");
            }

            _context.Banners.Remove(banner);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private static BannerDto ToBannerDto(global::Banner banner)
        {
            return new BannerDto(banner.Id, banner.ImageUrl, banner.PlacementType, banner.IsActive);
        }
    }
}
