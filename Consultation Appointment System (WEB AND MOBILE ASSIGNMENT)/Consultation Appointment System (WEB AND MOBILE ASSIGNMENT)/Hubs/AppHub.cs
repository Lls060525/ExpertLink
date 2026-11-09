using ExpertLink.Data;
using ExpertLink.Models;
using ExpertLink.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ExpertLink.Hubs;

// Real-time hub: appointment chat + push notifications.
// Users are identified by the NameIdentifier claim (user id).
[Authorize]
public class AppHub(AppDbContext db) : Hub
{
    private int UserId => Context.User!.GetUserId();

    private static string Group(int appointmentId) => "appt-" + appointmentId;

    private Task<Appointment?> FindAsync(int appointmentId) =>
        db.Appointments.FirstOrDefaultAsync(a => a.Id == appointmentId && (a.ClientId == UserId || a.ConsultantId == UserId));

    public async Task JoinAppointment(int appointmentId)
    {
        var a = await FindAsync(appointmentId) ?? throw new HubException("You are not allowed to join this chat.");
        await Groups.AddToGroupAsync(Context.ConnectionId, Group(a.Id));
    }

    public async Task SendMessage(int appointmentId, string content)
    {
        content = (content ?? "").Trim();
        if (content.Length == 0) return;
        if (content.Length > 1000) throw new HubException("Message is too long (max 1000 characters).");

        var a = await FindAsync(appointmentId) ?? throw new HubException("You are not allowed to send messages here.");
        if (a.Status is AppointmentStatus.Cancelled or AppointmentStatus.Rejected)
            throw new HubException("This chat is closed because the appointment was cancelled.");

        var msg = new ChatMessage { AppointmentId = a.Id, SenderId = UserId, Content = content, SentAt = DateTime.Now };
        db.ChatMessages.Add(msg);
        await db.SaveChangesAsync();

        var name = Context.User!.Identity?.Name ?? "User";
        await Clients.Group(Group(a.Id)).SendAsync("message", new
        {
            senderId = UserId,
            sender = name,
            photo = Context.User!.GetPhoto(),
            content,
            sentAt = msg.SentAt.ToString("d MMM, h:mm tt")
        });

        // toast for the other party (if they are not looking at the chat)
        var other = a.ClientId == UserId ? a.ConsultantId : a.ClientId;
        await Clients.User(other.ToString()).SendAsync("chat", new
        {
            appointmentId = a.Id,
            message = $"New message from {name}",
            url = $"/Appointment/Details/{a.Id}#chat"
        });
    }

    public async Task Typing(int appointmentId)
    {
        await Clients.OthersInGroup(Group(appointmentId)).SendAsync("typing", Context.User!.Identity?.Name);
    }
}
