using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using olx_api.Data;
using olx_api.DTOs;
using olx_api.Models;
using System.Security.Claims;

namespace olx_api.Controllers
{
    [ApiController]
    [Route("api/media")]
    public class MediaController : ControllerBase
    {
        private const int MaxImagesPerListing = 10;
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public MediaController(ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        [HttpPost("upload/{listingId:guid}")]
        [RequestSizeLimit(50_000_000)]
        public async Task<ActionResult<IEnumerable<ListingImageDto>>> UploadImages(Guid listingId, [FromForm] List<IFormFile> files)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var listing = await _context.Listings
                .Include(l => l.Images)
                .FirstOrDefaultAsync(l => l.Id == listingId && l.Status != "Deleted");

            if (listing == null)
                return NotFound();

            if (listing.UserId != userId.Value)
                return Forbid();

            if (files.Count == 0)
                return BadRequest("At least one image is required.");

            if (listing.Images.Count + files.Count > MaxImagesPerListing)
                return BadRequest("A listing can have at most 10 images.");

            var uploadsRoot = Path.Combine(_environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"), "uploads", "listings", listingId.ToString());
            Directory.CreateDirectory(uploadsRoot);

            var created = new List<ListingImage>();
            foreach (var file in files.Take(MaxImagesPerListing))
            {
                if (file.Length == 0)
                    continue;

                if (file.ContentType == null || !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                    return BadRequest("Only image files are allowed.");

                var extension = Path.GetExtension(file.FileName);
                var fileName = $"{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(uploadsRoot, fileName);

                await using (var stream = System.IO.File.Create(filePath))
                {
                    await file.CopyToAsync(stream);
                }

                var image = new ListingImage
                {
                    ListingId = listingId,
                    ImageUrl = $"/uploads/listings/{listingId}/{fileName}",
                    IsPrimary = listing.Images.Count == 0 && created.Count == 0
                };

                created.Add(image);
                await _context.ListingImages.AddAsync(image);
            }

            await _context.SaveChangesAsync();
            return Ok(created.Select(i => new ListingImageDto(i.Id, i.ImageUrl, i.IsPrimary)));
        }

        private Guid? GetCurrentUserId()
        {
            var value =
                User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                User.FindFirstValue("sub") ??
                User.FindFirstValue("nameid");

            return Guid.TryParse(value, out var userId) ? userId : null;
        }
    }
}
