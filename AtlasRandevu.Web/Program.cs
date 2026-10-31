
using AtlasRandevu.Application.Interfaces;
using AtlasRandevu.Infrastructure.Persistence;
using AtlasRandevu.Infrastructure.Services;
using AtlasRandevu.Web;
using AtlasRandevu.Web.Hubs;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);


// MVC ve SignalR
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

// SQL Server
builder.Services.AddDbContext<AtlasRandevuDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// ASP.NET Core Identity
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;

    options.User.RequireUniqueEmail = true;

    options.Lockout.DefaultLockoutTimeSpan =
        TimeSpan.FromMinutes(15);

    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
})
.AddEntityFrameworkStores<AtlasRandevuDbContext>()
.AddDefaultTokenProviders();

// Oturum çerezi
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Hesap/Giris";
    options.AccessDeniedPath = "/Hesap/Yetkisiz";

    options.Cookie.Name = "AtlasRandevu.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.IsEssential = true;

    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.SlidingExpiration = false;

    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api") ||
            context.Request.Headers.XRequestedWith == "XMLHttpRequest")
        {
            context.Response.StatusCode =
                StatusCodes.Status401Unauthorized;

            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };

    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api") ||
            context.Request.Headers.XRequestedWith == "XMLHttpRequest")
        {
            context.Response.StatusCode =
                StatusCodes.Status403Forbidden;

            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});

// Yetkilendirme
builder.Services.AddAuthorization(options =>
{
    // Platform yöneticisi
    options.AddPolicy("PlatformYoneticisi", policy =>
        policy.RequireAuthenticatedUser()
              .RequireRole("PlatformAdmin"));

    // Müþteri
    options.AddPolicy("MusteriYetkisi", policy =>
        policy.RequireAuthenticatedUser()
              .RequireRole("Musteri")
              .RequireAssertion(context =>
                  !context.User.IsInRole("PlatformAdmin") &&
                  !context.User.IsInRole("IsletmeSahibi")));

    // Onaylý iþletme sahibi
    options.AddPolicy("OnayliIsletmeSahibi", policy =>
        policy.RequireAuthenticatedUser()
              .RequireAssertion(context =>
                  !context.User.IsInRole("PlatformAdmin") &&
                  !context.User.IsInRole("Musteri"))
              .AddRequirements(
                  new OnayliIsletmeSahibiRequirement()));
});

// Ýþletme yetki kontrolü
builder.Services.AddScoped<
    Microsoft.AspNetCore.Authorization.IAuthorizationHandler,
    OnayliIsletmeSahibiHandler>();

// SMS servisi
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddScoped<
        ISmsService,
        DevelopmentSmsService>();
}
else
{
    builder.Services.AddScoped<
        ISmsService,
        SmsService>();
}

// Uygulama servisleri
builder.Services.AddScoped<TelefonDogrulamaService>();
builder.Services.AddScoped<VergiLevhasiDosyaService>();
builder.Services.AddScoped<WebPushService>();

// E-posta servisi
builder.Services.AddTransient<
    Microsoft.AspNetCore.Identity.UI.Services.IEmailSender,
    AtlasRandevu.Web.Services.GmailEmailSender>();

// Rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;

    options.AddPolicy("sms", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

// Session
builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(15);

    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

var app = builder.Build();

// Production hata yönetimi
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Middleware
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseRateLimiter();
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

// Controller rotalarý
app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Kesfet}/{action=Index}/{id?}");

// SignalR
app.MapHub<IsletmeBildirimHub>(
    "/hubs/isletme-bildirim");

// Gerekli Identity rollerini oluþtur
using (var scope = app.Services.CreateScope())
{
    var roleManager =
        scope.ServiceProvider
            .GetRequiredService<RoleManager<IdentityRole>>();

    foreach (var rol in new[]
    {
        "PlatformAdmin",
        "IsletmeSahibi",
        "Musteri"
    })
    {
        if (!await roleManager.RoleExistsAsync(rol))
        {
            var sonuc = await roleManager.CreateAsync(
                new IdentityRole(rol));

            if (!sonuc.Succeeded)
            {
                throw new Exception(
                    $"{rol} rolü oluþturulamadý: " +
                    string.Join(", ",
                        sonuc.Errors.Select(x => x.Description)));
            }
        }
    }
}

// Platform yöneticisi ilk kurulum
if (app.Configuration.GetValue<bool>("PlatformAdminSetup:Enabled"))
{
    using var scope = app.Services.CreateScope();

    var userManager = scope.ServiceProvider
        .GetRequiredService<UserManager<IdentityUser>>();

    var email = app.Configuration["PlatformAdminSetup:Email"];
    var password = app.Configuration["PlatformAdminSetup:Password"];

    if (string.IsNullOrWhiteSpace(email) ||
        string.IsNullOrWhiteSpace(password))
    {
        throw new Exception(
            "Platform yöneticisi kurulum bilgileri eksik.");
    }

    var admin = await userManager.FindByEmailAsync(email);

    if (admin == null)
    {
        admin = new IdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(admin, password);

        if (!result.Succeeded)
        {
            throw new Exception(
                "Platform yöneticisi oluþturulamadý: " +
                string.Join(", ",
                    result.Errors.Select(x => x.Description)));
        }
    }

    if (!await userManager.IsInRoleAsync(admin, "PlatformAdmin"))
    {
        var result = await userManager.AddToRoleAsync(
            admin, "PlatformAdmin");

        if (!result.Succeeded)
        {
            throw new Exception(
                "Platform yöneticisi rolü atanamadý: " +
                string.Join(", ",
                    result.Errors.Select(x => x.Description)));
        }
    }
}

app.Run();

// Ýþletme sahibi yetki gereksinimi
public sealed class OnayliIsletmeSahibiRequirement :
    Microsoft.AspNetCore.Authorization.IAuthorizationRequirement
{
}

// Ýþletme sahibi yetki kontrolü
public sealed class OnayliIsletmeSahibiHandler :
    Microsoft.AspNetCore.Authorization.AuthorizationHandler<
        OnayliIsletmeSahibiRequirement>
{
    private readonly AtlasRandevuDbContext _db;

    public OnayliIsletmeSahibiHandler(
        AtlasRandevuDbContext db)
    {
        _db = db;
    }

    protected override async Task HandleRequirementAsync(
        Microsoft.AspNetCore.Authorization.AuthorizationHandlerContext context,
        OnayliIsletmeSahibiRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true ||
            context.User.IsInRole("PlatformAdmin") ||
            context.User.IsInRole("Musteri"))
        {
            return;
        }

        var userId = context.User.FindFirst(
            System.Security.Claims.ClaimTypes.NameIdentifier
        )?.Value;

        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        var yetkili = await _db.IsletmeKullanicilari
            .AsNoTracking()
            .AnyAsync(x =>
                x.KullaniciId == userId &&
                x.AktifMi &&
                x.Rol == "IsletmeSahibi" &&
                x.Isletme.Durum == "Onaylandi");

        if (yetkili)
        {
            context.Succeed(requirement);
        }
    }
}
