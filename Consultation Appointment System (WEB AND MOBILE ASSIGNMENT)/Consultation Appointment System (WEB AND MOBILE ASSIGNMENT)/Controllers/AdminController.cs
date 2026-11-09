using System.Net;
using System.Text;
using ExpertLink.Data;
using ExpertLink.Models;
using ExpertLink.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpertLink.Controllers;

// ADMIN: dashboard & reports, user maintenance, consultant approval,
// appointment overview, review moderation.
[Authorize(Roles = "Admin")]
public class AdminController(AppDbContext db, Helper hp, EmailService email, NotificationService ns) : Controller
{
    // =======================================================================
    // Dashboard + charts
    // =======================================================================
    public async Task<IActionResult> Dashboard()
    {
        var now = DateTime.Now;
        var monthStart = new DateOnly(now.Year, now.Month, 1);
        var today = DateOnly.FromDateTime(now);
        var paid = db.Payments.Where(p => p.Status == PaymentStatus.Paid);

        var vm = new DashboardVM
        {
            Clients = await db.Clients.CountAsync(),
            Consultants = await db.Consultants.CountAsync(c => c.IsApproved),
            PendingApprovals = await db.Consultants.CountAsync(c => !c.IsApproved && c.IsActive),
            AppointmentsThisMonth = await db.Appointments.CountAsync(a => a.Date >= monthStart && a.Date < monthStart.AddMonths(1)),
            UpcomingAppointments = await db.Appointments.CountAsync(a => a.Date >= today &&
                (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed)),
            RevenueThisMonth = await paid.Where(p => p.PaidAt >= monthStart.ToDateTime(TimeOnly.MinValue)).SumAsync(p => (decimal?)p.Amount) ?? 0,
            TotalRevenue = await paid.SumAsync(p => (decimal?)p.Amount) ?? 0,
            AverageRating = await db.Reviews.AverageAsync(r => (double?)r.Rating) ?? 0,
            Recent = await db.Appointments.Include(a => a.Client).Include(a => a.Consultant).Include(a => a.Service)
                .OrderByDescending(a => a.CreatedAt).Take(8).ToListAsync(),
            AwaitingApproval = await db.Consultants.Include(c => c.Category)
                .Where(c => !c.IsApproved && c.IsActive).OrderBy(c => c.CreatedAt).ToListAsync(),
        };
        return View(vm);
    }

    // AJAX: data for Chart.js
    public async Task<IActionResult> ChartData(int months = 6)
    {
        months = Math.Clamp(months, 3, 12);
        var now = DateTime.Now;
        var from = new DateOnly(now.Year, now.Month, 1).AddMonths(-(months - 1));

        var appts = await db.Appointments
            .Where(a => a.Date >= from)
            .Select(a => new { a.Date, a.Status, a.Fee, Category = a.Consultant.Category!.Name, Paid = a.Payment != null && a.Payment.Status == PaymentStatus.Paid })
            .ToListAsync();

        var labels = new List<string>();
        var bookings = new List<int>();
        var revenue = new List<decimal>();
        for (int i = 0; i < months; i++)
        {
            var m = from.AddMonths(i);
            labels.Add(m.ToString("MMM yyyy"));
            var inMonth = appts.Where(a => a.Date.Year == m.Year && a.Date.Month == m.Month).ToList();
            bookings.Add(inMonth.Count);
            revenue.Add(inMonth.Where(a => a.Paid).Sum(a => a.Fee));
        }

        var status = appts.GroupBy(a => a.Status)
            .Select(g => new { label = g.Key.Text(), count = g.Count() })
            .OrderByDescending(x => x.count).ToList();

        var byCategory = appts.Where(a => a.Paid).GroupBy(a => a.Category ?? "Uncategorised")
            .Select(g => new { label = g.Key, revenue = g.Sum(a => a.Fee) })
            .OrderByDescending(x => x.revenue).ToList();

        var topConsultants = await db.Consultants
            .Where(c => c.IsApproved)
            .Select(c => new
            {
                c.Name,
                Sessions = c.Appointments.Count(a => a.Status == AppointmentStatus.Completed && a.Date >= from),
                Rating = c.Appointments.Where(a => a.Review != null).Average(a => (double?)a.Review!.Rating) ?? 0
            })
            .OrderByDescending(c => c.Sessions).Take(5).ToListAsync();

        return Json(new { labels, bookings, revenue, status, byCategory, topConsultants });
    }

