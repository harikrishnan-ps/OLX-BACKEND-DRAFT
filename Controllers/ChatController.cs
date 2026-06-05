using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using olx_api.Data;
using olx_api.DTOs;
using olx_api.Repositories;
using System.Security.Claims;

namespace olx_api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/chat")]
    public class ChatController : ControllerBase
    {
        private readonly IMessageRepository _messageRepo;
        private readonly ApplicationDbContext _context;

        public ChatController(IMessageRepository messageRepo, ApplicationDbContext context)
        {
            _messageRepo = messageRepo;
            _context = context;
        }

        [HttpGet("history")]
        public async Task<ActionResult<IEnumerable<MessageResponseDto>>> GetHistory([FromQuery] Guid otherUserId, [FromQuery] Guid? listingId)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            if (otherUserId == userId.Value)
                return BadRequest("Chat history requires two distinct users.");

            var messages = await _messageRepo.GetChatHistoryAsync(userId.Value, otherUserId, listingId);
            var deletedAt = await _context.ConversationDeletions
                .Where(d =>
                    d.UserId == userId.Value &&
                    d.OtherUserId == otherUserId &&
                    d.ListingId == listingId)
                .OrderByDescending(d => d.DeletedAt)
                .Select(d => (DateTime?)d.DeletedAt)
                .FirstOrDefaultAsync();

            if (deletedAt.HasValue)
                messages = messages.Where(m => m.SentAt > deletedAt.Value);

            return Ok(messages.Select(m => new MessageResponseDto(
                m.Id,
                m.Content,
                m.SentAt,
                m.IsRead,
                m.SenderId,
                m.Sender.FullName,
                m.ReceiverId
            )));
        }

        [HttpPatch("messages/{id:guid}/read")]
        public async Task<IActionResult> MarkRead(Guid id)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var message = await _context.Messages.FirstOrDefaultAsync(m => m.Id == id);
            if (message == null)
                return NotFound();

            if (message.ReceiverId != userId.Value)
                return Forbid();

            message.IsRead = true;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("conversations")]
        public async Task<IActionResult> DeleteConversation(DeleteConversationDto dto)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            if (dto.OtherUserId == userId.Value)
                return BadRequest("Conversation requires two distinct users.");

            await _context.ConversationDeletions.AddAsync(new Models.ConversationDeletion
            {
                UserId = userId.Value,
                OtherUserId = dto.OtherUserId,
                ListingId = dto.ListingId,
                DeletedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpGet("conversations")]
        public async Task<ActionResult<object>> GetConversations()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var messages = await _context.Messages
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .Include(m => m.Listing)
                .Where(m => m.SenderId == userId.Value || m.ReceiverId == userId.Value)
                .OrderByDescending(m => m.SentAt)
                .ToListAsync();

            var grouped = messages
                .GroupBy(m => {
                    var otherUserId = m.SenderId == userId.Value ? m.ReceiverId : m.SenderId;
                    return (otherUserId, m.ListingId);
                })
                .Select(g => {
                    var lastMessage = g.First();
                    var otherUser = lastMessage.SenderId == userId.Value ? lastMessage.Receiver : lastMessage.Sender;
                    var listing = lastMessage.Listing;

                    return new
                    {
                        OtherUserId = otherUser.Id,
                        OtherUserName = otherUser.FullName,
                        OtherUserProfilePicture = otherUser.ProfilePictureUrl,
                        ListingId = listing?.Id,
                        ListingTitle = listing?.Title,
                        LastMessage = lastMessage.Content,
                        LastMessageSentAt = lastMessage.SentAt,
                        LastMessageSenderId = lastMessage.SenderId,
                        IsRead = lastMessage.IsRead,
                        UnreadCount = g.Count(m => m.ReceiverId == userId.Value && !m.IsRead)
                    };
                })
                .ToList();

            var deletions = await _context.ConversationDeletions
                .Where(d => d.UserId == userId.Value)
                .ToListAsync();

            var activeConversations = grouped.Where(c => {
                var deletion = deletions.FirstOrDefault(d => d.OtherUserId == c.OtherUserId && d.ListingId == c.ListingId);
                return deletion == null || c.LastMessageSentAt > deletion.DeletedAt;
            }).ToList();

            return Ok(activeConversations);
        }

        private Guid? GetCurrentUserId()
        {
            var value =
                User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                User.FindFirstValue("sub") ??
                User.FindFirstValue("nameid");

            return Guid.TryParse(value, out var userId) ? userId : null;
        }
    }
}
