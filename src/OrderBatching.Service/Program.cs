using Contracts;
using MassTransit;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<RackLayoutConsumer>();
    x.AddConsumer<StartRunConsumerEcho>();
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["Rabbit:Host"] ?? "rabbitmq", "/", h => { });
        cfg.ConfigureEndpoints(ctx);
    });
});

var app = builder.Build();
app.Run();

static class RunParamsCache { public static Dictionary<Guid, RunParams> Cache { get; } = new(); }

class StartRunConsumerEcho : IConsumer<StartRunCommand>
{
    public Task Consume(ConsumeContext<StartRunCommand> ctx) { RunParamsCache.Cache[ctx.Message.RunId]=ctx.Message.Params; return Task.CompletedTask; }
}

class RackLayoutConsumer : IConsumer<RackLayoutCalculated>
{
    public async Task Consume(ConsumeContext<RackLayoutCalculated> ctx)
    {
        RunParamsCache.Cache.TryGetValue(ctx.Message.RunId, out var p);
        int maxOrders = p?.MaxOrdersPerBatch ?? 10;
        bool ptl = p?.UsePickToLine ?? false;

        // Fake-batch using rack count as a proxy for SKU spread (no access to order lines here)
        var batches = new List<Batch>();
        int batchCount = Math.Max(1, (ctx.Message.Racks.Count + 1) / 2);
        for (int i=0;i<batchCount;i++)
        {
            var lines = new List<OrderLine>();
            for (int j=0;j<maxOrders;j++)
                lines.Add(new OrderLine($"O{i}-{j}", $"SKU{j}", 1));
            batches.Add(new Batch($"B{i+1}", ptl ? "PTL" : "PTO", lines));
        }
        await ctx.Publish(new BatchesCreated(ctx.Message.RunId, batches, ptl ? "PTL" : "PTO"));
    }
}
