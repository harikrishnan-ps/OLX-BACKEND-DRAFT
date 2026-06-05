// Repositories/IListingRepository.cs
using olx_api.Models;

namespace olx_api.Repositories
{
    public interface IListingRepository
    {
        Task<(IEnumerable<Listing> Items, int TotalCount)> GetAllAsync(
            string? search,
            int? categoryId,
            int? cityId,
            int? stateId,
            decimal? minPrice,
            decimal? maxPrice,
            string? condition,
            string? status,
            string? specifications,
            string? datePosted,
            int page,
            int pageSize
        );
        Task<Listing?> GetByIdAsync(Guid id);
        Task<IEnumerable<Listing>> GetSimilarAsync(Guid id, int limit);
        Task AddAsync(Listing listing);
        Task UpdateAsync(Listing listing);
        Task DeleteAsync(Listing listing);
        Task<bool> SaveChangesAsync();
    }
}
