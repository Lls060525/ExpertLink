using System.Net;
using ExpertLink.Data;
using ExpertLink.Models;
using ExpertLink.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpertLink.Controllers;

// APPOINTMENT MAINTENANCE: listing/history, details (QR e-ticket, chat),
// consultant processing (confirm / reject / check-in / complete / no-show),
// client review.
[Authorize]
public class AppointmentController(AppDbContext db, SlotService slots, NotificationService ns, EmailService email) : Controller
{
    private int Me => User.GetUserId();

    // Appointments the current user is allowed to see
    private IQueryable<Appointment> Visible()
    {
        var q = db.Appointments.Include(a => a.Client).Include(a => a.Consultant).Include(a => a.Service).Include(a => a.Payment).AsQueryable();
        if (User.IsInRole("Admin")) return q;
        if (User.IsInRole("Consultant")) return q.Where(a => a.ConsultantId == Me);
        return q.Where(a => a.ClientId == Me);
    }

    // =======================================================================
    // List (tabs: upcoming / pending / past / cancelled) with paging
    // =======================================================================
    public async Task<IActionResult> Index(string tab = "upcoming", string? status = null, string? search = null, int page = 1)
    {
        await slots.ExpireUnpaidAsync();
        if (User.IsInRole("Admin")) return RedirectToAction("Appointments", "Admin");

        var today = DateOnly.FromDateTime(DateTime.Now);
        var q = Visible();
        if (status == "Pending") tab = "pending";

        // Consultants only see bookings that have been paid
        if (User.IsInRole("Consultant")) q = q.Where(a => a.Payment != null);

        q = tab switch
        {
            "pending" => q.Where(a => a.Status == AppointmentStatus.Pending && a.Date >= today),
            "past" => q.Where(a => a.Status == AppointmentStatus.Completed || a.Status == AppointmentStatus.NoShow ||
                                   (a.Date < today && (a.Status == AppointmentStatus.Confirmed || a.Status == AppointmentStatus.CheckedIn))),
            "cancelled" => q.Where(a => a.Status == AppointmentStatus.Cancelled || a.Status == AppointmentStatus.Rejected),
            _ => q.Where(a => a.Date >= today && (a.Status == AppointmentStatus.Confirmed || a.Status == AppointmentStatus.CheckedIn ||
                                                  a.Status == AppointmentStatus.Pending))
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(a => a.Client.Name.Contains(s) || a.Consultant.Name.Contains(s) || a.Service.Name.Contains(s));
        }

        q = tab is "past" or "cancelled"
            ? q.OrderByDescending(a => a.Date).ThenByDescending(a => a.StartTime)
            : q.OrderBy(a => a.Date).ThenBy(a => a.StartTime);

        ViewBag.Tab = tab;
        ViewBag.Search = search;
        var mine = User.IsInRole("Consultant") ? db.Appointments.Where(a => a.ConsultantId == Me && a.Payment != null) : db.Appointments.Where(a => a.ClientId == Me);
        ViewBag.PendingCount = await mine.CountAsync(a => a.Status == AppointmentStatus.Pending && a.Date >= today);

        return View(await PagedList<Appointment>.CreateAsync(q, page, 8));
    }

    // =======================================================================
    // Details (QR e-ticket, chat, actions)
    // =======================================================================
    public async Task<IActionResult> Details(int id)
    {
        var a = await Visible().Include(x => x.Review).Include(x => x.Consultant.Category).FirstOrDefaultAsync(x => x.Id == id);
        if (a == null) return NotFound();

        ViewBag.Messages = await db.ChatMessages.Include(m => m.Sender)
            .Where(m => m.AppointmentId == id).OrderBy(m => m.SentAt).ToListAsync();
        ViewBag.CanChat = !User.IsInRole("Admin") && a.Status is not (AppointmentStatus.Cancelled or AppointmentStatus.Rejected) && a.Payment != null;
        return View(a);
    }

    // =======================================================================
    // Consultant actions
    // =======================================================================
    private async Task<Appointment?> FindForConsultant(int id) =>
        User.IsInRole("Consultant")
            ? await db.Appointments.Include(a => a.Client).Include(a => a.Service).Include(a => a.Payment).Include(a => a.Consultant)
                .FirstOrDefaultAsync(a => a.Id == id && a.ConsultantId == Me)
            : null;

