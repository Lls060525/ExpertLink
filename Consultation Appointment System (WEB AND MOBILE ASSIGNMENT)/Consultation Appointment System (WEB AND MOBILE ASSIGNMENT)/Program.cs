using ExpertLink.Data;
using ExpertLink.Hubs;
using ExpertLink.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Private settings (e.g. Gmail App Password) - this file is git-ignored
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// ---------------------------------------------------------------------------
// MVC + SignalR
// ---------------------------------------------------------------------------
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();
builder.Services.AddHttpContextAccessor();

// Behind a tunnel / proxy (e.g. Visual Studio Dev Tunnels): use the public host name
// so generated links (Stripe return URL, e-mail verification, password reset) work.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

// Session (used by captcha + failed login counter)
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(o =>
{
    o.IdleTimeout = TimeSpan.FromMinutes(20);
    o.Cookie.HttpOnly = true;
    o.Cookie.IsEssential = true;
});

// ---------------------------------------------------------------------------
// Data layer: EF Core + SQL Server Express (file-based .mdf in the project folder)
// ---------------------------------------------------------------------------
// The database name is made unique per project folder, so copies of the project
// (e.g. a teammate's clone) never clash inside LocalDB.
var dbName = "ExpertLink_" + Convert.ToHexString(
    System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(builder.Environment.ContentRootPath.ToLowerInvariant())))[..8];
var connectionString = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(
    builder.Configuration.GetConnectionString("DefaultConnection")!
        .Replace("|DataDirectory|", builder.Environment.ContentRootPath))
{
    InitialCatalog = dbName
}.ConnectionString;
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlServer(connectionString, sql => sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));

// ---------------------------------------------------------------------------
// Security: manual cookie-based authentication (NO ASP.NET Core Identity)
// ---------------------------------------------------------------------------
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Account/Login";
        o.LogoutPath = "/Account/Logout";
        o.AccessDeniedPath = "/Account/AccessDenied";
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
        o.Cookie.Name = "ExpertLink.Auth";
        o.Cookie.HttpOnly = true;
    });
builder.Services.AddAuthorization();

// ---------------------------------------------------------------------------
// Application services
// ---------------------------------------------------------------------------
builder.Services.AddScoped<Helper>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<SlotService>();
builder.Services.AddScoped<StripeService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddSingleton<CaptchaService>();

var app = builder.Build();

// Apply migrations and insert sample data (only if a migration has been created)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (db.Database.GetMigrations().Any())
    {
        db.Database.Migrate();
        var hp = scope.ServiceProvider.GetRequiredService<Helper>();
        DbSeeder.Seed(db, hp);
    }
}

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/Home/Status/{0}");

app.UseHttpsRedirection();
app.UseRequestLocalization("en-US");
app.UseStaticFiles();          // serves runtime-uploaded photos under wwwroot/uploads
app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();
app.MapHub<AppHub>("/hub");

app.Run();
