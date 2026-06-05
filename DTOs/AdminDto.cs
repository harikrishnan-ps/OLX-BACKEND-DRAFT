namespace olx_api.DTOs
{
    public record CreateReportDto(Guid ReportedListingId, string Reason);
    public record CreateReviewDto(Guid TargetUserId, int Rating, string? Comment);
    public record NotificationDto(Guid Id, string Message, string Type, bool IsRead, DateTime CreatedAt);
    public record AdminDashboardStatsDto(int TotalUsers, int ActiveAds, int PendingAds, int TotalReports, int BlockedUsers, int FeaturedAds);
    public record BlockUserDto(bool IsBlocked);
    public record UpdateListingStatusDto(string Status, bool? IsFeatured);
    public record UpsertCategoryDto(string Name, string? IconUrl, int? ParentCategoryId);
    public record UpsertStaticPageDto(string Title, string HtmlContent);
    public record BannerDto(int Id, string ImageUrl, string PlacementType, bool IsActive);
    public record CreateBannerDto(string ImageUrl, string PlacementType, bool IsActive = true);
}