    [HttpPost, Authorize(Roles = "Consultant"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(ConfirmVM vm)
    {
        var a = await FindForConsultant(vm.AppointmentId);
        if (a == null) return NotFound();
        if (a.Status != AppointmentStatus.Pending || a.Payment == null)
        {
            TempData["Error"] = "Only paid, pending appointments can be confirmed.";
            return RedirectToAction("Details", new { id = a.Id });
        }
        if (a.Mode == ConsultationMode.Online && string.IsNullOrWhiteSpace(vm.MeetingLink))
            ModelState.AddModelError("MeetingLink", "Please provide a meeting link for online sessions.");
        if (!ModelState.IsValid)
        {
            TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            return RedirectToAction("Details", new { id = a.Id });
        }

        a.Status = AppointmentStatus.Confirmed;
        a.MeetingLink = string.IsNullOrWhiteSpace(vm.MeetingLink) ? null : vm.MeetingLink.Trim();
        await db.SaveChangesAsync();

        await ns.NotifyAsync(a.ClientId, $"{a.Consultant.Name} confirmed your appointment {a.Reference} on {a.Date:d MMM} at {a.StartTime.Time12()}.", $"/Appointment/Details/{a.Id}");
        var link = Url.Action("Details", "Appointment", new { id = a.Id }, Request.Scheme)!;
        email.Send(a.Client.Email, $"Appointment confirmed - {a.Reference}", EmailService.Template("Your appointment is confirmed",
            $"<p>Hi {WebUtility.HtmlEncode(a.Client.Name)},</p><p><b>{WebUtility.HtmlEncode(a.Consultant.Name)}</b> has confirmed your <b>{WebUtility.HtmlEncode(a.Service.Name)}</b> session on <b>{a.Date:dddd, d MMM yyyy}</b> at <b>{a.StartTime.Time12()}</b> ({a.Mode.Text()}).</p>" +
            (a.MeetingLink != null ? $"<p>Meeting link: <a href='{WebUtility.HtmlEncode(a.MeetingLink)}'>{WebUtility.HtmlEncode(a.MeetingLink)}</a></p>" : "<p>Please show your QR e-ticket at the reception when you arrive.</p>"),
            "View Appointment", link));

        TempData["Info"] = $"{a.Reference} confirmed.";
        return RedirectToAction("Details", new { id = a.Id });
    }

    [HttpPost, Authorize(Roles = "Consultant"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(ReasonVM vm)
    {
        var a = await FindForConsultant(vm.AppointmentId);
        if (a == null) return NotFound();
        if (a.Status is not (AppointmentStatus.Pending or AppointmentStatus.Confirmed) || !ModelState.IsValid)
        {
            TempData["Error"] = !ModelState.IsValid ? "Please state a reason." : "This appointment can no longer be rejected.";
            return RedirectToAction("Details", new { id = a.Id });
        }

        a.Status = AppointmentStatus.Rejected;
        a.CancelReason = vm.Reason.Trim();
        if (a.Payment != null)
        {
            a.Payment.Status = PaymentStatus.Refunded;   // consultant-initiated -> always full refund
            a.Payment.RefundedAt = DateTime.Now;
        }
        await db.SaveChangesAsync();

        await ns.NotifyAsync(a.ClientId, $"Sorry, {a.Consultant.Name} is unable to take {a.Reference}. A full refund has been issued.", $"/Appointment/Details/{a.Id}");
        email.Send(a.Client.Email, $"Appointment not accepted - {a.Reference}", EmailService.Template("Appointment not accepted",
            $"<p>Hi {WebUtility.HtmlEncode(a.Client.Name)},</p><p>Unfortunately {WebUtility.HtmlEncode(a.Consultant.Name)} is unable to take your session on {a.Date:d MMM yyyy} at {a.StartTime.Time12()}.</p><p>Reason: <i>{WebUtility.HtmlEncode(a.CancelReason)}</i></p><p>A full refund of <b>{Helper.Money(a.Fee)}</b> has been issued.</p>"));

        TempData["Info"] = $"{a.Reference} rejected and refunded.";
        return RedirectToAction("Details", new { id = a.Id });
    }

    [HttpPost, Authorize(Roles = "Consultant"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(CompleteVM vm)
    {
        var a = await FindForConsultant(vm.AppointmentId);
        if (a == null) return NotFound();
        if (a.Status is not (AppointmentStatus.Confirmed or AppointmentStatus.CheckedIn) || a.StartDateTime > DateTime.Now)
        {
            TempData["Error"] = "Only confirmed sessions that have started can be marked as completed.";
            return RedirectToAction("Details", new { id = a.Id });
        }
        if (!ModelState.IsValid)
        {
            TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            return RedirectToAction("Details", new { id = a.Id });
        }

        a.Status = AppointmentStatus.Completed;
        a.SessionNotes = vm.SessionNotes.Trim();
        a.CheckedInAt ??= DateTime.Now;
        await db.SaveChangesAsync();

        await ns.NotifyAsync(a.ClientId, $"Your session {a.Reference} is completed. How was it? Leave a review for {a.Consultant.Name}!", $"/Appointment/Details/{a.Id}#review");
        TempData["Info"] = $"{a.Reference} marked as completed.";
        return RedirectToAction("Details", new { id = a.Id });
    }

    [HttpPost, Authorize(Roles = "Consultant"), ValidateAntiForgeryToken]
    public async Task<IActionResult> NoShow(int id)
    {
        var a = await FindForConsultant(id);
        if (a == null) return NotFound();
        if (a.Status != AppointmentStatus.Confirmed || a.StartDateTime.AddMinutes(15) > DateTime.Now)
        {
            TempData["Error"] = "You can only mark a confirmed appointment as no-show 15 minutes after its start time.";
            return RedirectToAction("Details", new { id = a.Id });
        }
        a.Status = AppointmentStatus.NoShow;
        await db.SaveChangesAsync();
        await ns.NotifyAsync(a.ClientId, $"You were marked as a no-show for {a.Reference}.", $"/Appointment/Details/{a.Id}");
        TempData["Info"] = $"{a.Reference} marked as no-show.";
        return RedirectToAction("Details", new { id = a.Id });
    }

    // =======================================================================
    // QR code check-in (consultant scans the client's e-ticket with webcam)
    // =======================================================================
    [Authorize(Roles = "Consultant")]
    public IActionResult Scanner() => View();

    [HttpPost, Authorize(Roles = "Consultant"), ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckIn(string code)
    {
        // QR content format: "EXL:{appointmentId}:{checkInCode}"
        var parts = (code ?? "").Trim().Split(':');
        if (parts.Length != 3 || parts[0] != "EXL" || !int.TryParse(parts[1], out var id))
            return Json(new { ok = false, message = "This is not a valid ExpertLink e-ticket." });

        var a = await db.Appointments.Include(x => x.Client).Include(x => x.Service)
            .FirstOrDefaultAsync(x => x.Id == id && x.CheckInCode == parts[2]);
        if (a == null)
            return Json(new { ok = false, message = "E-ticket not found or no longer valid." });
        if (a.ConsultantId != Me)
            return Json(new { ok = false, message = "This appointment belongs to another consultant." });
        if (a.Status == AppointmentStatus.CheckedIn)
            return Json(new { ok = false, message = $"{a.Client.Name} has already checked in at {a.CheckedInAt:h:mm tt}." });
        if (a.Status != AppointmentStatus.Confirmed)
            return Json(new { ok = false, message = $"Appointment status is {a.Status.Text()} - cannot check in." });
        if (a.Date != DateOnly.FromDateTime(DateTime.Now))
            return Json(new { ok = false, message = $"This appointment is on {a.Date:d MMM yyyy}, not today." });

        a.Status = AppointmentStatus.CheckedIn;
        a.CheckedInAt = DateTime.Now;
        await db.SaveChangesAsync();
        await ns.NotifyAsync(a.ClientId, $"You have checked in for {a.Reference}. Enjoy your session!", $"/Appointment/Details/{a.Id}");

        return Json(new
        {
            ok = true,
            message = $"Checked in: {a.Client.Name}",
            detail = $"{a.Reference} · {a.Service.Name} · {a.StartTime.Time12()} - {a.EndTime.Time12()}",
            url = Url.Action("Details", new { id = a.Id })
        });
    }

    // =======================================================================
    // Review (client, after completed session)
    // =======================================================================
    [HttpPost, Authorize(Roles = "Client"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(ReviewVM vm)
    {
        var a = await db.Appointments.Include(x => x.Review).Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == vm.AppointmentId && x.ClientId == Me);
        if (a == null) return NotFound();
        if (a.Status != AppointmentStatus.Completed || a.Review != null)
        {
            TempData["Error"] = "You can only review a completed session once.";
            return RedirectToAction("Details", new { id = a.Id });
        }
        if (!ModelState.IsValid)
        {
            TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            return RedirectToAction("Details", new { id = a.Id });
        }

        a.Review = new Review { Rating = vm.Rating!.Value, Comment = vm.Comment?.Trim(), CreatedAt = DateTime.Now };
        await db.SaveChangesAsync();
        await ns.NotifyAsync(a.ConsultantId, $"{a.Client.Name} left a {vm.Rating}-star review.", "/Consultant/Reviews");

        TempData["Info"] = "Thank you for your review!";
        return RedirectToAction("Details", new { id = a.Id });
    }
}
