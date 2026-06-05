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
            "Sold",
            "Expired"
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
                await _context.Users.CountAsync(u => !u.IsBlocked),
                await _context.Users.CountAsync(u => u.IsBlocked),
                await _context.Listings.CountAsync(),
                await _context.Listings.CountAsync(l => l.Status == "Active"),
                await _context.Listings.CountAsync(l => l.Status == "Pending"),
                await _context.Listings.CountAsync(l => l.Status == "Rejected"),
                await _context.Listings.CountAsync(l => l.IsFeatured),
                await _context.Categories.CountAsync(),
                await _context.Reports.CountAsync()
            );

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
                return BadRequest("Status must be Active, Pending, Rejected, Draft, Sold, or Expired.");
            }

            var oldStatus = listing.Status;
            var newStatus = AllowedListingStatuses.First(s => string.Equals(s, status, StringComparison.OrdinalIgnoreCase));
            listing.Status = newStatus;

            if (dto.IsFeatured.HasValue)
            {
                listing.IsFeatured = dto.IsFeatured.Value;
            }

            // Generate notification for status transitions
            if (string.Equals(newStatus, "Active", StringComparison.OrdinalIgnoreCase) && 
                !string.Equals(oldStatus, "Active", StringComparison.OrdinalIgnoreCase))
            {
                var notification = new InAppNotification
                {
                    UserId = listing.UserId,
                    Message = $"Your advertisement '{listing.Title}' has been approved.",
                    Type = "AdApproved",
                    CreatedAt = DateTime.UtcNow
                };
                await _context.InAppNotifications.AddAsync(notification);
            }
            else if (string.Equals(newStatus, "Rejected", StringComparison.OrdinalIgnoreCase) && 
                     !string.Equals(oldStatus, "Rejected", StringComparison.OrdinalIgnoreCase))
            {
                var notification = new InAppNotification
                {
                    UserId = listing.UserId,
                    Message = $"Your advertisement '{listing.Title}' has been rejected by moderation.",
                    Type = "AdRejected",
                    CreatedAt = DateTime.UtcNow
                };
                await _context.InAppNotifications.AddAsync(notification);
            }
            else if (string.Equals(newStatus, "Sold", StringComparison.OrdinalIgnoreCase) && 
                     !string.Equals(oldStatus, "Sold", StringComparison.OrdinalIgnoreCase))
            {
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

        [HttpGet("dashboard/charts")]
        public async Task<ActionResult<object>> GetDashboardCharts()
        {
            var sixMonthsAgo = DateTime.UtcNow.AddMonths(-6);
            var users = await _context.Users
                .Where(u => u.CreatedAt >= sixMonthsAgo)
                .ToListAsync();

            var registrationTrend = users
                .GroupBy(u => u.CreatedAt.ToString("yyyy-MM"))
                .OrderBy(g => g.Key)
                .Select(g => new { Month = g.Key, Count = g.Count() })
                .ToList();

            var listingsBreakdown = await _context.Listings
                .GroupBy(l => l.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            var categoryStats = await _context.Listings
                .Include(l => l.Category)
                .GroupBy(l => l.Category.Name)
                .Select(g => new { Category = g.Key, Count = g.Count() })
                .ToListAsync();

            return Ok(new
            {
                RegistrationTrend = registrationTrend,
                ListingsBreakdown = listingsBreakdown,
                CategoryStats = categoryStats
            });
        }

        [HttpGet("users")]
        public async Task<ActionResult<object>> GetUsers([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var query = _context.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var norm = search.Trim().ToLower();
                query = query.Where(u => u.FullName.ToLower().Contains(norm) || u.Email.ToLower().Contains(norm) || u.PhoneNumber.Contains(norm));
            }

            var totalCount = await query.CountAsync();
            var users = await query
                .OrderByDescending(u => u.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new
                {
                    u.Id,
                    u.FullName,
                    u.Email,
                    u.PhoneNumber,
                    u.Role,
                    u.IsVerified,
                    u.IsBlocked,
                    u.CreatedAt
                })
                .ToListAsync();

            return Ok(new { Items = users, Page = page, PageSize = pageSize, TotalCount = totalCount });
        }

        [HttpGet("users/{id:guid}")]
        public async Task<ActionResult<object>> GetUserDetails(Guid id)
        {
            var user = await _context.Users
                .Include(u => u.Listings)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
            {
                return NotFound("User was not found.");
            }

            return Ok(new
            {
                user.Id,
                user.FullName,
                user.Email,
                user.PhoneNumber,
                user.Role,
                user.IsVerified,
                user.IsBlocked,
                user.UserTier,
                user.AdQuotaRemaining,
                user.CreatedAt,
                TotalListings = user.Listings.Count
            });
        }

        [HttpDelete("users/{id:guid}")]
        public async Task<IActionResult> DeleteUser(Guid id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound("User was not found.");
            }

            var userListings = await _context.Listings.Where(l => l.UserId == id).ToListAsync();
            foreach (var listing in userListings)
            {
                listing.Status = "Deleted";
                listing.DeletedAt = DateTime.UtcNow;
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpGet("listings")]
        public async Task<ActionResult<object>> GetAdminListings([FromQuery] string? search, [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var query = _context.Listings
                .Include(l => l.User)
                .Include(l => l.Category)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(l => l.Title.Contains(search) || l.Description.Contains(search));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(l => l.Status == status);
            }

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(l => l.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(l => new
                {
                    l.Id,
                    l.Title,
                    l.Price,
                    l.Status,
                    l.IsFeatured,
                    l.CreatedAt,
                    SellerName = l.User.FullName,
                    CategoryName = l.Category.Name
                })
                .ToListAsync();

            return Ok(new { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount });
        }

        [HttpDelete("listings/{id:guid}")]
        public async Task<IActionResult> DeleteListingAdmin(Guid id)
        {
            var listing = await _context.Listings.FindAsync(id);
            if (listing == null)
            {
                return NotFound("Listing was not found.");
            }

            listing.Status = "Deleted";
            listing.DeletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpGet("reports/{id:guid}")]
        public async Task<ActionResult<object>> GetReportDetails(Guid id)
        {
            var report = await _context.Reports
                .Include(r => r.Reporter)
                .Include(r => r.ReportedListing)
                    .ThenInclude(l => l.User)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (report == null)
            {
                return NotFound("Report was not found.");
            }

            return Ok(new
            {
                report.Id,
                report.Reason,
                report.Status,
                report.CreatedAt,
                Reporter = new { report.Reporter.Id, report.Reporter.FullName, report.Reporter.Email },
                Listing = new 
                { 
                    report.ReportedListing.Id, 
                    report.ReportedListing.Title, 
                    report.ReportedListing.Price, 
                    report.ReportedListing.Status,
                    Seller = new { report.ReportedListing.User.Id, report.ReportedListing.User.FullName, report.ReportedListing.User.Email }
                }
            });
        }

        [HttpPatch("reports/{id:guid}/resolve")]
        public async Task<IActionResult> ResolveReport(Guid id, [FromQuery] string status = "Reviewed")
        {
            var report = await _context.Reports.FindAsync(id);
            if (report == null)
            {
                return NotFound("Report was not found.");
            }

            if (status != "Reviewed" && status != "Dismissed")
            {
                return BadRequest("Status must be Reviewed or Dismissed.");
            }

            report.Status = status;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpGet("reviews")]
        public async Task<ActionResult<object>> GetReviewsAdmin([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var query = _context.Reviews
                .Include(r => r.Reviewer)
                .Include(r => r.TargetUser);

            var totalCount = await query.CountAsync();
            var reviews = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new
                {
                    r.Id,
                    r.Rating,
                    r.Comment,
                    r.CreatedAt,
                    ReviewerName = r.Reviewer.FullName,
                    TargetUserName = r.TargetUser.FullName
                })
                .ToListAsync();

            return Ok(new { Items = reviews, Page = page, PageSize = pageSize, TotalCount = totalCount });
        }

        [HttpDelete("reviews/{id:guid}")]
        public async Task<IActionResult> DeleteReviewAdmin(Guid id)
        {
            var review = await _context.Reviews.FindAsync(id);
            if (review == null)
            {
                return NotFound("Review was not found.");
            }

            _context.Reviews.Remove(review);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private static BannerDto ToBannerDto(global::Banner banner)
        {
            return new BannerDto(banner.Id, banner.ImageUrl, banner.PlacementType, banner.IsActive);
        }
    }
}
