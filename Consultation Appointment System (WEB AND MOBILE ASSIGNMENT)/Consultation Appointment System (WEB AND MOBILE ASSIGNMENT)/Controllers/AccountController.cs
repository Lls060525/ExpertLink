using System.Net;
using ExpertLink.Data;
using ExpertLink.Models;
using ExpertLink.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ExpertLink.Controllers;

// SECURITY MODULE: login, logout, registration, e-mail verification,
// password reset/change, profile, captcha, temporary login blocking.
public class AccountController(AppDbContext db, Helper hp, EmailService email, CaptchaService captcha, NotificationService ns) : Controller
{
    private const int MaxFailedAttempts = 3;
    private const int LockoutMinutes = 5;

    // =======================================================================
    // Captcha
    // =======================================================================
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Captcha()
    {
        var (code, svg) = captcha.Generate();
        HttpContext.Session.SetString("Captcha", code);
        return Content(svg, "image/svg+xml");
    }

    private bool CaptchaValid(string? input)
    {
        var code = HttpContext.Session.GetString("Captcha");
        HttpContext.Session.Remove("Captcha");   // one-time use
        return code != null && input != null && string.Equals(code, input.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    // Remote validation: is the e-mail still available?
    [AcceptVerbs("GET", "POST")]
    public async Task<IActionResult> CheckEmail(string email)
    {
        email = (email ?? "").Trim().ToLower();
        return Json(!await db.Users.AnyAsync(u => u.Email == email));
    }

    private IActionResult RedirectToRoleHome(string role) => role switch
    {
        "Admin" => RedirectToAction("Dashboard", "Admin"),
        "Consultant" => RedirectToAction("Dashboard", "Consultant"),
        _ => RedirectToAction("Index", "Home")
    };

    // =======================================================================
    // Login / Logout
    // =======================================================================
    public IActionResult Login(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        ViewBag.ShowCaptcha = (HttpContext.Session.GetInt32("LoginFails") ?? 0) > 0;
        return View(new LoginVM { ReturnUrl = returnUrl });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginVM vm)
    {
        int fails = HttpContext.Session.GetInt32("LoginFails") ?? 0;
        if (fails > 0 && !CaptchaValid(vm.Captcha))
            ModelState.AddModelError("Captcha", "Incorrect captcha code.");

        User? u = null;
        if (ModelState.IsValid)
        {
            var mail = vm.Email.Trim().ToLower();
            u = await db.Users.FirstOrDefaultAsync(x => x.Email == mail);

            if (u == null)
            {
                ModelState.AddModelError("", "Invalid email or password.");
            }
            else if (u.LockoutEnd != null && u.LockoutEnd > DateTime.Now)
            {
                var mins = Math.Ceiling((u.LockoutEnd.Value - DateTime.Now).TotalMinutes);
                ModelState.AddModelError("", $"Your account is temporarily locked because of too many failed attempts. Please try again in {mins} minute(s).");
            }
            else if (!hp.VerifyPassword(u.Hash, vm.Password))
            {
                u.FailedLoginCount++;
                if (u.FailedLoginCount >= MaxFailedAttempts)
                {
                    u.LockoutEnd = DateTime.Now.AddMinutes(LockoutMinutes);
                    u.FailedLoginCount = 0;
                    ModelState.AddModelError("", $"Too many failed attempts. Your account is locked for {LockoutMinutes} minutes.");
                }
                else
                {
                    ModelState.AddModelError("", $"Invalid email or password. {MaxFailedAttempts - u.FailedLoginCount} attempt(s) left before your account is temporarily locked.");
                }
                await db.SaveChangesAsync();
            }
            else if (!u.IsActive)
            {
                ModelState.AddModelError("", "Your account has been deactivated. Please contact the administrator.");
            }
            else if (!u.EmailVerified)
            {
                ModelState.AddModelError("", "Please verify your email address before logging in.");
                ViewBag.Unverified = u.Email;
            }
        }

        if (!ModelState.IsValid || u == null)
        {
            HttpContext.Session.SetInt32("LoginFails", fails + 1);
            ViewBag.ShowCaptcha = true;
            vm.Captcha = "";
            return View(vm);
        }

        u.FailedLoginCount = 0;
        u.LockoutEnd = null;
        await db.SaveChangesAsync();
        HttpContext.Session.Remove("LoginFails");

        await hp.SignIn(u, vm.RememberMe);
        hp.SetThemeCookie(u.Theme);
        TempData["Info"] = $"Welcome back, {u.Name}!";

        if (!string.IsNullOrEmpty(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl))
            return Redirect(vm.ReturnUrl);
        return RedirectToRoleHome(u.Role);
    }

    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await hp.SignOut();
        TempData["Info"] = "You have been logged out.";
        return RedirectToAction("Index", "Home");
    }

    public IActionResult AccessDenied() => View();

    // =======================================================================
    // Registration (Client)
    // =======================================================================
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        return View(new RegisterVM());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterVM vm)
    {
        await ValidateRegistration(vm);
        if (!ModelState.IsValid) return View(vm);

        var c = new Client
        {
            Name = vm.Name.Trim(),
            Email = vm.Email.Trim().ToLower(),
            Phone = vm.Phone,
            Hash = hp.HashPassword(vm.Password),
            CreatedAt = DateTime.Now,
        };
        if (vm.Photo != null) c.PhotoURL = hp.SavePhoto(vm.Photo, "users");

        db.Clients.Add(c);
        await db.SaveChangesAsync();

        await SendVerificationEmail(c);
        return RedirectToAction("Login");
    }

