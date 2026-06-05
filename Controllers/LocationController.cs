using Microsoft.AspNetCore.Mvc;
using olx_api.DTOs;
using olx_api.Repositories;

namespace olx_api.Controllers
{
    [ApiController]
    [Route("api/locations")]
    public class LocationController : ControllerBase
    {
        private readonly ILocationRepository _locationRepo;

        public LocationController(ILocationRepository locationRepo) =>
            _locationRepo = locationRepo;

        [HttpGet("countries")]
        public async Task<ActionResult<IEnumerable<CountryDto>>> GetCountries()
        {
            var countries = await _locationRepo.GetCountriesAsync();
            return Ok(countries.Select(c => new CountryDto(c.Id, c.Name)));
        }

        [HttpGet("countries/{id}/states")]
        public async Task<ActionResult<IEnumerable<StateDto>>> GetStatesByCountry(int id)
        {
            if (!await _locationRepo.CountryExistsAsync(id))
                return NotFound();

            var states = await _locationRepo.GetStatesByCountryAsync(id);
            return Ok(states.Select(s => new StateDto(s.Id, s.Name, s.CountryId)));
        }

        [HttpGet("states/{id}/cities")]
        public async Task<ActionResult<IEnumerable<CityDto>>> GetCitiesByState(int id)
        {
            if (!await _locationRepo.StateExistsAsync(id))
                return NotFound();

            var cities = await _locationRepo.GetCitiesByStateAsync(id);
            return Ok(cities.Select(c => new CityDto(c.Id, c.Name, c.StateId)));
        }
    }
}
