using Contracts;
using MassTransit;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<BatchesCreatedConsumer>();
    x.AddConsumer<StartRunConsumerEcho>();
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["Rabbit:Host"] ?? "rabbitmq", "/", h => { });
        cfg.ConfigureEndpoints(ctx);
    });
});

var app = builder.Build();
app.Run();

static class ParamCache { public static Dictionary<Guid, RunParams> Cache { get; } = new(); }
class StartRunConsumerEcho : IConsumer<StartRunCommand>
{
    public Task Consume(ConsumeContext<StartRunCommand> ctx) { ParamCache.Cache[ctx.Message.RunId]=ctx.Message.Params; return Task.CompletedTask; }
}

class BatchesCreatedConsumer : IConsumer<BatchesCreated>
{
    public async Task Consume(ConsumeContext<BatchesCreated> ctx)
    {
        ParamCache.Cache.TryGetValue(ctx.Message.RunId, out var p);
        int stations = p?.NumberOfStations ?? 5;
        var assignments = new List<StationAssignment>();
        for (int i=0;i<stations;i++) assignments.Add(new StationAssignment($"S{i+1}", new()));
        int idx = 0;
        foreach (var b in ctx.Message.Batches)
        {
            assignments[idx % stations].BatchIds.Add(b.BatchId);
            idx++;
        }
        await ctx.Publish(new StationsAllocated(ctx.Message.RunId, assignments));
    }
}
