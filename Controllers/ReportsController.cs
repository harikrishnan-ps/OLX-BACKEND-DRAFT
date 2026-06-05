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
    [Route("api/reports")]
    public class ReportsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ReportsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<ActionResult> ReportListing(CreateReportDto dto)
        {
            var reporterId = GetCurrentUserId();
            if (reporterId is null)
            {
                return Unauthorized();
            }

            var reason = dto.Reason.Trim();
            if (string.IsNullOrWhiteSpace(reason))
            {
                return BadRequest("Report reason is required.");
            }

            var allowedReasons = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Spam",
                "Duplicate Listing",
                "Fake Product",
                "Offensive Content",
                "Scam"
            };

            if (!allowedReasons.Contains(reason))
            {
                return BadRequest("Invalid report reason. Allowed reasons are: Spam, Duplicate Listing, Fake Product, Offensive Content, Scam.");
            }

            var listing = await _context.Listings.FirstOrDefaultAsync(l => l.Id == dto.ReportedListingId);
            if (listing is null)
            {
                return NotFound("Listing was not found.");
            }

            var report = new Report
            {
                ReporterId = reporterId.Value,
                ReportedListingId = listing.Id,
                Reason = reason
            };

            _context.Reports.Add(report);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(ReportListing), new { id = report.Id }, new
            {
                report.Id,
                report.ReportedListingId,
                report.Reason,
                report.Status,
                report.CreatedAt
            });
        }

        private Guid? GetCurrentUserId()
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(userIdValue, out var userId) ? userId : null;
        }
    }
}
