using SmallMind.Showcase.Web.Components;
using SmallMind.Showcase.Core.Interfaces;
using SmallMind.Showcase.Core.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure SmallMind paths
var modelsPath = builder.Configuration["SmallMind:ModelsPath"] ?? "./models";
var dataPath = builder.Configuration["SmallMind:DataPath"] ?? "./.data";

// Ensure directories exist
Directory.CreateDirectory(modelsPath);
Directory.CreateDirectory(dataPath);

// Register SmallMind Showcase services
builder.Services.AddSingleton<IModelRegistry>(sp => new ModelRegistry(modelsPath));
builder.Services.AddSingleton<IChatSessionStore>(sp => new JsonChatSessionStore(dataPath));
builder.Services.AddSingleton<IMetricsCollector, MetricsCollector>();
builder.Services.AddSingleton<IChatOrchestrator, ChatOrchestrator>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
