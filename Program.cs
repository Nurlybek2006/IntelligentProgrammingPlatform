using IntelligentProgrammingPlatform.Data;
using IntelligentProgrammingPlatform.Models;
using IntelligentProgrammingPlatform.Services.CodeExecution;
using IntelligentProgrammingPlatform.Services.Submissions;
using IntelligentProgrammingPlatform.Services.Leaderboards;
using IntelligentProgrammingPlatform.Services.Progress;
using IntelligentProgrammingPlatform.Services.AI;
using IntelligentProgrammingPlatform.Services.Security;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IntelligentProgrammingPlatform;
using IntelligentProgrammingPlatform.Services.Localization;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Localization;

var builder = WebApplication.CreateBuilder(args);

// Әр жауапқа жеке CSP nonce беріп, бос не wildcard хост тізімін қабылдамайды.
builder.Services.AddScoped<ContentSecurityPolicy>();
builder.Services.AddOptions<HostFilteringOptions>()
    .Configure(options => { options.AllowEmptyHosts = false; options.IncludeFailureMessage = false; })
    .Validate(options => options.AllowedHosts.Count > 0 && options.AllowedHosts.All(host =>
        !string.IsNullOrWhiteSpace(host) && !host.Contains('*')),
        "AllowedHosts must contain explicit host names; empty lists and wildcards are not allowed.")
    .ValidateOnStart();

// Ресурстар мен culture cookie арқылы үш тілді, әдепкі қазақша интерфейсті тіркейді.
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.SetDefaultCulture(SupportedCultures.Default)
        .AddSupportedCultures(SupportedCultures.Names.ToArray())
        .AddSupportedUICultures(SupportedCultures.Names.ToArray());
    options.RequestCultureProviders = new[] { new CookieRequestCultureProvider() };
    options.ApplyCurrentCultureToResponseHeaders = true;
});

// MVC беттерін және барлық өзгертетін сұраулардың CSRF қорғанысын тіркейді.
builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()))
    .AddDataAnnotationsLocalization(options => options.DataAnnotationLocalizerProvider = (type, factory) =>
        factory.Create(type.Namespace?.Contains(".Admin", StringComparison.Ordinal) == true
            ? typeof(AdminResource) : typeof(SharedResource)));
// Түрге түрлендіру қателерін де ағымдағы сұрау тілімен көрсетеді.
builder.Services.AddOptions<MvcOptions>().Configure<IStringLocalizer<SharedResource>>((options, text) =>
{
    var messages = options.ModelBindingMessageProvider;
    messages.SetAttemptedValueIsInvalidAccessor((value, field) => text["Validation_InvalidValue", field]);
    messages.SetValueMustNotBeNullAccessor(value => text["Validation_RequiredValue"]);
    messages.SetValueIsInvalidAccessor(value => text["Validation_InvalidValue", value]);
    messages.SetMissingBindRequiredValueAccessor(field => text["Validation_Required", field]);
    messages.SetUnknownValueIsInvalidAccessor(field => text["Validation_InvalidValue", field]);
    messages.SetValueMustBeANumberAccessor(field => text["Validation_Number", field]);
    messages.SetNonPropertyAttemptedValueIsInvalidAccessor(value => text["Validation_InvalidValue", value]);
    messages.SetNonPropertyUnknownValueIsInvalidAccessor(() => text["Validation_RequiredValue"]);
    messages.SetNonPropertyValueMustBeANumberAccessor(() => text["Validation_Number", ""]);
});
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
builder.Services.AddScoped<CustomRunService>();
builder.Services.AddScoped<AttemptJourneyService>();
builder.Services.AddScoped<TaskPageService>();

// Прогресті тарихтан есептеп, рейтинг summary жаңартуларын реттейді.
builder.Services.AddScoped<ProgressService>();
builder.Services.AddScoped<LearningInsightsService>();
builder.Services.AddSingleton<LeaderboardUpdateGate>();
builder.Services.AddScoped<LeaderboardService>();

// API кілтін конфигурациядан алып, AI шақыруын студенттің жеке әрекетіне ғана қосады.
builder.Services.Configure<AiTutorOptions>(builder.Configuration.GetSection("OpenAI"));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<AiRequestGate>();
builder.Services.AddSingleton<IAiFeedbackClient, OpenAiFeedbackClient>();
builder.Services.AddScoped<OpenAiTutorService>();
builder.Services.AddScoped<HintRevealService>();

var app = builder.Build();

// Қате беттері де culture cookie таңдаған тілде көрсетіледі.
app.UseRequestLocalization();

// HTTP сұрауларын HTTPS және қауіпсіз қате өңдеу middleware-лері арқылы өткізеді.
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseExceptionHandler("/Home/Error");
if (!app.Environment.IsDevelopment()) app.UseHsts();

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