    // =======================================================================
    // Registration (Consultant application - requires admin approval)
    // =======================================================================
    public IActionResult RegisterConsultant()
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        ViewBag.Categories = CategoryList();
        return View(new ConsultantRegisterVM());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterConsultant(ConsultantRegisterVM vm)
    {
        await ValidateRegistration(vm);
        if (vm.CategoryId != null && !await db.Categories.AnyAsync(c => c.Id == vm.CategoryId && c.IsActive))
            ModelState.AddModelError("CategoryId", "Invalid specialty.");

        if (!ModelState.IsValid)
        {
            ViewBag.Categories = CategoryList();
            return View(vm);
        }

        var c = new Consultant
        {
            Name = vm.Name.Trim(),
            Email = vm.Email.Trim().ToLower(),
            Phone = vm.Phone,
            Hash = hp.HashPassword(vm.Password),
            CategoryId = vm.CategoryId,
            Title = vm.Title.Trim(),
            Qualification = vm.Qualification.Trim(),
            YearsExperience = vm.YearsExperience,
            IsApproved = false,
            CreatedAt = DateTime.Now,
        };
        if (vm.Photo != null) c.PhotoURL = hp.SavePhoto(vm.Photo, "users");

        db.Consultants.Add(c);
        await db.SaveChangesAsync();

        await ns.NotifyAdminsAsync($"New consultant application from {c.Name}.", $"/Admin/UserDetail/{c.Id}");
        await SendVerificationEmail(c);
        return RedirectToAction("Login");
    }

    private async Task ValidateRegistration(RegisterVM vm)
    {
        var mail = (vm.Email ?? "").Trim().ToLower();
        if (await db.Users.AnyAsync(u => u.Email == mail))
            ModelState.AddModelError("Email", "This email has already been registered.");

        if (!CaptchaValid(vm.Captcha))
            ModelState.AddModelError("Captcha", "Incorrect captcha code.");

        if (vm.Photo != null)
        {
            var err = hp.ValidatePhoto(vm.Photo);
            if (err != "") ModelState.AddModelError("Photo", err);
        }
        vm.Captcha = "";
    }

    private List<SelectListItem> CategoryList() =>
        db.Categories.Where(c => c.IsActive).OrderBy(c => c.Name)
          .Select(c => new SelectListItem(c.Name, c.Id.ToString())).ToList();

