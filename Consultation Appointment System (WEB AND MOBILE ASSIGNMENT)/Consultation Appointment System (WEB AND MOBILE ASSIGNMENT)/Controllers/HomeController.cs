using System.Diagnostics;
using ExpertLink.Data;
using ExpertLink.Models;
using ExpertLink.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpertLink.Controllers;

public class HomeController(AppDbContext db, Helper hp) : Controller
{
    public async Task<IActionResult> Index()
    {
        if (User.IsInRole("Admin")) return RedirectToAction("Dashboard", "Admin");
        if (User.IsInRole("Consultant")) return RedirectToAction("Dashboard", "Consultant");

        ViewBag.Categories = await db.Categories
            .Where(c => c.IsActive)
            .Select(c => new { c.Id, c.Name, c.Icon, c.Description, Count = c.Consultants.Count(x => x.IsApproved && x.IsActive) })
            .ToListAsync();

        ViewBag.Top = await db.Consultants
            .Where(c => c.IsApproved && c.IsActive)
            .Select(c => new ConsultantCardVM
            {
                Id = c.Id, Name = c.Name, Title = c.Title, PhotoURL = c.PhotoURL,
                Category = c.Category!.Name, CategoryIcon = c.Category.Icon,
                YearsExperience = c.YearsExperience,
                Rating = c.Appointments.Where(a => a.Review != null).Average(a => (double?)a.Review!.Rating) ?? 0,
                ReviewCount = c.Appointments.Count(a => a.Review != null),
                MinFee = c.Services.Where(s => s.IsActive).Min(s => (decimal?)s.Fee),
                Online = c.Services.Any(s => s.IsActive && s.AllowOnline),
                InPerson = c.Services.Any(s => s.IsActive && s.AllowInPerson),
            })
            .OrderByDescending(c => c.Rating).ThenByDescending(c => c.ReviewCount)
            .Take(4)
            .ToListAsync();

        ViewBag.Stats = new
        {
            Consultants = await db.Consultants.CountAsync(c => c.IsApproved && c.IsActive),
            Sessions = await db.Appointments.CountAsync(a => a.Status == AppointmentStatus.Completed),
            Rating = await db.Reviews.AverageAsync(r => (double?)r.Rating) ?? 0
        };
        return View();
    }

    public IActionResult Privacy() => View();

    // AJAX: save theme preference (cookie for everyone, DB for signed-in users)
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetTheme(string theme)
    {
        theme = theme == "dark" ? "dark" : "light";
        hp.SetThemeCookie(theme);
        if (User.Identity?.IsAuthenticated == true)
        {
            var u = await db.Users.FindAsync(User.GetUserId());
            if (u != null)
            {
                u.Theme = theme;
                await db.SaveChangesAsync();
            }
        }
        return Json(new { ok = true, theme });
    }

    [Route("/Home/Status/{code:int}")]
    public IActionResult Status(int code)
    {
        ViewBag.Code = code;
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
