// DTOs/MessageDtos.cs
namespace olx_api.DTOs
{
    public record SendMessageDto(Guid ReceiverId, Guid? ListingId, string Content);
    
    public record MessageResponseDto(
        Guid Id, string Content, DateTime SentAt, bool IsRead, 
        Guid SenderId, string SenderName, Guid ReceiverId
    );

    public record DeleteConversationDto(Guid OtherUserId, Guid? ListingId);
}
