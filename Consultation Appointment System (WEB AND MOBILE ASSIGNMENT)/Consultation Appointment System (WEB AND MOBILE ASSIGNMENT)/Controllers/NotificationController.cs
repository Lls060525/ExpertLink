using ExpertLink.Data;
using ExpertLink.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpertLink.Controllers;

[Authorize]
public class NotificationController(AppDbContext db) : Controller
{
    private int Me => User.GetUserId();

    public async Task<IActionResult> Index()
    {
        var list = await db.Notifications.Where(n => n.UserId == Me)
            .OrderByDescending(n => n.CreatedAt).Take(100).ToListAsync();
        return View(list);
    }

    // AJAX: unread count + latest items for the navbar bell
    public async Task<IActionResult> Latest()
    {
        var unread = await db.Notifications.CountAsync(n => n.UserId == Me && !n.IsRead);
        var items = await db.Notifications.Where(n => n.UserId == Me)
            .OrderByDescending(n => n.CreatedAt).Take(6)
            .Select(n => new { n.Id, n.Message, n.Url, n.IsRead, n.CreatedAt })
            .ToListAsync();
        return Json(new
        {
            unread,
            items = items.Select(n => new { n.Id, n.Message, n.Url, n.IsRead, time = Ago(n.CreatedAt) })
        });
    }

    // Open a notification: mark as read and go to its link
    public async Task<IActionResult> Open(int id)
    {
        var n = await db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == Me);
        if (n == null) return RedirectToAction("Index");
        n.IsRead = true;
        await db.SaveChangesAsync();
        return !string.IsNullOrEmpty(n.Url) && Url.IsLocalUrl(n.Url) ? Redirect(n.Url) : RedirectToAction("Index");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead()
    {
        await db.Notifications.Where(n => n.UserId == Me && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
        if (Request.Headers.XRequestedWith == "XMLHttpRequest") return Json(new { ok = true });
        return RedirectToAction("Index");
    }

    public static string Ago(DateTime t)
    {
        var span = DateTime.Now - t;
        if (span.TotalMinutes < 1) return "just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} min ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours} hr ago";
        if (span.TotalDays < 7) return $"{(int)span.TotalDays} day(s) ago";
        return t.ToString("d MMM yyyy");
    }
}
