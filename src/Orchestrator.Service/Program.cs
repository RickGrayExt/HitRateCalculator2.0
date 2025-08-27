using System;  
using System.Threading.Tasks;
using Contracts;
using MassTransit;
using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<RunStore>();
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<ResultConsumer>();
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["Rabbit:Host"] ?? "rabbitmq", "/", h => { });
        cfg.ConfigureEndpoints(ctx);
    });
});
var app = builder.Build();

app.MapPost("/runs", async (IPublishEndpoint bus, RunRequest req) =>
{
    var runId = Guid.NewGuid();
    var p = new RunParams(
        req.UsePickToLine, req.StationCapacity, req.NumberOfStations, req.WaveSize,
        req.EnableSeasonality, req.SkusPerRack, req.MaxOrdersPerBatch, req.MaxStationsOpen
    );
    await bus.Publish(new StartRunCommand(runId, req.DataUrl, p));
    return Results.Accepted($"/runs/{runId}", new { runId, status = "Started" });
});

app.MapGet("/runs/{id}", (RunStore store, Guid id) =>
{
    if (store.Results.TryGetValue(id, out var res)) return Results.Ok(res);
    return Results.Ok(new { status = "Running" });
});

app.Run();

record RunRequest(string DataUrl, bool UsePickToLine, int StationCapacity, int NumberOfStations, int WaveSize, bool EnableSeasonality, int SkusPerRack, int MaxOrdersPerBatch, int MaxStationsOpen);

class RunStore { public ConcurrentDictionary<Guid, HitRateCalculated> Results { get; } = new(); }

class ResultConsumer : IConsumer<HitRateCalculated>
{
    private readonly RunStore _store;
    public ResultConsumer(RunStore store) => _store = store;
    public Task Consume(ConsumeContext<HitRateCalculated> ctx)
    {
        _store.Results[ctx.Message.RunId] = ctx.Message;
        return Task.CompletedTask;
    }
}
