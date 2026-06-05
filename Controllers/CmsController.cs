using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using olx_api.Data;
using olx_api.Models;

namespace olx_api.Controllers
{
    [ApiController]
    [Route("api")]
    public class CmsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public CmsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("cms/{slug}")]
        public async Task<ActionResult<object>> GetPageBySlug(string slug)
        {
            var page = await _context.StaticPages
                .FirstOrDefaultAsync(p => p.Slug == slug.Trim().ToLowerInvariant());

            if (page == null)
            {
                return NotFound("Page was not found.");
            }

            return Ok(new
            {
                page.Id,
                page.Slug,
                page.Title,
                page.HtmlContent
            });
        }

        [HttpGet("banners")]
        public async Task<ActionResult<IEnumerable<object>>> GetActiveBanners([FromQuery] string? placementType)
        {
            var query = _context.Banners.Where(b => b.IsActive);

            if (!string.IsNullOrWhiteSpace(placementType))
            {
                query = query.Where(b => b.PlacementType.ToLower() == placementType.Trim().ToLower());
            }

            var banners = await query.ToListAsync();

            return Ok(banners.Select(b => new
            {
                b.Id,
                b.ImageUrl,
                b.PlacementType,
                b.IsActive
            }));
        }
    }
}
