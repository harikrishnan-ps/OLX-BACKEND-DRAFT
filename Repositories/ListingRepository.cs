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

        public async Task<(IEnumerable<Listing> Items, int TotalCount)> GetAllAsync(
            string? search,
            int? categoryId,
            int? cityId,
            int? stateId,
            decimal? minPrice,
            decimal? maxPrice,
            string? condition,
            string? status,
            string? specifications,
            int page,
            int pageSize
        )
        {
            var query = _context.Listings
                .Include(l => l.Images)
                .Include(l => l.Category)
                .Include(l => l.User)
                .Include(l => l.City)
                    .ThenInclude(c => c.State)
                        .ThenInclude(s => s.Country)
                .Where(l => l.Status != "Deleted");

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(l => l.Status == status);
            }
            else
            {
                query = query.Where(l => l.Status == "Active");
            }

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(l => l.Title.Contains(search) || l.Description.Contains(search));

            if (categoryId.HasValue)
            {
                query = query.Where(l => l.CategoryId == categoryId.Value);
            }

            if (cityId.HasValue)
                query = query.Where(l => l.CityId == cityId.Value);

            if (stateId.HasValue)
                query = query.Where(l => l.City.StateId == stateId.Value);

            if (minPrice.HasValue)
                query = query.Where(l => l.Price >= minPrice.Value);

            if (maxPrice.HasValue)
                query = query.Where(l => l.Price <= maxPrice.Value);

            if (!string.IsNullOrWhiteSpace(condition))
                query = query.Where(l => l.Condition == condition);

            if (!string.IsNullOrWhiteSpace(specifications))
                query = query.Where(l => l.SpecificationsJson != null && l.SpecificationsJson.Contains(specifications));

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(l => l.IsFeatured)      // 1st Priority: Featured ads pin to top
                .ThenByDescending(l => l.LastBoostedAt)   // 2nd Priority: Most recently bumped/created ads
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<Listing?> GetByIdAsync(Guid id)
        {
            return await _context.Listings
                .Include(l => l.Images)
                .Include(l => l.Category)
                .Include(l => l.User)
                .Include(l => l.City)
                    .ThenInclude(c => c.State)
                        .ThenInclude(s => s.Country)
                .FirstOrDefaultAsync(l => l.Id == id && l.Status != "Deleted");

        public async Task<IEnumerable<Listing>> GetSimilarAsync(Guid id, int limit)
        {
            var source = await _context.Listings
                .Include(l => l.City)
                .FirstOrDefaultAsync(l => l.Id == id && l.Status != "Deleted");

            if (source == null)
                return Enumerable.Empty<Listing>();

            return await _context.Listings
                .Include(l => l.Images)
                .Include(l => l.Category)
                .Include(l => l.User)
                .Include(l => l.City)
                    .ThenInclude(c => c.State)
                        .ThenInclude(s => s.Country)
                .Where(l =>
                    l.Id != id &&
                    l.Status == "Active" &&
                    l.CategoryId == source.CategoryId)
                .OrderByDescending(l => l.CityId == source.CityId)
                .ThenByDescending(l => l.City.StateId == source.City.StateId)
                .ThenByDescending(l => l.IsFeatured)
                .ThenByDescending(l => l.LastBoostedAt)
                .Take(limit)
                .ToListAsync();
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