    // =======================================================================
    // E-mail verification
    // =======================================================================
    private async Task SendVerificationEmail(User u)
    {
        var token = new UserToken { UserId = u.Id, Purpose = "Verify", Expiry = DateTime.Now.AddHours(24) };
        db.UserTokens.Add(token);
        await db.SaveChangesAsync();

        var link = Url.Action("Verify", "Account", new { id = token.Id }, Request.Scheme)!;
        var body = EmailService.Template("Verify your email address",
            $"<p>Hi {WebUtility.HtmlEncode(u.Name)},</p><p>Thank you for registering with ExpertLink. Please confirm your email address to activate your account. This link expires in 24 hours.</p>",
            "Verify Email", link);
        bool sent = email.Send(u.Email, "Verify your ExpertLink account", body);

        TempData["Info"] = sent
            ? "Registration successful! Please check your email to verify your account."
            : "Registration successful! The verification email could not be sent (SMTP not configured), so use the demo link below.";
        if (!sent) TempData["DemoLink"] = link;
    }

    public async Task<IActionResult> Verify(string id)
    {
        var t = await db.UserTokens.Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Id == id && t.Purpose == "Verify");
        if (t == null || t.Expiry < DateTime.Now)
        {
            TempData["Error"] = "The verification link is invalid or has expired. Please request a new one.";
            return RedirectToAction("Login");
        }

        t.User.EmailVerified = true;
        db.UserTokens.Remove(t);
        await db.SaveChangesAsync();

        TempData["Info"] = t.User is Consultant
            ? "Email verified! You can now log in and complete your profile while the admin reviews your application."
            : "Email verified! You can now log in.";
        return RedirectToAction("Login");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendVerification(string email)
    {
        var mail = (email ?? "").Trim().ToLower();
        var u = await db.Users.FirstOrDefaultAsync(x => x.Email == mail && !x.EmailVerified);
        if (u != null)
        {
            db.UserTokens.RemoveRange(db.UserTokens.Where(t => t.UserId == u.Id && t.Purpose == "Verify"));
            await SendVerificationEmail(u);
            if (TempData.Peek("DemoLink") == null) TempData["Info"] = "A new verification email has been sent.";
            else TempData["Info"] = "A new verification link has been generated (SMTP not configured).";
        }
        return RedirectToAction("Login");
    }

