using Contracts;
using MassTransit;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<SkuGroupsConsumer>();
    x.AddConsumer<StartRunEcho>(); // to capture params if needed
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["Rabbit:Host"] ?? "rabbitmq", "/", h => { });
        cfg.ConfigureEndpoints(ctx);
    });
});

var app = builder.Build();
app.Run();

static class ParamCache { public static Dictionary<Guid, RunParams> Cache { get; } = new(); }

class StartRunEcho : IConsumer<StartRunCommand>
{
    public Task Consume(ConsumeContext<StartRunCommand> ctx)
    {
        ParamCache.Cache[ctx.Message.RunId] = ctx.Message.Params;
        return Task.CompletedTask;
    }
}

class SkuGroupsConsumer : IConsumer<SkuGroupsCreated>
{
    public async Task Consume(ConsumeContext<SkuGroupsCreated> ctx)
    {
        // Flatten SKUs from groups; rank by group order
        var skuIds = ctx.Message.Groups.SelectMany(g => g.SkuIds).Distinct().ToList();
        ParamCache.Cache.TryGetValue(ctx.Message.RunId, out var p);
        int perRack = p?.SkusPerRack ?? 12;
        int totalRacks = (int)Math.Ceiling((double)skuIds.Count / Math.Max(1, perRack));

        var locations = new List<ShelfLocation>();
        int rackIdx = 1; int slot = 1; int rank = 1;
        foreach (var sku in skuIds)
        {
            var rackId = $"R{rackIdx}";
            var slotId = $"S{slot}";
            locations.Add(new ShelfLocation(sku, rackId, slotId, rank++));
            slot++;
            if (slot > perRack) { slot = 1; rackIdx++; }
        }
        await ctx.Publish(new ShelfLocationsAssigned(ctx.Message.RunId, locations, totalRacks));
    }
}
