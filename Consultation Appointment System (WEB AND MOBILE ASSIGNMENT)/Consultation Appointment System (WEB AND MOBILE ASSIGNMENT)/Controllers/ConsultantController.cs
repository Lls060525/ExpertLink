using ExpertLink.Data;
using ExpertLink.Models;
using ExpertLink.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ExpertLink.Controllers;

// CONSULTANT SELF-SERVICE: dashboard, profile & certificates, services,
// weekly availability, time off, reviews.
[Authorize(Roles = "Consultant")]
public class ConsultantController(AppDbContext db, Helper hp, NotificationService ns) : Controller
{
    private int Me => User.GetUserId();

    // =======================================================================
    // Dashboard
    // =======================================================================
    public async Task<IActionResult> Dashboard()
    {
        var me = await db.Consultants.Include(c => c.Services).Include(c => c.Availabilities)
            .FirstOrDefaultAsync(c => c.Id == Me);
        if (me == null) return RedirectToAction("Login", "Account");

        var today = DateOnly.FromDateTime(DateTime.Now);
        var mine = db.Appointments.Include(a => a.Client).Include(a => a.Service).Where(a => a.ConsultantId == Me);

        ViewBag.Today = await mine.Where(a => a.Date == today &&
                (a.Status == AppointmentStatus.Confirmed || a.Status == AppointmentStatus.CheckedIn || a.Status == AppointmentStatus.Completed))
            .OrderBy(a => a.StartTime).ToListAsync();
        ViewBag.PendingList = await mine.Where(a => a.Status == AppointmentStatus.Pending && a.Payment != null)
            .OrderBy(a => a.Date).ThenBy(a => a.StartTime).Take(5).ToListAsync();
        ViewBag.Upcoming = await mine.CountAsync(a => a.Date > today && a.Status == AppointmentStatus.Confirmed);
        ViewBag.Completed = await mine.CountAsync(a => a.Status == AppointmentStatus.Completed);
        ViewBag.Earnings = await mine.Where(a => a.Status == AppointmentStatus.Completed && a.Date.Month == today.Month && a.Date.Year == today.Year)
            .SumAsync(a => (decimal?)a.Fee) ?? 0;
        ViewBag.Rating = await db.Reviews.Where(r => r.Appointment.ConsultantId == Me).AverageAsync(r => (double?)r.Rating) ?? 0;

        // Setup checklist for new consultants
        ViewBag.NeedsProfile = string.IsNullOrWhiteSpace(me.Bio);
        ViewBag.NeedsServices = !me.Services.Any(s => s.IsActive);
        ViewBag.NeedsAvailability = me.Availabilities.Count == 0;
        return View(me);
    }