    // Export appointments report as CSV (opens in Excel)
    public async Task<IActionResult> ExportAppointments(DateOnly? from, DateOnly? to)
    {
        var q = db.Appointments.Include(a => a.Client).Include(a => a.Consultant).Include(a => a.Service).Include(a => a.Payment).AsQueryable();
        if (from != null) q = q.Where(a => a.Date >= from);
        if (to != null) q = q.Where(a => a.Date <= to);
        var list = await q.OrderBy(a => a.Date).ThenBy(a => a.StartTime).ToListAsync();

        static string Csv(string? s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
        var sb = new StringBuilder();
        sb.AppendLine("Reference,Date,Start,End,Client,Consultant,Service,Mode,Status,Fee (RM),Payment,Payment Status");
        foreach (var a in list)
        {
            sb.AppendLine(string.Join(",",
                a.Reference, a.Date.ToString("yyyy-MM-dd"), a.StartTime.ToString("HH:mm"), a.EndTime.ToString("HH:mm"),
                Csv(a.Client.Name), Csv(a.Consultant.Name), Csv(a.Service.Name), a.Mode.Text(), a.Status.Text(),
                a.Fee.ToString("0.00"), a.Payment?.Method ?? "-", a.Payment?.Status.ToString() ?? "Unpaid"));
        }
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"appointments_{DateTime.Now:yyyyMMdd}.csv");
    }

    // =======================================================================
    // User maintenance (AJAX search / filter / sort / paging)
    // =======================================================================
    public async Task<IActionResult> Users(string? search, string? role, string? status, string sort = "newest", int page = 1)
    {
        IQueryable<User> q = db.Users;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(u => u.Name.Contains(s) || u.Email.Contains(s) || (u.Phone != null && u.Phone.Contains(s)));
        }
        q = role switch
        {
            "Admin" => q.Where(u => u is Admin),
            "Consultant" => q.Where(u => u is Consultant),
            "Client" => q.Where(u => u is Client),
            _ => q
        };
        q = status switch
        {
            "active" => q.Where(u => u.IsActive),
            "inactive" => q.Where(u => !u.IsActive),
            "pending" => q.Where(u => u is Consultant && !((Consultant)u).IsApproved),
            "unverified" => q.Where(u => !u.EmailVerified),
            "locked" => q.Where(u => u.LockoutEnd != null && u.LockoutEnd > DateTime.Now),
            _ => q
        };
        q = sort switch
        {
            "name" => q.OrderBy(u => u.Name),
            "name_desc" => q.OrderByDescending(u => u.Name),
            "email" => q.OrderBy(u => u.Email),
            "oldest" => q.OrderBy(u => u.CreatedAt),
            _ => q.OrderByDescending(u => u.CreatedAt)
        };

        ViewBag.Search = search;
        ViewBag.Role = role;
        ViewBag.Status = status;
        ViewBag.Sort = sort;

