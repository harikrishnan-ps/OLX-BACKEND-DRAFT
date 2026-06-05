namespace olx_api.Models
{
    public class ConversationDeletion
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; }
        public Guid OtherUserId { get; set; }
        public Guid? ListingId { get; set; }
        public DateTime DeletedAt { get; set; } = DateTime.UtcNow;
    }
}
