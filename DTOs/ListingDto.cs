// DTOs/ListingDtos.cs
namespace olx_api.DTOs
{
    public record CategoryDto(int Id, string Name, string IconUrl, int? ParentCategoryId);

    public record ListingQueryDto(
        string? Search,
        int? CategoryId,
        int? CityId,
        int? StateId,
        decimal? MinPrice,
        decimal? MaxPrice,
        string? Condition,
        string? Status,
        string? Specifications,
        int Page = 1,
        int PageSize = 20
    );

    public record CreateListingDto(
        string Title,
        string Description,
        decimal Price,
        bool IsNegotiable,
        int CityId,
        int CategoryId,
        string Condition,
        string? SpecificationsJson,
        string Status = "Active"
    );

    public record UpdateListingDto(
        string Title,
        string Description,
        decimal Price,
        bool IsNegotiable,
        int CityId,
        int CategoryId,
        string Condition,
        string? SpecificationsJson,
        string Status
    );

    public record ListingImageDto(Guid Id, string ImageUrl, bool IsPrimary);
    
    public record ListingResponseDto(
        Guid Id, string Title, string Description, decimal Price, bool IsNegotiable, 
        int CityId, string City, int StateId, string State, int CountryId, string Country,
        string Condition, string Status, DateTime CreatedAt, DateTime LastBoostedAt,
        string? SpecificationsJson,
        Guid UserId, string SellerName, string SellerPhone,
        CategoryDto Category, IEnumerable<ListingImageDto> Images
    );

    public record PagedResultDto<T>(IEnumerable<T> Items, int Page, int PageSize, int TotalCount);
}