        var result = await PagedList<User>.CreateAsync(q, page, 10);
        if (Request.Headers.XRequestedWith == "XMLHttpRequest")
            return PartialView("_UserTable", result);
        return View(result);
    }

    public async Task<IActionResult> UserDetail(int id)
    {
        var u = await db.Users.FindAsync(id);
        if (u == null) return NotFound();

        if (u is Consultant c)
        {
            await db.Entry(c).Reference(x => x.Category).LoadAsync();
            await db.Entry(c).Collection(x => x.Services).LoadAsync();
            await db.Entry(c).Collection(x => x.Certificates).LoadAsync();
            ViewBag.Appointments = await db.Appointments.Include(a => a.Client).Include(a => a.Service)
                .Where(a => a.ConsultantId == id).OrderByDescending(a => a.Date).Take(10).ToListAsync();
        }
        else if (u is Client)
        {
            ViewBag.Appointments = await db.Appointments.Include(a => a.Consultant).Include(a => a.Service)
                .Where(a => a.ClientId == id).OrderByDescending(a => a.Date).Take(10).ToListAsync();
        }
        return View(u);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var u = await db.Users.FindAsync(id);
        if (u == null) return NotFound();
        if (u.Id == User.GetUserId())
        {
            TempData["Error"] = "You cannot deactivate your own account.";
            return RedirectToAction("UserDetail", new { id });
        }
        u.IsActive = !u.IsActive;
        await db.SaveChangesAsync();
        TempData["Info"] = $"{u.Name} has been {(u.IsActive ? "activated" : "deactivated")}.";
        return RedirectToAction("UserDetail", new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Unlock(int id)
    {
        var u = await db.Users.FindAsync(id);
        if (u == null) return NotFound();
        u.LockoutEnd = null;
        u.FailedLoginCount = 0;
        await db.SaveChangesAsync();
        TempData["Info"] = $"{u.Name}'s account has been unlocked.";
        return RedirectToAction("UserDetail", new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, bool approve)
    {
        var c = await db.Consultants.FindAsync(id);
        if (c == null) return NotFound();

        if (approve)
        {
            c.IsApproved = true;
            await db.SaveChangesAsync();
            await ns.NotifyAsync(c.Id, "Congratulations! Your consultant profile has been approved and is now listed.", "/Consultant/Dashboard");
            email.Send(c.Email, "Your ExpertLink consultant application is approved",
                EmailService.Template("Application approved", $"<p>Hi {WebUtility.HtmlEncode(c.Name)},</p><p>Your consultant profile is now visible to clients. Remember to set up your services and weekly availability so clients can book you.</p>"));
            TempData["Info"] = $"{c.Name} has been approved.";
        }
        else
        {
            c.IsApproved = false;
            c.IsActive = false;
            await db.SaveChangesAsync();
            email.Send(c.Email, "Your ExpertLink consultant application",
                EmailService.Template("Application not approved", $"<p>Hi {WebUtility.HtmlEncode(c.Name)},</p><p>We are unable to approve your consultant application at this time. Please contact us for more information.</p>"));
            TempData["Info"] = $"{c.Name}'s application has been rejected.";
        }
        return RedirectToAction("UserDetail", new { id });
    }

    public IActionResult CreateUser() => View(new CreateUserVM());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(CreateUserVM vm)
    {
        var mail = (vm.Email ?? "").Trim().ToLower();
        if (await db.Users.AnyAsync(u => u.Email == mail))
            ModelState.AddModelError("Email", "This email has already been registered.");
        if (!ModelState.IsValid) return View(vm);

        User u = vm.Role == "Admin" ? new Admin() : new Client();
        u.Name = vm.Name.Trim();
        u.Email = mail;
        u.Phone = vm.Phone;
        u.Hash = hp.HashPassword(vm.Password);
        u.EmailVerified = true;
        db.Users.Add(u);
        await db.SaveChangesAsync();

        email.Send(u.Email, "Your ExpertLink account",
            EmailService.Template("An account has been created for you",
                $"<p>Hi {WebUtility.HtmlEncode(u.Name)},</p><p>An administrator created a <b>{u.Role}</b> account for you. Log in with this email and the temporary password given to you, then change your password.</p>"));

        TempData["Info"] = $"{u.Role} account for {u.Name} created.";
        return RedirectToAction("Users");
    }

    // =======================================================================
    // Appointments overview
    // =======================================================================
    public async Task<IActionResult> Appointments(string? search, AppointmentStatus? status, DateOnly? from, DateOnly? to, int page = 1)
    {
        var q = db.Appointments.Include(a => a.Client).Include(a => a.Consultant).Include(a => a.Service).Include(a => a.Payment).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(a => a.Client.Name.Contains(s) || a.Consultant.Name.Contains(s) || a.Service.Name.Contains(s));
        }
        if (status != null) q = q.Where(a => a.Status == status);
        if (from != null) q = q.Where(a => a.Date >= from);
        if (to != null) q = q.Where(a => a.Date <= to);

        ViewBag.Search = search;
        ViewBag.StatusFilter = status;
        ViewBag.From = from;
        ViewBag.To = to;
        var result = await PagedList<Appointment>.CreateAsync(q.OrderByDescending(a => a.Date).ThenByDescending(a => a.StartTime), page, 15);
        return View(result);
    }

    // =======================================================================
    // Review moderation
    // =======================================================================
    public async Task<IActionResult> Reviews(int? rating, int page = 1)
    {
        var q = db.Reviews.Include(r => r.Appointment).ThenInclude(a => a.Client)
            .Include(r => r.Appointment).ThenInclude(a => a.Consultant).AsQueryable();
        if (rating != null) q = q.Where(r => r.Rating == rating);
        ViewBag.Rating = rating;
        var result = await PagedList<Review>.CreateAsync(q.OrderByDescending(r => r.CreatedAt), page, 10);
        return View(result);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteReview(int id)
    {
        var r = await db.Reviews.FindAsync(id);
        if (r != null)
        {
            db.Reviews.Remove(r);
            await db.SaveChangesAsync();
            TempData["Info"] = "Review removed.";
        }
        return RedirectToAction("Reviews");
    }
}
