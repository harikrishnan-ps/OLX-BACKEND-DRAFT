using olx_api.Models;

namespace olx_api.Repositories
{
    public interface ILocationRepository
    {
        Task<IEnumerable<Country>> GetCountriesAsync();
        Task<bool> CountryExistsAsync(int countryId);
        Task<IEnumerable<State>> GetStatesByCountryAsync(int countryId);
        Task<bool> StateExistsAsync(int stateId);
        Task<IEnumerable<City>> GetCitiesByStateAsync(int stateId);
    }
}