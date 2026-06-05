using Microsoft.EntityFrameworkCore;
using olx_api.Data;
using olx_api.Models;

namespace olx_api.Repositories
{
    public class LocationRepository : ILocationRepository
    {
        private readonly ApplicationDbContext _context;

        public LocationRepository(ApplicationDbContext context) => _context = context;

        public async Task<IEnumerable<Country>> GetCountriesAsync() =>
            await _context.Countries.OrderBy(c => c.Name).ToListAsync();

        public async Task<bool> CountryExistsAsync(int countryId) =>
            await _context.Countries.AnyAsync(c => c.Id == countryId);

        public async Task<IEnumerable<State>> GetStatesByCountryAsync(int countryId) =>
            await _context.States
                .Where(s => s.CountryId == countryId)
                .OrderBy(s => s.Name)
                .ToListAsync();

        public async Task<bool> StateExistsAsync(int stateId) =>
            await _context.States.AnyAsync(s => s.Id == stateId);

        public async Task<IEnumerable<City>> GetCitiesByStateAsync(int stateId) =>
            await _context.Cities
                .Where(c => c.StateId == stateId)
                .OrderBy(c => c.Name)
                .ToListAsync();
    }
}
