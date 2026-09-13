using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Bitely.Hubs
{
    public class QueueHub : Hub
    {
        public async Task JoinOrderGroup(string orderId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"order-{orderId}");
        }

        public async Task JoinStallGroup(string foodStallId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"stall-{foodStallId}");
        }
    }
}
