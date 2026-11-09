using ExpertLink.Data;
using ExpertLink.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ExpertLink.Controllers;

// PUBLIC CONSULTANT DIRECTORY - browse, AJAX search/filter/sort/paging, profile.
public class ConsultantsController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index(ConsultantSearchVM vm)
    {
        vm.Result = await Search(vm);
        ViewBag.Categories = new SelectList(await db.Categories.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync(), "Id", "Name", vm.CategoryId);

        if (Request.Headers.XRequestedWith == "XMLHttpRequest")
            return PartialView("_ConsultantCards", vm.Result);
        return View(vm);
    }

    private async Task<PagedList<ConsultantCardVM>> Search(ConsultantSearchVM vm)
    {
        var q = db.Consultants.Where(c => c.IsApproved && c.IsActive);

        if (!string.IsNullOrWhiteSpace(vm.Search))
        {
            var s = vm.Search.Trim();
            q = q.Where(c => c.Name.Contains(s) || (c.Title != null && c.Title.Contains(s)) ||
                             (c.Bio != null && c.Bio.Contains(s)) || c.Services.Any(x => x.IsActive && x.Name.Contains(s)));
        }
        if (vm.CategoryId != null) q = q.Where(c => c.CategoryId == vm.CategoryId);
        if (vm.Mode == "online") q = q.Where(c => c.Services.Any(s => s.IsActive && s.AllowOnline));
        if (vm.Mode == "inperson") q = q.Where(c => c.Services.Any(s => s.IsActive && s.AllowInPerson));
        if (vm.MaxFee != null) q = q.Where(c => c.Services.Any(s => s.IsActive && s.Fee <= vm.MaxFee));

        var cards = q.Select(c => new ConsultantCardVM
        {
            Id = c.Id,
            Name = c.Name,
            Title = c.Title,
            PhotoURL = c.PhotoURL,
            Category = c.Category!.Name,
            CategoryIcon = c.Category.Icon,
            YearsExperience = c.YearsExperience,
            Rating = c.Appointments.Where(a => a.Review != null).Average(a => (double?)a.Review!.Rating) ?? 0,
            ReviewCount = c.Appointments.Count(a => a.Review != null),
            MinFee = c.Services.Where(s => s.IsActive).Min(s => (decimal?)s.Fee),
            Online = c.Services.Any(s => s.IsActive && s.AllowOnline),
            InPerson = c.Services.Any(s => s.IsActive && s.AllowInPerson),
        });

        cards = vm.Sort switch
        {
            "fee" => cards.OrderBy(c => c.MinFee),
            "fee_desc" => cards.OrderByDescending(c => c.MinFee),
            "experience" => cards.OrderByDescending(c => c.YearsExperience),
            "name" => cards.OrderBy(c => c.Name),
            "reviews" => cards.OrderByDescending(c => c.ReviewCount),
            _ => cards.OrderByDescending(c => c.Rating).ThenByDescending(c => c.ReviewCount)
        };

        return await PagedList<ConsultantCardVM>.CreateAsync(cards, vm.Page, 6);
    }

    public async Task<IActionResult> Details(int id)
    {
        var c = await db.Consultants
            .Include(x => x.Category)
            .Include(x => x.Services.Where(s => s.IsActive))
            .Include(x => x.Certificates)
            .Include(x => x.Availabilities)
            .FirstOrDefaultAsync(x => x.Id == id && x.IsApproved && x.IsActive);
        if (c == null) return NotFound();

        var reviews = await db.Reviews.Include(r => r.Appointment).ThenInclude(a => a.Client)
            .Where(r => r.Appointment.ConsultantId == id)
            .OrderByDescending(r => r.CreatedAt).ToListAsync();

        ViewBag.Reviews = reviews.Take(10).ToList();
        ViewBag.ReviewCount = reviews.Count;
        ViewBag.Rating = reviews.Count > 0 ? reviews.Average(r => r.Rating) : 0;
        ViewBag.Breakdown = Enumerable.Range(1, 5).Reverse().Select(n => new { Stars = n, Count = reviews.Count(r => r.Rating == n) }).ToList();
        ViewBag.Sessions = await db.Appointments.CountAsync(a => a.ConsultantId == id && a.Status == AppointmentStatus.Completed);
        return View(c);
    }
}
