using Contracts;
using MassTransit;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["Rabbit:Host"] ?? "rabbitmq", "/", h => { });
    });
});
var app = builder.Build();

// in-memory run status
var runs = new Dictionary<Guid, string>();
var results = new Dictionary<Guid, HitRateResult>();

app.MapPost("/runs", async (IPublishEndpoint bus, StartRequest req) =>
{
    var runId = Guid.NewGuid();
    var config = new RunConfig(req.Mode, req.OrdersPerBatch, req.MaxLinesPerBatch, req.StationCount,
                               req.MaxSkusPerRack, req.SlotsPerRack, req.LevelsPerRack, req.MaxWeightPerRackKg);
    runs[runId] = "Started";
    await bus.Publish(new StartRunCommand(runId, req.DatasetPath, config));
    return Results.Accepted($"/runs/{runId}", new { runId, status = runs[runId]});
});

app.MapGet("/runs/{runId:guid}", (Guid runId) =>
{
    return runs.TryGetValue(runId, out var s) ? Results.Ok(new { runId, status = s }) : Results.NotFound();
});

app.MapGet("/results/{runId:guid}", (Guid runId) =>
{
    return results.TryGetValue(runId, out var r) ? Results.Ok(r) : Results.NotFound();
});

app.MapPost("/_internal/complete", (CompletePayload p) =>
{
    runs[p.RunId] = "Completed";
    results[p.RunId] = p.Result;
    return Results.Ok();
});

app.Run();

record StartRequest(string DatasetPath, string Mode, int OrdersPerBatch, int MaxLinesPerBatch, int StationCount,
                    int MaxSkusPerRack, int SlotsPerRack, int LevelsPerRack, double MaxWeightPerRackKg);
record CompletePayload(Guid RunId, HitRateResult Result);