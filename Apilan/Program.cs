using Apilan.Data;
using Apilan.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// Slot, waitlist, and check-in code rules — shared by the officer console and
// the public calendar so the two can never disagree.
builder.Services.AddScoped<Apilan.Services.RegistrationService>();

// Which organization a screen is speaking for — the console masthead and the
// org line printed on a registration stub.
builder.Services.AddScoped<Apilan.Services.OrgContext>();

builder.Services.AddDbContext<ApilanDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        // Sensible for a student org: real passwords, no email round-trip.
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = true;
        options.Password.RequireDigit = true;

        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = false;

        options.Lockout.MaxFailedAccessAttempts = 8;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
    })
    .AddEntityFrameworkStores<ApilanDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/Denied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Cookie.Name = "Apilan.Auth";
});

builder.Services.AddAuthorization(options =>
{
    // One staff policy, because there is one staff role. Everything an officer
    // cannot do is something no signed-in account can do.
    options.AddPolicy("StaffOnly", p => p.RequireRole(Roles.Officer));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Roles, the first admin account, and (in development) demo records.
await DbSeeder.SeedAsync(app.Services, app.Configuration);

app.Run();
