using Microsoft.Extensions.DependencyInjection;

// Setup ConsoleAppFramework
var builder = ConsoleApp.CreateBuilder(args);

// Register DI
builder.ConfigureServices((services) => {
    services.AddScoped<IAuthentication, Auth>();
});

// Create app
var app = builder.Build();

await app.AddCommands<Account>().RunAsync();
