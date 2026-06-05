namespace olx_api.DTOs
{
    public record CountryDto(int Id, string Name);
    public record StateDto(int Id, string Name, int CountryId);
    public record CityDto(int Id, string Name, int StateId);
}
