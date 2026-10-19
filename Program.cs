using System.Text.Json.Serialization;
using SmartX.Part2;
using SmartX.Part2.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<DeviceRegistry>();
builder.Services.AddSingleton<AlertTracker>();
builder.Services.AddSingleton<PredictionEngine>();
builder.Services.AddSingleton<TelemetryProcessor>();
builder.Services.AddSingleton<CommandService>();
builder.Services.AddSingleton<InsightService>();
builder.Services.AddHostedService<TelemetryWorker>();

var app = builder.Build();

if (app.Configuration.GetValue("Seed:Enabled", true))
    SeedData.Apply(app.Services);

app.UseDefaultFiles();
app.UseStaticFiles();

ApiEndpoints.Map(app);

app.Run();

public partial class Program { }
