using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpertLink.Controllers;

// ---------------------------------------------------------------------------
// MAP PROXY: the server downloads OpenStreetMap tiles and address searches
// (Nominatim) with a proper User-Agent, and caches tiles on disk (TileCache/).
// The browser only talks to our own site, so the maps keep working behind a
// tunnel / proxy and OpenStreetMap's volunteer servers are not overloaded
// (as required by the OSM tile usage policy).
// ---------------------------------------------------------------------------
[AllowAnonymous]
public class MapController(IHttpClientFactory http, IWebHostEnvironment en, ILogger<MapController> log) : Controller
{
    private static readonly TimeSpan CacheAge = TimeSpan.FromDays(7);

    // GET /map/tile/{z}/{x}/{y}.png
    [HttpGet("map/tile/{z:int}/{x:int}/{y:int}.png")]
    [ResponseCache(Duration = 604800, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Tile(int z, int x, int y)
    {
        if (z < 0 || z > 19) return NotFound();
        int max = 1 << z;
        if (x < 0 || y < 0 || x >= max || y >= max) return NotFound();

        var path = Path.Combine(en.ContentRootPath, "TileCache", z.ToString(), x.ToString(), y + ".png");
        if (System.IO.File.Exists(path) && DateTime.Now - System.IO.File.GetLastWriteTime(path) < CacheAge)
            return PhysicalFile(path, "image/png");

        try
        {
            using var resp = await http.CreateClient("osm").GetAsync($"https://tile.openstreetmap.org/{z}/{x}/{y}.png");
            if (!resp.IsSuccessStatusCode)
            {
                log.LogWarning("OSM tile {Z}/{X}/{Y} returned {Status}", z, x, y, (int)resp.StatusCode);
                return System.IO.File.Exists(path) ? PhysicalFile(path, "image/png") : StatusCode((int)resp.StatusCode);
            }
            var bytes = await resp.Content.ReadAsByteArrayAsync();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await System.IO.File.WriteAllBytesAsync(path, bytes);
            return File(bytes, "image/png");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            log.LogWarning(ex, "OSM tile {Z}/{X}/{Y} failed", z, x, y);
            return System.IO.File.Exists(path) ? PhysicalFile(path, "image/png") : StatusCode(502);
        }
    }

    // GET /map/search?q=...   (address -> coordinates, Malaysia only)
    [HttpGet("map/search")]
    public async Task<IActionResult> Search(string? q)
    {
        q = (q ?? "").Trim();
        if (q.Length < 3 || q.Length > 200) return Json(Array.Empty<object>());
        try
        {
            var url = "https://nominatim.openstreetmap.org/search?format=json&limit=1&countrycodes=my&q=" + Uri.EscapeDataString(q);
            var json = await http.CreateClient("osm").GetStringAsync(url);
            return Content(json, "application/json");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            log.LogWarning(ex, "Nominatim search failed");
            return StatusCode(502);
        }
    }
}
