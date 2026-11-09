using ExpertLink.Data;
using ExpertLink.Hubs;
using ExpertLink.Models;
using Microsoft.AspNetCore.SignalR;

namespace ExpertLink.Services;

// Saves an in-app notification and pushes it in real time through SignalR.
public class NotificationService(AppDbContext db, IHubContext<AppHub> hub)
{
    public async Task NotifyAsync(int userId, string message, string? url = null)
    {
        db.Notifications.Add(new Notification { UserId = userId, Message = message, Url = url, CreatedAt = DateTime.Now });
        await db.SaveChangesAsync();
        await hub.Clients.User(userId.ToString()).SendAsync("notify", new { message, url });
    }

    public async Task NotifyAdminsAsync(string message, string? url = null)
    {
        var ids = db.Admins.Where(a => a.IsActive).Select(a => a.Id).ToList();
        foreach (var id in ids)
            db.Notifications.Add(new Notification { UserId = id, Message = message, Url = url, CreatedAt = DateTime.Now });
        await db.SaveChangesAsync();
        foreach (var id in ids)
            await hub.Clients.User(id.ToString()).SendAsync("notify", new { message, url });
    }
}
