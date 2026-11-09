using System.Net;
using ExpertLink.Data;
using ExpertLink.Models;
using ExpertLink.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpertLink.Controllers;

// BOOKING: interactive slot selection, booking, payment (simulated gateway),
// e-receipt, reschedule and cancellation (by client).
[Authorize(Roles = "Client")]
public class BookingController(AppDbContext db, SlotService slots, NotificationService ns, EmailService email) : Controller
{
    private int Me => User.GetUserId();

    public const int FreeCancelHours = 24;      // full refund if cancelled >= 24h before
    public const int MaxReschedules = 2;

    private Task<Service?> FindService(int serviceId) =>
        db.Services.Include(s => s.Consultant).ThenInclude(c => c.Category)
          .FirstOrDefaultAsync(s => s.Id == serviceId && s.IsActive && s.Consultant.IsApproved && s.Consultant.IsActive);

    // =======================================================================
    // AJAX: available slots for a service on a date  (also used by reschedule)
    // =======================================================================
    [AllowAnonymous]
    public async Task<IActionResult> Slots(int serviceId, DateOnly date, int exclude = 0)
    {
        await slots.ExpireUnpaidAsync();
        var s = await FindService(serviceId);
        if (s == null) return NotFound();
        var list = await slots.GetSlotsAsync(s, date, Me, exclude);
        bool off = await db.TimeOffs.AnyAsync(t => t.ConsultantId == s.ConsultantId && t.Date == date);
        return Json(new { date = date.ToString("dddd, d MMMM yyyy"), off, slots = list });
    }

    // AJAX: which days in a month have free slots (for calendar highlighting)
    [AllowAnonymous]
    public async Task<IActionResult> Month(int serviceId, int year, int month, int exclude = 0)
    {
        var s = await FindService(serviceId);
        if (s == null || month < 1 || month > 12 || year < 2000 || year > 2100) return NotFound();
        var result = new Dictionary<string, int>();
        var first = new DateOnly(year, month, 1);
        for (var d = first; d.Month == month; d = d.AddDays(1))
        {
            var list = await slots.GetSlotsAsync(s, d, Me, exclude);
            result[d.ToString("yyyy-MM-dd")] = list.Count(x => x.Available);
        }
        return Json(result);
    }

