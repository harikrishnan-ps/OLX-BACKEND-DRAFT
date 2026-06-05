using Microsoft.EntityFrameworkCore;
using olx_api.Data;
using olx_api.Models;

namespace olx_api.Repositories
{
    public class ListingRepository : IListingRepository
    {
        private readonly ApplicationDbContext _context;

        public ListingRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Listing>> GetAllAsync(
            string? search,
            int? categoryId,
            string? city,
            int page,
            int pageSize)
        {
            var query = _context.Listings
                .Include(l => l.Images)
                .Include(l => l.Category)
                .Include(l => l.User)
                .Include(l => l.City) 
                .Where(l => l.Status == "Active");

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(l =>
                    l.Title.Contains(search) ||
                    l.Description.Contains(search));
            }

            if (categoryId.HasValue)
            {
                query = query.Where(l => l.CategoryId == categoryId.Value);
            }

            if (!string.IsNullOrEmpty(city))
            {
                query = query.Where(l =>
                    l.City != null &&
                    l.City.Name.ToLower() == city.ToLower());
            }

            return await query
                .OrderByDescending(l => l.IsFeatured)      // Featured first
                .ThenByDescending(l => l.LastBoostedAt)   // Recently boosted next
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<Listing?> GetByIdAsync(Guid id)
        {
            return await _context.Listings
                .Include(l => l.Images)
                .Include(l => l.Category)
                .Include(l => l.User)
                .Include(l => l.City)
                .FirstOrDefaultAsync(l => l.Id == id);
        }

        public async Task AddAsync(Listing listing)
        {
            await _context.Listings.AddAsync(listing);
        }

        public async Task UpdateAsync(Listing listing)
        {
            _context.Listings.Update(listing);
            await Task.CompletedTask;
        }

        public async Task DeleteAsync(Listing listing)
        {
            _context.Listings.Remove(listing);
            await Task.CompletedTask;
        }

        public async Task<bool> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync() > 0;
        }
    }
}