    // =======================================================================
    // Forgot / reset password (token-based e-mail link)
    // =======================================================================
    public IActionResult ForgotPassword() => View(new ForgotPasswordVM());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordVM vm)
    {
        if (!CaptchaValid(vm.Captcha)) ModelState.AddModelError("Captcha", "Incorrect captcha code.");
        if (!ModelState.IsValid)
        {
            vm.Captcha = "";
            return View(vm);
        }

        var mail = vm.Email.Trim().ToLower();
        var u = await db.Users.FirstOrDefaultAsync(x => x.Email == mail && x.IsActive);
        if (u != null)
        {
            db.UserTokens.RemoveRange(db.UserTokens.Where(t => t.UserId == u.Id && t.Purpose == "Reset"));
            var token = new UserToken { UserId = u.Id, Purpose = "Reset", Expiry = DateTime.Now.AddMinutes(30) };
            db.UserTokens.Add(token);
            await db.SaveChangesAsync();

            var link = Url.Action("ResetPassword", "Account", new { id = token.Id }, Request.Scheme)!;
            var body = EmailService.Template("Reset your password",
                $"<p>Hi {WebUtility.HtmlEncode(u.Name)},</p><p>We received a request to reset your ExpertLink password. The link below is valid for 30 minutes. If you did not request this, you can ignore this email.</p>",
                "Reset Password", link);
            if (!email.Send(u.Email, "Reset your ExpertLink password", body))
                TempData["DemoLink"] = link;
        }

        // Same message whether or not the e-mail exists (prevents account enumeration)
        TempData["Info"] = "If the email is registered, a password reset link has been sent to it.";
        return RedirectToAction("Login");
    }

    public async Task<IActionResult> ResetPassword(string id)
    {
        var t = await db.UserTokens.FirstOrDefaultAsync(t => t.Id == id && t.Purpose == "Reset");
        if (t == null || t.Expiry < DateTime.Now)
        {
            TempData["Error"] = "The password reset link is invalid or has expired.";
            return RedirectToAction("ForgotPassword");
        }
        return View(new ResetPasswordVM { Token = id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordVM vm)
    {
        var t = await db.UserTokens.Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Id == vm.Token && t.Purpose == "Reset");
        if (t == null || t.Expiry < DateTime.Now)
        {
            TempData["Error"] = "The password reset link is invalid or has expired.";
            return RedirectToAction("ForgotPassword");
        }
        if (!ModelState.IsValid) return View(vm);

        t.User.Hash = hp.HashPassword(vm.New);
        t.User.FailedLoginCount = 0;
        t.User.LockoutEnd = null;
        db.UserTokens.RemoveRange(db.UserTokens.Where(x => x.UserId == t.UserId && x.Purpose == "Reset"));
        await db.SaveChangesAsync();

        TempData["Info"] = "Your password has been reset. Please log in with your new password.";
        return RedirectToAction("Login");
    }

    // =======================================================================
    // Change password
    // =======================================================================
    [Authorize]
    public IActionResult ChangePassword() => View(new ChangePasswordVM());

    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordVM vm)
    {
        var u = await db.Users.FindAsync(User.GetUserId());
        if (u == null) return RedirectToAction("Login");

        if (ModelState.IsValid && !hp.VerifyPassword(u.Hash, vm.Current))
            ModelState.AddModelError("Current", "Current password is incorrect.");
        if (ModelState.IsValid && vm.Current == vm.New)
            ModelState.AddModelError("New", "New password must be different from the current password.");
        if (!ModelState.IsValid) return View(vm);

        u.Hash = hp.HashPassword(vm.New);
        await db.SaveChangesAsync();

        email.Send(u.Email, "Your ExpertLink password was changed",
            EmailService.Template("Password changed", $"<p>Hi {WebUtility.HtmlEncode(u.Name)},</p><p>Your password was changed on {DateTime.Now:d MMM yyyy, h:mm tt}. If this was not you, please reset your password immediately.</p>"));

        TempData["Info"] = "Password changed successfully.";
        return RedirectToAction("Profile");
    }

    // =======================================================================
    // Profile (Admin & Client; consultants have an extended profile page)
    // =======================================================================
    [Authorize]
    public async Task<IActionResult> Profile()
    {
        if (User.IsInRole("Consultant")) return RedirectToAction("Profile", "Consultant");
        var u = await db.Users.FindAsync(User.GetUserId());
        if (u == null) return RedirectToAction("Login");

        return View(new ProfileVM { Email = u.Email, Role = u.Role, Name = u.Name, Phone = u.Phone ?? "", PhotoURL = u.PhotoURL });
    }

    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileVM vm)
    {
        var u = await db.Users.FindAsync(User.GetUserId());
        if (u == null) return RedirectToAction("Login");

        if (vm.Photo != null)
        {
            var err = hp.ValidatePhoto(vm.Photo);
            if (err != "") ModelState.AddModelError("Photo", err);
        }
        if (!ModelState.IsValid)
        {
            vm.Email = u.Email;
            vm.Role = u.Role;
            vm.PhotoURL = u.PhotoURL;
            return View(vm);
        }

        u.Name = vm.Name.Trim();
        u.Phone = vm.Phone;
        if (vm.Photo != null)
        {
            hp.DeletePhoto(u.PhotoURL);
            u.PhotoURL = hp.SavePhoto(vm.Photo, "users");
        }
        else if (vm.RemovePhoto)
        {
            hp.DeletePhoto(u.PhotoURL);
            u.PhotoURL = null;
        }
        await db.SaveChangesAsync();
        await hp.RefreshSignIn(u);

        TempData["Info"] = "Profile updated.";
        return RedirectToAction("Profile");
    }
}
