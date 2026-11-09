using ExpertLink.Data;
using ExpertLink.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpertLink.Services;

// Calculates bookable time slots from the consultant's weekly availability,
// time-off dates and existing appointments.
public class SlotService(AppDbContext db)
{
    public const int SlotStepMinutes = 30;
    public const int MinLeadHours = 2;          // cannot book less than 2 hours ahead
    public const int MaxDaysAhead = 60;
    public const int UnpaidExpiryMinutes = 30;

    // Statuses that occupy a time slot
    // (a List, not an array, so that .Contains() in EF queries binds to List<T>.Contains)
    public static readonly List<AppointmentStatus> Blocking =
        [AppointmentStatus.Pending, AppointmentStatus.Confirmed, AppointmentStatus.CheckedIn, AppointmentStatus.Completed];

    // Auto-cancel bookings that were never paid (releases the slot)
    public async Task ExpireUnpaidAsync()
    {
        var cutoff = DateTime.Now.AddMinutes(-UnpaidExpiryMinutes);
        var list = await db.Appointments
            .Where(a => a.Status == AppointmentStatus.Pending && a.Payment == null && a.CreatedAt < cutoff)
            .ToListAsync();
        foreach (var a in list)
        {
            a.Status = AppointmentStatus.Cancelled;
            a.CancelReason = $"Auto-cancelled: payment not completed within {UnpaidExpiryMinutes} minutes.";
        }
        if (list.Count > 0) await db.SaveChangesAsync();
    }

    public async Task<List<SlotVM>> GetSlotsAsync(Service service, DateOnly date, int clientId, int excludeAppointmentId = 0)
    {
        var result = new List<SlotVM>();
        var today = DateOnly.FromDateTime(DateTime.Now);
        if (date < today || date > today.AddDays(MaxDaysAhead)) return result;

        bool off = await db.TimeOffs.AnyAsync(t => t.ConsultantId == service.ConsultantId && t.Date == date);
        if (off) return result;

        var day = date.DayOfWeek;
        var windows = await db.Availabilities
            .Where(a => a.ConsultantId == service.ConsultantId && a.Day == day)
            .OrderBy(a => a.StartTime)
            .ToListAsync();

        // Appointments that block: the consultant's, plus the client's own (no double booking)
        var busy = await db.Appointments
            .Where(a => a.Date == date && Blocking.Contains(a.Status) && a.Id != excludeAppointmentId &&
                        (a.ConsultantId == service.ConsultantId || a.ClientId == clientId))
            .Select(a => new { a.StartTime, a.EndTime })
            .ToListAsync();

        var earliest = DateTime.Now.AddHours(MinLeadHours);

        foreach (var w in windows)
        {
            var start = w.StartTime;
            while (true)
            {
                var end = start.AddMinutes(service.DurationMinutes);
                if (end <= start || end > w.EndTime) break;   // (end <= start) guards midnight wrap

                bool taken = busy.Any(b => start < b.EndTime && end > b.StartTime);
                bool tooSoon = date.ToDateTime(start) < earliest;

                result.Add(new SlotVM
                {
                    Start = start.ToString("HH:mm"),
                    End = end.ToString("HH:mm"),
                    Label = start.ToString("h:mm tt"),
                    Available = !taken && !tooSoon
                });

                var next = start.AddMinutes(SlotStepMinutes);
                if (next <= start) break;
                start = next;
            }
        }
        return result;
    }

    public async Task<bool> IsSlotAvailableAsync(Service service, DateOnly date, TimeOnly start, int clientId, int excludeAppointmentId = 0)
    {
        var slots = await GetSlotsAsync(service, date, clientId, excludeAppointmentId);
        return slots.Any(s => s.Start == start.ToString("HH:mm") && s.Available);
    }
}