    // =======================================================================
    // Book
    // =======================================================================
    public async Task<IActionResult> Book(int id)   // id = serviceId
    {
        var s = await FindService(id);
        if (s == null) return NotFound();
        var vm = new BookVM { ServiceId = s.Id, Service = s };
        if (s.AllowOnline && !s.AllowInPerson) vm.Mode = ConsultationMode.Online;
        if (!s.AllowOnline && s.AllowInPerson) vm.Mode = ConsultationMode.InPerson;
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Book(BookVM vm)
    {
        await slots.ExpireUnpaidAsync();
        var s = await FindService(vm.ServiceId);
        if (s == null) return NotFound();
        vm.Service = s;

        if (vm.Mode == ConsultationMode.Online && !s.AllowOnline)
            ModelState.AddModelError("Mode", "This service is not available online.");
        if (vm.Mode == ConsultationMode.InPerson && !s.AllowInPerson)
            ModelState.AddModelError("Mode", "This service is not available in person.");
        if (ModelState.IsValid && !await slots.IsSlotAvailableAsync(s, vm.Date!.Value, vm.StartTime!.Value, Me))
            ModelState.AddModelError("StartTime", "Sorry, this time slot is no longer available. Please choose another slot.");

        if (!ModelState.IsValid) return View(vm);

        var a = new Appointment
        {
            ClientId = Me,
            ConsultantId = s.ConsultantId,
            ServiceId = s.Id,
            Date = vm.Date!.Value,
            StartTime = vm.StartTime!.Value,
            EndTime = vm.StartTime!.Value.AddMinutes(s.DurationMinutes),
            Mode = vm.Mode!.Value,
            Concern = vm.Concern?.Trim(),
            Fee = s.Fee,
            Status = AppointmentStatus.Pending,
            CreatedAt = DateTime.Now,
        };
        db.Appointments.Add(a);
        await db.SaveChangesAsync();

        TempData["Info"] = $"Slot reserved! Please complete payment within {SlotService.UnpaidExpiryMinutes} minutes to secure your booking.";
        return RedirectToAction("Pay", new { id = a.Id });
    }

    // =======================================================================
    // Payment (simulated gateway: Card with Luhn check / FPX / E-Wallet)
    // =======================================================================
    private Task<Appointment?> FindMine(int id) =>
        db.Appointments.Include(a => a.Consultant).Include(a => a.Service).Include(a => a.Payment).Include(a => a.Client)
          .FirstOrDefaultAsync(a => a.Id == id && a.ClientId == Me);

    public async Task<IActionResult> Pay(int id)
    {
        await slots.ExpireUnpaidAsync();
        var a = await FindMine(id);
        if (a == null) return NotFound();
        if (a.Payment != null) return RedirectToAction("Receipt", new { id });
        if (a.Status != AppointmentStatus.Pending)
        {
            TempData["Error"] = "This booking can no longer be paid: " + (a.CancelReason ?? a.Status.Text());
            return RedirectToAction("Details", "Appointment", new { id });
        }
        ViewBag.ExpiresAt = a.CreatedAt.AddMinutes(SlotService.UnpaidExpiryMinutes);
        return View(new PaymentVM { AppointmentId = id, Appointment = a });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(PaymentVM vm)
    {
        await slots.ExpireUnpaidAsync();
        var a = await FindMine(vm.AppointmentId);
        if (a == null) return NotFound();
        if (a.Payment != null) return RedirectToAction("Receipt", new { id = a.Id });
        if (a.Status != AppointmentStatus.Pending)
        {
            TempData["Error"] = "This booking has expired. Please book again.";
            return RedirectToAction("Details", "Appointment", new { id = a.Id });
        }
        if (!ModelState.IsValid)
        {
            vm.Appointment = a;
            ViewBag.ExpiresAt = a.CreatedAt.AddMinutes(SlotService.UnpaidExpiryMinutes);
            return View(vm);
        }

        var digits = (vm.CardNumber ?? "").Replace(" ", "").Replace("-", "");
        a.Payment = new Payment
        {
            Amount = a.Fee,
            Method = vm.Method,
            CardLast4 = vm.Method == "Card" && digits.Length >= 4 ? digits[^4..] : null,
            ReferenceNo = "PAY" + DateTime.Now.ToString("yyMMddHHmmss") + Random.Shared.Next(100, 999),
            PaidAt = DateTime.Now,
            Status = PaymentStatus.Paid,
        };
        await db.SaveChangesAsync();

        await ns.NotifyAsync(a.ConsultantId, $"New booking request {a.Reference} from {a.Client.Name} on {a.Date:d MMM} at {a.StartTime.Time12()}.", $"/Appointment/Details/{a.Id}");
        SendReceipt(a);

        TempData["Info"] = "Payment successful! Your booking is awaiting the consultant's confirmation.";
        return RedirectToAction("Receipt", new { id = a.Id });
    }

    // =======================================================================
    // E-receipt (printable page + e-mail)
    // =======================================================================
    public async Task<IActionResult> Receipt(int id)
    {
        var a = await FindMine(id);
        if (a == null || a.Payment == null) return NotFound();
        return View(a);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EmailReceipt(int id)
    {
        var a = await FindMine(id);
        if (a == null || a.Payment == null) return NotFound();
        bool sent = SendReceipt(a);
        TempData["Info"] = sent ? $"E-receipt sent to {a.Client.Email}." : "SMTP is not configured - the e-receipt was saved to the MailDrop folder instead.";
        return RedirectToAction("Receipt", new { id });
    }

    private bool SendReceipt(Appointment a)
    {
        var p = a.Payment!;
        var link = Url.Action("Details", "Appointment", new { id = a.Id }, Request.Scheme)!;
        var rows = new[]
        {
            ("Booking Ref.", a.Reference),
            ("Consultant", a.Consultant.Name),
            ("Service", a.Service.Name),
            ("Date & Time", $"{a.Date:dddd, d MMM yyyy}, {a.StartTime.Time12()} - {a.EndTime.Time12()}"),
            ("Mode", a.Mode.Text()),
            ("Payment Method", p.Method + (p.CardLast4 != null ? $" (**** {p.CardLast4})" : "")),
            ("Payment Ref.", p.ReferenceNo),
            ("Paid At", p.PaidAt.ToString("d MMM yyyy, h:mm tt")),
            ("Amount Paid", Helper.Money(p.Amount)),
        };
        var table = "<table style='width:100%;border-collapse:collapse'>" +
            string.Concat(rows.Select(r => $"<tr><td style='padding:6px 0;color:#64748b'>{r.Item1}</td><td style='padding:6px 0;text-align:right'><b>{WebUtility.HtmlEncode(r.Item2)}</b></td></tr>")) +
            "</table>";
        var body = EmailService.Template("Payment receipt",
            $"<p>Hi {WebUtility.HtmlEncode(a.Client.Name)},</p><p>Thank you for your booking. Here is your e-receipt. Show the QR code on the appointment page when you arrive for in-person sessions.</p>{table}",
            "View Appointment", link);
        return email.Send(a.Client.Email, $"ExpertLink e-receipt {a.Reference}", body);
    }

    // =======================================================================
    // Reschedule (>= 24 hours before, max 2 times)
    // =======================================================================
    private string? CannotChange(Appointment a, string action)
    {
        if (a.Status is not (AppointmentStatus.Pending or AppointmentStatus.Confirmed))
            return $"Only pending or confirmed appointments can be {action}.";
        if (a.Payment == null) return null;
        if (a.StartDateTime < DateTime.Now.AddHours(FreeCancelHours) && action == "rescheduled")
            return $"Appointments can only be rescheduled at least {FreeCancelHours} hours in advance.";
        return null;
    }

    public async Task<IActionResult> Reschedule(int id)
    {
        var a = await FindMine(id);
        if (a == null) return NotFound();
        var err = CannotChange(a, "rescheduled") ?? (a.RescheduleCount >= MaxReschedules ? $"An appointment can only be rescheduled {MaxReschedules} times." : null);
        if (err != null)
        {
            TempData["Error"] = err;
            return RedirectToAction("Details", "Appointment", new { id });
        }
        return View(new RescheduleVM { AppointmentId = id, Appointment = a });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reschedule(RescheduleVM vm)
    {
        var a = await FindMine(vm.AppointmentId);
        if (a == null) return NotFound();
        var err = CannotChange(a, "rescheduled") ?? (a.RescheduleCount >= MaxReschedules ? $"An appointment can only be rescheduled {MaxReschedules} times." : null);
        if (err != null)
        {
            TempData["Error"] = err;
            return RedirectToAction("Details", "Appointment", new { id = a.Id });
        }

        if (ModelState.IsValid && !await slots.IsSlotAvailableAsync(a.Service, vm.Date!.Value, vm.StartTime!.Value, Me, a.Id))
            ModelState.AddModelError("StartTime", "Sorry, this time slot is not available. Please choose another slot.");
        if (ModelState.IsValid && vm.Date == a.Date && vm.StartTime == a.StartTime)
            ModelState.AddModelError("StartTime", "Please choose a different date or time.");
        if (!ModelState.IsValid)
        {
            vm.Appointment = a;
            return View(vm);
        }

        var old = $"{a.Date:d MMM} {a.StartTime.Time12()}";
        a.Date = vm.Date!.Value;
        a.StartTime = vm.StartTime!.Value;
        a.EndTime = a.StartTime.AddMinutes(a.Service.DurationMinutes);
        a.RescheduleCount++;
        a.Status = AppointmentStatus.Pending;   // consultant needs to re-confirm the new time
        a.CheckInCode = Guid.NewGuid().ToString("N");
        await db.SaveChangesAsync();

        await ns.NotifyAsync(a.ConsultantId, $"{a.Client.Name} rescheduled {a.Reference} from {old} to {a.Date:d MMM} {a.StartTime.Time12()}. Please confirm.", $"/Appointment/Details/{a.Id}");
        TempData["Info"] = "Appointment rescheduled. The consultant will confirm the new time.";
        return RedirectToAction("Details", "Appointment", new { id = a.Id });
    }

    // =======================================================================
    // Cancel (full refund if >= 24 hours before)
    // =======================================================================
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(ReasonVM vm)
    {
        var a = await FindMine(vm.AppointmentId);
        if (a == null) return NotFound();
        var err = CannotChange(a, "cancelled");
        if (err == null && !ModelState.IsValid) err = "Please state a reason for cancelling (max 300 characters).";
        if (err != null)
        {
            TempData["Error"] = err;
            return RedirectToAction("Details", "Appointment", new { id = a.Id });
        }

        bool refund = a.Payment != null && a.StartDateTime >= DateTime.Now.AddHours(FreeCancelHours);
        a.Status = AppointmentStatus.Cancelled;
        a.CancelReason = vm.Reason.Trim();
        if (refund)
        {
            a.Payment!.Status = PaymentStatus.Refunded;
            a.Payment.RefundedAt = DateTime.Now;
        }
        await db.SaveChangesAsync();

        if (a.Payment != null)
            await ns.NotifyAsync(a.ConsultantId, $"{a.Client.Name} cancelled {a.Reference} ({a.Date:d MMM} {a.StartTime.Time12()}).", $"/Appointment/Details/{a.Id}");

        TempData["Info"] = a.Payment == null
            ? "Booking cancelled."
            : refund ? $"Appointment cancelled. A full refund of {Helper.Money(a.Fee)} will be credited within 5-7 working days."
                     : $"Appointment cancelled. No refund is given for cancellations less than {FreeCancelHours} hours before the session.";
        return RedirectToAction("Details", "Appointment", new { id = a.Id });
    }
}
