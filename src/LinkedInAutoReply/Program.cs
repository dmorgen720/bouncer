using System.Security.Claims;
using DotNetEnv;
using LinkedInAutoReply.Components;
using LinkedInAutoReply.Data;
using LinkedInAutoReply.Models;
using LinkedInAutoReply.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using MudBlazor.Services;
using OllamaSharp;
using OpenAI;

// Load .env file if present (development convenience — never commit .env to source control)
Env.Load(Path.Combine(Directory.GetCurrentDirectory(), ".env"));

var builder = WebApplication.CreateBuilder(args);

// ── Settings ────────────────────────────────────────────────────────────────
var graphSettings = builder.Configuration.GetSection("Graph").Get<GraphSettings>()
    ?? throw new InvalidOperationException("Missing configuration section 'Graph'.");
var aiSettings = builder.Configuration.GetSection("AI").Get<AISettings>()
    ?? throw new InvalidOperationException("Missing configuration section 'AI'.");
var autoReplySettings = builder.Configuration.GetSection("AutoReply").Get<AutoReplySettings>()
    ?? throw new InvalidOperationException("Missing configuration section 'AutoReply'.");

if (string.IsNullOrWhiteSpace(graphSettings.TenantId))
    throw new InvalidOperationException("Graph:TenantId is required.");
if (string.IsNullOrWhiteSpace(graphSettings.ClientId))
    throw new InvalidOperationException("Graph:ClientId is required.");
if (string.IsNullOrWhiteSpace(graphSettings.ClientSecret))
    throw new InvalidOperationException("Graph:ClientSecret is required. Set it via .env or an environment variable.");
if (string.IsNullOrWhiteSpace(graphSettings.UserId))
    throw new InvalidOperationException("Graph:UserId is required.");

var uiPassword = builder.Configuration["Bouncer:Password"];
if (string.IsNullOrWhiteSpace(uiPassword))
    throw new InvalidOperationException("Bouncer:Password is required. Set it via .env (Bouncer__Password=...).");

builder.Services.AddSingleton(graphSettings);
builder.Services.AddSingleton(aiSettings);
builder.Services.AddSingleton(autoReplySettings);

// ── EF Core — SQLite ─────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── AI — Microsoft.Extensions.AI (IChatClient) ──────────────────────────────
IChatClient chatClient = aiSettings.Provider.Equals("Ollama", StringComparison.OrdinalIgnoreCase)
    ? new OllamaApiClient(new Uri(aiSettings.Ollama.Endpoint), aiSettings.Ollama.ModelId)
    : new OpenAIClient(aiSettings.OpenAI.ApiKey)
        .GetChatClient(aiSettings.OpenAI.ModelId)
        .AsIChatClient();

builder.Services.AddSingleton(chatClient);

// ── Authentication — cookie auth with password from .env ─────────────────────
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/login";
        o.LogoutPath = "/logout";
        o.ExpireTimeSpan = TimeSpan.FromDays(7);
        o.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// ── Application services ─────────────────────────────────────────────────────
builder.Services.AddSingleton<GraphMailService>();
builder.Services.AddSingleton<GraphDraftService>();
builder.Services.AddSingleton<JobOfferClassifier>();
builder.Services.AddSingleton<AttachmentTextExtractor>();
builder.Services.AddSingleton<LinkedInMessageParser>();
builder.Services.AddSingleton<RecruitmentAssessor>();
builder.Services.AddSingleton<MergeService>();
builder.Services.AddSingleton<ScanTriggerService>();
builder.Services.AddHostedService<MailWorker>();

// ── MudBlazor + Blazor ───────────────────────────────────────────────────────
builder.Services.AddMudServices();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// ── Auto-migrate database on startup ─────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapStaticAssets();

// ── Login / Logout endpoints ──────────────────────────────────────────────────
app.MapPost("/do-login", async (HttpContext ctx, IConfiguration config) =>
{
    var form = await ctx.Request.ReadFormAsync();
    var password = form["password"].ToString();
    var configured = config["Bouncer:Password"] ?? string.Empty;

    if (!string.Equals(password, configured, StringComparison.Ordinal))
    {
        ctx.Response.Redirect("/login?error=1");
        return;
    }

    var claims = new[] { new Claim(ClaimTypes.Name, "admin") };
    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    ctx.Response.Redirect("/");
});

app.MapGet("/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    ctx.Response.Redirect("/login");
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
