using System.Security.Claims;
using System.Text.RegularExpressions;
using ExpertLink.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace ExpertLink.Services;

// General helper: password hashing, cookie sign-in/out, photo upload, theme.
public class Helper(IWebHostEnvironment en, IHttpContextAccessor ct)
{
    // ===================================================================
    // Password hashing (PBKDF2 via PasswordHasher - NOT the Identity framework)
    // ===================================================================
    private readonly PasswordHasher<object> ph = new();

    public string HashPassword(string password) => ph.HashPassword(0, password);

    public bool VerifyPassword(string hash, string password) =>
        ph.VerifyHashedPassword(0, hash, password) != PasswordVerificationResult.Failed;

    // ===================================================================
    // Manual cookie authentication
    // ===================================================================
    public async Task SignIn(User u, bool rememberMe)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, u.Id.ToString()),
            new(ClaimTypes.Name, u.Name),
            new(ClaimTypes.Email, u.Email),
            new(ClaimTypes.Role, u.Role),
            new("Photo", u.PhotoURL ?? ""),
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var props = new AuthenticationProperties { IsPersistent = rememberMe };
        await ct.HttpContext!.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, props);
    }

    // Re-issue the cookie after the profile (name / photo) changed
    public async Task RefreshSignIn(User u)
    {
        var auth = await ct.HttpContext!.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        await SignIn(u, auth.Properties?.IsPersistent ?? false);
    }

    public async Task SignOut()
    {
        await ct.HttpContext!.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    // ===================================================================
    // User preference: theme (light / dark) stored in DB + cookie
    // ===================================================================
    public void SetThemeCookie(string theme)
    {
        ct.HttpContext!.Response.Cookies.Append("theme", theme == "dark" ? "dark" : "light",
            new CookieOptions { Expires = DateTimeOffset.Now.AddYears(1), IsEssential = true, HttpOnly = false });
    }

    // ===================================================================
    // Photo upload (validated + resized with ImageSharp)
    // ===================================================================
    public string ValidatePhoto(IFormFile f)
    {
        var reType = new Regex(@"^image\/(jpeg|png|webp)$", RegexOptions.IgnoreCase);
        var reName = new Regex(@"^.+\.(jpeg|jpg|png|webp)$", RegexOptions.IgnoreCase);

        if (!reType.IsMatch(f.ContentType) || !reName.IsMatch(f.FileName))
            return "Only JPG, PNG or WEBP photos are allowed.";
        if (f.Length > 2 * 1024 * 1024)
            return "Photo size must not exceed 2 MB.";
        return "";
    }

    // square = true  -> cropped square avatar (e.g. 400x400)
    // square = false -> keeps aspect ratio, fits within width x height
    public string SavePhoto(IFormFile f, string folder, int width = 400, int height = 400, bool square = true)
    {
        var dir = Path.Combine(en.WebRootPath, "uploads", folder);
        Directory.CreateDirectory(dir);

        var file = Guid.NewGuid().ToString("n") + ".jpg";
        var path = Path.Combine(dir, file);

        var options = new ResizeOptions
        {
            Size = new Size(width, height),
            Mode = square ? ResizeMode.Crop : ResizeMode.Max,
        };

        using var stream = f.OpenReadStream();
        using var img = Image.Load(stream);
        img.Mutate(x => x.AutoOrient().Resize(options));
        img.Save(path, new JpegEncoder { Quality = 85 });

        return $"/uploads/{folder}/{file}";
    }

    public void DeletePhoto(string? url)
    {
        if (string.IsNullOrEmpty(url) || !url.StartsWith("/uploads/")) return;
        var path = Path.Combine(en.WebRootPath, url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(path)) File.Delete(path);
    }

    // ===================================================================
    // Misc
    // ===================================================================
    public static string Avatar(string? url) => string.IsNullOrEmpty(url) ? "/images/avatar.svg" : url;

    public static string Money(decimal amount) => "RM " + amount.ToString("N2");
}
