var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Scoped-сервис: один экземпляр REPL-сессии на SignalR-соединение
builder.Services.AddScoped<AiFrameworkDemo.Modules.SolversMath.SolversMathRunner>();

var app = builder.Build();

// Инициализируем путь к Docs/Tutorials от ContentRootPath приложения
AiFrameworkDemo.Core.TheoryLoader.Configure(app.Environment.ContentRootPath);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        var ct = context.Response.ContentType ?? "";
        if (ct.StartsWith("text/html", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Headers["Cache-Control"] = "no-store, no-cache, max-age=0";
            context.Response.Headers["Pragma"]        = "no-cache";
        }
        return Task.CompletedTask;
    });
    await next();
});

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
        Path.Combine(builder.Environment.ContentRootPath, "AIFrameworkUIKit")),
    RequestPath = "/AIFrameworkUIKit",
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers["Cache-Control"] = "no-cache, must-revalidate";
        ctx.Context.Response.Headers["Pragma"]        = "no-cache";
    }
});

app.MapStaticAssets();
app.MapRazorComponents<AiFrameworkDemo.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