    // =======================================================================
    // Profile (with map location) + certificate photos
    // =======================================================================
    public async Task<IActionResult> Profile()
    {
        var c = await db.Consultants.Include(x => x.Certificates).FirstOrDefaultAsync(x => x.Id == Me);
        if (c == null) return RedirectToAction("Login", "Account");

        ViewBag.Categories = new SelectList(await db.Categories.Where(x => x.IsActive || x.Id == c.CategoryId).OrderBy(x => x.Name).ToListAsync(), "Id", "Name");
        ViewBag.Certificates = c.Certificates.OrderByDescending(x => x.UploadedAt).ToList();
        return View(new ConsultantProfileVM
        {
            Email = c.Email, PhotoURL = c.PhotoURL, Name = c.Name, Phone = c.Phone ?? "",
            Title = c.Title ?? "", Bio = c.Bio ?? "", Qualification = c.Qualification ?? "",
            YearsExperience = c.YearsExperience, CategoryId = c.CategoryId,
            OfficeAddress = c.OfficeAddress, Latitude = c.Latitude, Longitude = c.Longitude
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ConsultantProfileVM vm)
    {
        var c = await db.Consultants.Include(x => x.Certificates).FirstOrDefaultAsync(x => x.Id == Me);
        if (c == null) return RedirectToAction("Login", "Account");

        if (vm.Photo != null)
        {
            var err = hp.ValidatePhoto(vm.Photo);
            if (err != "") ModelState.AddModelError("Photo", err);
        }
        if ((vm.Latitude == null) != (vm.Longitude == null))
            ModelState.AddModelError("Latitude", "Please pick a location on the map (both latitude and longitude are needed).");
        if (vm.CategoryId != null && !await db.Categories.AnyAsync(x => x.Id == vm.CategoryId))
            ModelState.AddModelError("CategoryId", "Invalid specialty.");

        if (!ModelState.IsValid)
        {
            ViewBag.Categories = new SelectList(await db.Categories.OrderBy(x => x.Name).ToListAsync(), "Id", "Name");
            ViewBag.Certificates = c.Certificates.OrderByDescending(x => x.UploadedAt).ToList();
            vm.Email = c.Email;
            vm.PhotoURL = c.PhotoURL;
            return View(vm);
        }

        c.Name = vm.Name.Trim();
        c.Phone = vm.Phone;
        c.Title = vm.Title.Trim();
        c.Bio = vm.Bio.Trim();
        c.Qualification = vm.Qualification.Trim();
        c.YearsExperience = vm.YearsExperience;
        c.CategoryId = vm.CategoryId;
        c.OfficeAddress = vm.OfficeAddress?.Trim();
        c.Latitude = vm.Latitude;
        c.Longitude = vm.Longitude;
        if (vm.Photo != null)
        {
            hp.DeletePhoto(c.PhotoURL);
            c.PhotoURL = hp.SavePhoto(vm.Photo, "users");
        }
        await db.SaveChangesAsync();
        await hp.RefreshSignIn(c);

        TempData["Info"] = "Profile updated.";
        return RedirectToAction("Profile");
    }

    // Multiple photos uploaded in one form submission (drag & drop supported in the view)
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadCertificates(List<IFormFile> photos, string? caption)
    {
        const int MaxPhotos = 10;
        var count = await db.CertificatePhotos.CountAsync(x => x.ConsultantId == Me);

        if (photos == null || photos.Count == 0)
        {
            TempData["Error"] = "Please choose at least one photo.";
            return RedirectToAction("Profile");
        }
        if (count + photos.Count > MaxPhotos)
        {
            TempData["Error"] = $"You can upload at most {MaxPhotos} certificate photos ({MaxPhotos - count} remaining).";
            return RedirectToAction("Profile");
        }
        if (caption?.Length > 100) caption = caption[..100];

        var errors = new List<string>();
        int saved = 0;
        foreach (var f in photos)
        {
            var err = hp.ValidatePhoto(f);
            if (err != "") { errors.Add($"{f.FileName}: {err}"); continue; }
            db.CertificatePhotos.Add(new CertificatePhoto
            {
                ConsultantId = Me,
                PhotoURL = hp.SavePhoto(f, "certificates", 1200, 900, square: false),
                Caption = string.IsNullOrWhiteSpace(caption) ? Truncate(Path.GetFileNameWithoutExtension(f.FileName), 100) : caption.Trim(),
            });
            saved++;
        }
        await db.SaveChangesAsync();

        TempData["Info"] = $"{saved} photo(s) uploaded.";
        if (errors.Count > 0) TempData["Error"] = string.Join(" | ", errors);
        return RedirectToAction("Profile");
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCertificate(int id)
    {
        var p = await db.CertificatePhotos.FirstOrDefaultAsync(x => x.Id == id && x.ConsultantId == Me);
        if (p == null) return NotFound();
        hp.DeletePhoto(p.PhotoURL);
        db.CertificatePhotos.Remove(p);
        await db.SaveChangesAsync();
        TempData["Info"] = "Photo deleted.";
        return RedirectToAction("Profile");
    }

    // =======================================================================
    // Services (consultation packages)
    // =======================================================================
    public async Task<IActionResult> Services()
    {
        var list = await db.Services.Where(s => s.ConsultantId == Me)
            .Select(s => new { Service = s, Bookings = s.Appointments.Count })
            .OrderByDescending(x => x.Service.IsActive).ThenBy(x => x.Service.Name)
            .ToListAsync();
        ViewBag.Bookings = list.ToDictionary(x => x.Service.Id, x => x.Bookings);
        return View(list.Select(x => x.Service).ToList());
    }

    public IActionResult CreateService() => View("ServiceForm", new ServiceVM());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateService(ServiceVM vm)
    {
        if (await db.Services.AnyAsync(s => s.ConsultantId == Me && s.Name == vm.Name.Trim()))
            ModelState.AddModelError("Name", "You already have a service with this name.");
        if (!ModelState.IsValid) return View("ServiceForm", vm);

        db.Services.Add(new Service
        {
            ConsultantId = Me, Name = vm.Name.Trim(), Description = vm.Description?.Trim(),
            DurationMinutes = vm.DurationMinutes!.Value, Fee = vm.Fee!.Value,
            AllowOnline = vm.AllowOnline, AllowInPerson = vm.AllowInPerson, IsActive = vm.IsActive
        });
        await db.SaveChangesAsync();
        TempData["Info"] = "Service created.";
        return RedirectToAction("Services");
    }

    public async Task<IActionResult> EditService(int id)
    {
        var s = await db.Services.FirstOrDefaultAsync(x => x.Id == id && x.ConsultantId == Me);
        if (s == null) return NotFound();
        return View("ServiceForm", new ServiceVM
        {
            Id = s.Id, Name = s.Name, Description = s.Description, DurationMinutes = s.DurationMinutes,
            Fee = s.Fee, AllowOnline = s.AllowOnline, AllowInPerson = s.AllowInPerson, IsActive = s.IsActive
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditService(ServiceVM vm)
    {
        var s = await db.Services.FirstOrDefaultAsync(x => x.Id == vm.Id && x.ConsultantId == Me);
        if (s == null) return NotFound();
        if (await db.Services.AnyAsync(x => x.ConsultantId == Me && x.Name == vm.Name.Trim() && x.Id != vm.Id))
            ModelState.AddModelError("Name", "You already have a service with this name.");
        if (!ModelState.IsValid) return View("ServiceForm", vm);

        // Fee / duration changes only affect new bookings (existing appointments keep their own Fee/EndTime)
        s.Name = vm.Name.Trim();
        s.Description = vm.Description?.Trim();
        s.DurationMinutes = vm.DurationMinutes!.Value;
        s.Fee = vm.Fee!.Value;
        s.AllowOnline = vm.AllowOnline;
        s.AllowInPerson = vm.AllowInPerson;
        s.IsActive = vm.IsActive;
        await db.SaveChangesAsync();
        TempData["Info"] = "Service updated.";
        return RedirectToAction("Services");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteService(int id)
    {
        var s = await db.Services.Include(x => x.Appointments).FirstOrDefaultAsync(x => x.Id == id && x.ConsultantId == Me);
        if (s == null) return NotFound();
        if (s.Appointments.Count > 0)
        {
            s.IsActive = false;   // keep history, just hide it
            TempData["Info"] = "This service has booking history, so it was deactivated instead of deleted.";
        }
        else
        {
            db.Services.Remove(s);
            TempData["Info"] = "Service deleted.";
        }
        await db.SaveChangesAsync();
        return RedirectToAction("Services");
    }

    // =======================================================================
    // Weekly availability (batch insert / update / delete of rows)
    // =======================================================================
    public async Task<IActionResult> Availability()
    {
        var rows = await db.Availabilities.Where(a => a.ConsultantId == Me)
            .OrderBy(a => a.Day).ThenBy(a => a.StartTime)
            .Select(a => new AvailabilityRowVM { Day = a.Day, StartTime = a.StartTime, EndTime = a.EndTime })
            .ToListAsync();
        // Show Monday first
        rows = rows.OrderBy(r => ((int)r.Day + 6) % 7).ThenBy(r => r.StartTime).ToList();
        return View(new AvailabilityVM { Rows = rows });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Availability(AvailabilityVM vm)
    {
        vm.Rows ??= [];
        // Overlap check per day
        foreach (var g in vm.Rows.Where(r => r.StartTime != null && r.EndTime != null).GroupBy(r => r.Day))
        {
            var sorted = g.OrderBy(r => r.StartTime).ToList();
            for (int i = 1; i < sorted.Count; i++)
                if (sorted[i].StartTime < sorted[i - 1].EndTime)
                    ModelState.AddModelError("", $"{g.Key}: time ranges {sorted[i - 1].StartTime:HH:mm}-{sorted[i - 1].EndTime:HH:mm} and {sorted[i].StartTime:HH:mm}-{sorted[i].EndTime:HH:mm} overlap.");
        }
        if (!ModelState.IsValid) return View(vm);

        db.Availabilities.RemoveRange(db.Availabilities.Where(a => a.ConsultantId == Me));
        foreach (var r in vm.Rows)
            db.Availabilities.Add(new Availability { ConsultantId = Me, Day = r.Day, StartTime = r.StartTime!.Value, EndTime = r.EndTime!.Value });
        await db.SaveChangesAsync();

        TempData["Info"] = $"Weekly schedule saved ({vm.Rows.Count} time range(s)). Existing appointments are not affected.";
        return RedirectToAction("Availability");
    }

    // =======================================================================
    // Time off (leave dates)
    // =======================================================================
    public async Task<IActionResult> TimeOff()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        ViewBag.List = await db.TimeOffs.Where(t => t.ConsultantId == Me && t.Date >= today).OrderBy(t => t.Date).ToListAsync();
        return View(new TimeOffVM());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> TimeOff(TimeOffVM vm)
    {
        if (vm.Date != null)
        {
            if (await db.TimeOffs.AnyAsync(t => t.ConsultantId == Me && t.Date == vm.Date))
                ModelState.AddModelError("Date", "This date is already marked as time off.");

            var clash = await db.Appointments.CountAsync(a => a.ConsultantId == Me && a.Date == vm.Date &&
                (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed));
            if (clash > 0)
                ModelState.AddModelError("Date", $"You have {clash} active appointment(s) on this date. Please reject or complete them first.");
        }
        if (!ModelState.IsValid)
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            ViewBag.List = await db.TimeOffs.Where(t => t.ConsultantId == Me && t.Date >= today).OrderBy(t => t.Date).ToListAsync();
            return View(vm);
        }

        db.TimeOffs.Add(new TimeOff { ConsultantId = Me, Date = vm.Date!.Value, Reason = vm.Reason?.Trim() });
        await db.SaveChangesAsync();
        TempData["Info"] = $"{vm.Date:dddd, d MMM yyyy} marked as time off.";
        return RedirectToAction("TimeOff");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTimeOff(int id)
    {
        var t = await db.TimeOffs.FirstOrDefaultAsync(x => x.Id == id && x.ConsultantId == Me);
        if (t != null)
        {
            db.TimeOffs.Remove(t);
            await db.SaveChangesAsync();
            TempData["Info"] = "Time off removed.";
        }
        return RedirectToAction("TimeOff");
    }

    // =======================================================================
    // Reviews & replies
    // =======================================================================
    public async Task<IActionResult> Reviews()
    {
        var list = await db.Reviews.Include(r => r.Appointment).ThenInclude(a => a.Client)
            .Include(r => r.Appointment).ThenInclude(a => a.Service)
            .Where(r => r.Appointment.ConsultantId == Me)
            .OrderByDescending(r => r.CreatedAt).ToListAsync();
        return View(list);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reply(ReplyVM vm)
    {
        var r = await db.Reviews.Include(x => x.Appointment)
            .FirstOrDefaultAsync(x => x.AppointmentId == vm.AppointmentId && x.Appointment.ConsultantId == Me);
        if (r == null) return NotFound();
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Reply cannot be empty and must be at most 500 characters.";
            return RedirectToAction("Reviews");
        }
        r.Reply = vm.Reply.Trim();
        await db.SaveChangesAsync();
        await ns.NotifyAsync(r.Appointment.ClientId, $"{User.Identity!.Name} replied to your review.", $"/Consultants/Details/{Me}#reviews");
        TempData["Info"] = "Reply posted.";
        return RedirectToAction("Reviews");
    }
}
