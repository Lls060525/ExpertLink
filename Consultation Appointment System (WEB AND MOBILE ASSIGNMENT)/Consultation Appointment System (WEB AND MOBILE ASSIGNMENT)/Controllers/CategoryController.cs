using System.Text.RegularExpressions;
using ExpertLink.Data;
using ExpertLink.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpertLink.Controllers;

// CATEGORY (specialty) MAINTENANCE - CRUD, batch delete, batch import from CSV.
[Authorize(Roles = "Admin")]
public class CategoryController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index(string? search)
    {
        var q = db.Categories.Include(c => c.Consultants).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(c => c.Name.Contains(search.Trim()));
        ViewBag.Search = search;
        return View(await q.OrderBy(c => c.Name).ToListAsync());
    }

    // Remote validation: is the name unique? (ignores the record being edited)
    [AcceptVerbs("GET", "POST")]
    public async Task<IActionResult> CheckName(string name, int id)
    {
        name = (name ?? "").Trim();
        return Json(!await db.Categories.AnyAsync(c => c.Name == name && c.Id != id));
    }

    public IActionResult Create() => View(new CategoryVM());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CategoryVM vm)
    {
        var name = (vm.Name ?? "").Trim();
        if (await db.Categories.AnyAsync(c => c.Name == name))
            ModelState.AddModelError("Name", "This category name already exists.");
        if (!ModelState.IsValid) return View(vm);

        db.Categories.Add(new Category { Name = name, Description = vm.Description?.Trim(), Icon = vm.Icon, IsActive = vm.IsActive });
        await db.SaveChangesAsync();
        TempData["Info"] = $"Category \"{name}\" created.";
        return RedirectToAction("Index");
    }

    public async Task<IActionResult> Edit(int id)
    {
        var c = await db.Categories.FindAsync(id);
        if (c == null) return NotFound();
        return View(new CategoryVM { Id = c.Id, Name = c.Name, Description = c.Description, Icon = c.Icon, IsActive = c.IsActive });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CategoryVM vm)
    {
        var c = await db.Categories.FindAsync(vm.Id);
        if (c == null) return NotFound();

        var name = (vm.Name ?? "").Trim();
        if (await db.Categories.AnyAsync(x => x.Name == name && x.Id != vm.Id))
            ModelState.AddModelError("Name", "This category name already exists.");
        if (!ModelState.IsValid) return View(vm);

        c.Name = name;
        c.Description = vm.Description?.Trim();
        c.Icon = vm.Icon;
        c.IsActive = vm.IsActive;
        await db.SaveChangesAsync();
        TempData["Info"] = $"Category \"{name}\" updated.";
        return RedirectToAction("Index");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var c = await db.Categories.Include(x => x.Consultants).FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound();
        if (c.Consultants.Count > 0)
        {
            TempData["Error"] = $"\"{c.Name}\" is used by {c.Consultants.Count} consultant(s). Deactivate it instead.";
            return RedirectToAction("Index");
        }
        db.Categories.Remove(c);
        await db.SaveChangesAsync();
        TempData["Info"] = $"Category \"{c.Name}\" deleted.";
        return RedirectToAction("Index");
    }

    // Batch delete (selected with checkboxes)
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> BatchDelete(int[] ids)
    {
        if (ids == null || ids.Length == 0)
        {
            TempData["Error"] = "No category selected.";
            return RedirectToAction("Index");
        }
        var idList = ids.ToList();
        var list = await db.Categories.Include(c => c.Consultants).Where(c => idList.Contains(c.Id)).ToListAsync();
        var deletable = list.Where(c => c.Consultants.Count == 0).ToList();
        db.Categories.RemoveRange(deletable);
        await db.SaveChangesAsync();

        var skipped = list.Count - deletable.Count;
        TempData["Info"] = $"{deletable.Count} category(ies) deleted." + (skipped > 0 ? $" {skipped} skipped because they are in use." : "");
        return RedirectToAction("Index");
    }

    // Batch toggle active status
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> BatchStatus(int[] ids, bool active)
    {
        var idList = (ids ?? []).ToList();
        var list = await db.Categories.Where(c => idList.Contains(c.Id)).ToListAsync();
        list.ForEach(c => c.IsActive = active);
        await db.SaveChangesAsync();
        TempData["Info"] = $"{list.Count} category(ies) {(active ? "activated" : "deactivated")}.";
        return RedirectToAction("Index");
    }

    // Batch insert from a CSV text file:  Name,Description,Icon
    public IActionResult Import() => View(new CategoryImportVM());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(CategoryImportVM vm)
    {
        if (vm.File != null && !Regex.IsMatch(vm.File.FileName, @"\.(csv|txt)$", RegexOptions.IgnoreCase))
            ModelState.AddModelError("File", "Only .csv or .txt files are allowed.");
        if (!ModelState.IsValid) return View(vm);

        var existing = (await db.Categories.Select(c => c.Name).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = new List<string>();
        var errors = new List<string>();

        using var reader = new StreamReader(vm.File!.OpenReadStream());
        int lineNo = 0;
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            lineNo++;
            if (string.IsNullOrWhiteSpace(line)) continue;
            var cols = SplitCsv(line);
            if (lineNo == 1 && cols[0].Trim().Equals("Name", StringComparison.OrdinalIgnoreCase)) continue;   // header

            var name = cols[0].Trim();
            var desc = cols.Count > 1 ? cols[1].Trim() : null;
            var icon = cols.Count > 2 && !string.IsNullOrWhiteSpace(cols[2]) ? cols[2].Trim() : "bi-briefcase";

            if (name.Length is 0 or > 50) { errors.Add($"Line {lineNo}: name must be 1-50 characters."); continue; }
            if (desc?.Length > 300) { errors.Add($"Line {lineNo}: description exceeds 300 characters."); continue; }
            if (!Regex.IsMatch(icon, @"^bi-[a-z0-9\-]+$")) { errors.Add($"Line {lineNo}: invalid icon \"{icon}\"."); continue; }
            if (existing.Contains(name)) { errors.Add($"Line {lineNo}: \"{name}\" already exists."); continue; }

            db.Categories.Add(new Category { Name = name, Description = string.IsNullOrEmpty(desc) ? null : desc, Icon = icon });
            existing.Add(name);
            added.Add(name);
        }
        await db.SaveChangesAsync();

        TempData["Info"] = $"{added.Count} category(ies) imported.";
        if (errors.Count > 0) TempData["Error"] = string.Join(" | ", errors.Take(10)) + (errors.Count > 10 ? " ..." : "");
        return RedirectToAction("Index");
    }

    // Minimal CSV splitter that supports quoted values with commas
    private static List<string> SplitCsv(string line)
    {
        var result = new List<string>();
        var cur = new System.Text.StringBuilder();
        bool quoted = false;
        for (int i = 0; i < line.Length; i++)
        {
            char ch = line[i];
            if (ch == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"') { cur.Append('"'); i++; }
                else quoted = !quoted;
            }
            else if (ch == ',' && !quoted) { result.Add(cur.ToString()); cur.Clear(); }
            else cur.Append(ch);
        }
        result.Add(cur.ToString());
        return result;
    }
}
