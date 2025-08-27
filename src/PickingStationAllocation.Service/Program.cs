using Contracts;
using MassTransit;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<BatchesCreatedConsumer>();
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["Rabbit:Host"] ?? "rabbitmq", "/", h => { });
        cfg.ConfigureEndpoints(ctx);
    });
});
var app = builder.Build();
app.Run();

class BatchesCreatedConsumer : IConsumer<BatchesCreated>
{
    public async Task Consume(ConsumeContext<BatchesCreated> ctx)
    {
        var cfg = ctx.Message.Config;
        var stations = Enumerable.Range(1, cfg.StationCount).Select(i => $"S{i}").ToList();
        var assignments = stations.ToDictionary(s=>s, s=> new List<string>());
        int i=0;
        foreach (var b in ctx.Message.Batches)
        {
            var s = stations[i % stations.Count];
            assignments[s].Add(b.BatchId);
            i++;
        }
        var result = assignments.Select(kv => new StationAssignment(kv.Key, kv.Value)).ToList();
        await ctx.Publish(new StationsAllocated(ctx.Message.RunId, cfg, result, ctx.Message.Batches, ctx.Message.Racks, ctx.Message.Locations, ctx.Message.Demand, ctx.Message.Records));
    }
}