using Microsoft.AspNetCore.SignalR;
using olx_api.Data;
using olx_api.DTOs;
using olx_api.Models;
using System.Security.Claims;

namespace olx_api.Hubs
{
    public class ChatHub : Hub
    {
        private readonly ApplicationDbContext _context;

        public ChatHub(ApplicationDbContext context)
        {
            _context = context;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = GetCurrentUserId();
            if (userId != null)
                await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId.Value));

            await base.OnConnectedAsync();
        }

        public async Task SendMessage(SendMessageDto dto)
        {
            var senderId = GetCurrentUserId();
            if (senderId == null)
                throw new HubException("Unauthorized.");

            var message = new Message
            {
                SenderId = senderId.Value,
                ReceiverId = dto.ReceiverId,
                ListingId = dto.ListingId,
                Content = dto.Content.Trim(),
                SentAt = DateTime.UtcNow
            };

            await _context.Messages.AddAsync(message);
            await _context.SaveChangesAsync();

            var payload = new MessageResponseDto(
                message.Id,
                message.Content,
                message.SentAt,
                message.IsRead,
                message.SenderId,
                Context.User?.Identity?.Name ?? string.Empty,
                message.ReceiverId
            );

            await Clients.Groups(UserGroup(senderId.Value), UserGroup(dto.ReceiverId))
                .SendAsync("messageReceived", payload);
        }

        private Guid? GetCurrentUserId()
        {
            var value =
                Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ??
                Context.User?.FindFirstValue("sub") ??
                Context.User?.FindFirstValue("nameid");

            return Guid.TryParse(value, out var userId) ? userId : null;
        }

        private static string UserGroup(Guid userId) => $"user:{userId}";
    }
}
