using Microsoft.AspNetCore.Authorization;
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

        [HttpPatch("{id:guid}/primary")]
        public async Task<ActionResult<ListingImageDto>> SetPrimaryImage(Guid id)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var image = await _context.ListingImages
                .Include(i => i.Listing)
                .FirstOrDefaultAsync(i => i.Id == id && i.Listing.Status != "Deleted");

            if (image == null)
                return NotFound();

            if (image.Listing.UserId != userId.Value)
                return Forbid();

            var listingImages = await _context.ListingImages
                .Where(i => i.ListingId == image.ListingId)
                .ToListAsync();

            foreach (var listingImage in listingImages)
                listingImage.IsPrimary = listingImage.Id == id;

            await _context.SaveChangesAsync();
            return Ok(new ListingImageDto(image.Id, image.ImageUrl, true));
        }

        [HttpPost("upload/profile")]
        [RequestSizeLimit(10_000_000)]
        [Authorize]
        public async Task<ActionResult<object>> UploadProfilePicture([FromForm] IFormFile file)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var user = await _context.Users.FindAsync(userId.Value);
            if (user == null)
                return Unauthorized();

            if (file == null || file.Length == 0)
                return BadRequest("File is required.");

            if (file.ContentType == null || !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Only image files are allowed.");

            var uploadsRoot = Path.Combine(_environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"), "uploads", "profiles");
            Directory.CreateDirectory(uploadsRoot);

            var extension = Path.GetExtension(file.FileName);
            var fileName = $"{userId.Value}{extension}";
            var filePath = Path.Combine(uploadsRoot, fileName);

            await using (var stream = System.IO.File.Create(filePath))
            {
                await file.CopyToAsync(stream);
            }

            user.ProfilePictureUrl = $"/uploads/profiles/{fileName}";
            await _context.SaveChangesAsync();

            return Ok(new { profilePictureUrl = user.ProfilePictureUrl });
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
