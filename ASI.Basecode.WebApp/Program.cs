using System;
using System.Linq;
using System.Security.Claims;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Services;
using ASI.Basecode.WebApp.MappingConfiguration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// One school per deployment: its name and the default member password.
builder.Services.Configure<ApilanSettings>(builder.Configuration.GetSection(ApilanSettings.Section));

builder.Services.AddDbContext<AllianceDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

/* Squeeze stylesheets, scripts and icons before they go out, which matters
   for phones on the venue Wi-Fi. Pages themselves are left alone: they carry
   the anti-forgery token, and compressing secrets alongside user input leaks them. */
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.MimeTypes = new[] { "text/css", "text/javascript", "application/javascript", "image/svg+xml" };
});

// AutoMapper — the services depend on IMapper.
builder.Services.AddAutoMapper(cfg => cfg.AddProfile<MappingProfile>());

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<IEventRepository, EventRepository>();
builder.Services.AddScoped<IEventTypeRepository, EventTypeRepository>();
builder.Services.AddScoped<IAttendanceRepository, AttendanceRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();

builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<IEventTypeService, EventTypeService>();
builder.Services.AddScoped<IAttendanceService, AttendanceService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IAuditService, AuditService>();

/* Cookie sign-in. The whole console lives behind the Officer policy; a member
   account only ever reaches its own attendance page. */
builder.Services
    .AddAuthentication(Const.AuthenticationScheme)
    .AddCookie(Const.AuthenticationScheme, options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Denied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;

        /* The cookie remembers the role it was issued with. Check the account
           on every request instead, so making someone an officer (or a member
           again), or disabling them, applies on their next page, not hours later. */
        options.Events.OnValidatePrincipal = async context =>
        {
            var idClaim = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(idClaim, out var accountId))
            {
                return;
            }

            var accounts = context.HttpContext.RequestServices.GetRequiredService<IAccountService>();
            var state = accounts.CurrentState(accountId);

            if (state == null)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(Const.AuthenticationScheme);
                return;
            }

            var (role, name) = state.Value;
            if (!context.Principal.IsInRole(role) || context.Principal.FindFirstValue(Const.ClaimDisplayName) != name)
            {
                var claims = context.Principal.Claims
                    .Where(c => c.Type != ClaimTypes.Role && c.Type != Const.ClaimDisplayName)
                    .Append(new Claim(ClaimTypes.Role, role))
                    .Append(new Claim(Const.ClaimDisplayName, name));
                context.ReplacePrincipal(new ClaimsPrincipal(
                    new ClaimsIdentity(claims, Const.AuthenticationScheme, ClaimTypes.Name, ClaimTypes.Role)));
                context.ShouldRenew = true;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Const.OfficerOnlyPolicy, policy =>
        policy.RequireRole(Const.RoleOfficer));
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var types = scope.ServiceProvider.GetRequiredService<IEventTypeService>().GetEventTypes();
    ASI.Basecode.WebApp.Helpers.Ui.SetChosenTones(types.Select(t => (t.TypeName, t.Color)));
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseResponseCompression();

/* Let the browser keep static files instead of fetching them on every page.
   Files linked with asp-append-version carry ?v=<hash>, which changes whenever
   the file does, so they can be kept for a year. The vendored libraries and
   fonts never change, so a month is safe. Anything else keeps the default. */
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var request = ctx.Context.Request;
        if (request.Query.ContainsKey("v"))
        {
            ctx.Context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        }
        else if (request.Path.StartsWithSegments("/lib"))
        {
            ctx.Context.Response.Headers.CacheControl = "public, max-age=2592000";
        }
    }
});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// The participation report used to live at /Reports; old links still land.
app.MapGet("/Reports/{action?}", (HttpContext ctx, string action) =>
    Results.Redirect("/participation"
        + (string.Equals(action, "Export", StringComparison.OrdinalIgnoreCase) ? "/export" : "")
        + ctx.Request.QueryString, permanent: true));

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
