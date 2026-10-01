using IntelligentProgrammingPlatform.Data;
using IntelligentProgrammingPlatform.Models;
using IntelligentProgrammingPlatform.Services.CodeExecution;
using IntelligentProgrammingPlatform.Services.Submissions;
using IntelligentProgrammingPlatform.Services.Leaderboards;
using IntelligentProgrammingPlatform.Services.Progress;
using IntelligentProgrammingPlatform.Services.AI;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// MVC беттерін және барлық өзгертетін сұраулардың CSRF қорғанысын тіркейді.
builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
// Қауіпсіздік және уақытша хабар cookie-лерін тек HTTPS арқылы жібереді.
builder.Services.AddAntiforgery(options => options.Cookie.SecurePolicy = CookieSecurePolicy.Always);
builder.Services.Configure<Microsoft.AspNetCore.Mvc.CookieTempDataProviderOptions>(options =>
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always);
// EF Core контекстін қолданыстағы SQL Server базасына қосады.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.")));

// Identity пароль, email және рөл қауіпсіздігін баптайды.
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// MVC кіру cookie-сінің жолдары мен қолданылу мерзімін анықтайды.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
});

// Docker клиенті мен ортақ семафор барлық сұрауда бір орындау шегін сақтайды.
builder.Services.AddSingleton<DockerCli>();
builder.Services.AddSingleton<DockerCodeRunner>();
builder.Services.AddSingleton<SubmissionExecutionGate>();
builder.Services.AddScoped<SubmissionService>();
builder.Services.AddScoped<TaskPageService>();

// Прогресті тарихтан есептеп, рейтинг summary жаңартуларын реттейді.
builder.Services.AddScoped<ProgressService>();
builder.Services.AddSingleton<LeaderboardUpdateGate>();
builder.Services.AddScoped<LeaderboardService>();

// API кілтін конфигурациядан алып, AI шақыруын студенттің жеке әрекетіне ғана қосады.
builder.Services.Configure<AiTutorOptions>(builder.Configuration.GetSection("OpenAI"));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<AiRequestGate>();
builder.Services.AddSingleton<IAiFeedbackClient, OpenAiFeedbackClient>();
builder.Services.AddScoped<OpenAiTutorService>();

var app = builder.Build();

// HTTP сұрауларын HTTPS және қауіпсіз қате өңдеу middleware-лері арқылы өткізеді.
app.UseExceptionHandler("/Home/Error");
if (!app.Environment.IsDevelopment()) app.UseHsts();

// Браузер мүмкіндіктерін шектеп, Monaco-ға кедергі келтірмейтін жауап тақырыптарын қосады.
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    await next();
});
// Бос қате жауаптарын бастапқы HTTP күйін сақтайтын қауіпсіз бетке қайта орындайды.
app.UseStatusCodePagesWithReExecute("/Home/HttpError", "?code={0}");

app.UseHttpsRedirection();
app.UseRouting();

// Алдымен пайдаланушыны таниды, кейін оның рұқсаттарын тексереді.
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

// Admin аймағының және жалпы MVC беттерінің маршруттарын тіркейді.
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


// Рөлдерді, жергілікті әкімшіні және даму деректерін қайталамай дайындайды.
await DbSeeder.SeedAsync(app.Services, app.Configuration, app.Environment);

app.Run();
