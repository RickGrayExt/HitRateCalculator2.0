using Contracts;
using MassTransit;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<ShelfLocationsConsumer>();
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["Rabbit:Host"] ?? "rabbitmq", "/", h => { });
        cfg.ConfigureEndpoints(ctx);
    });
});
var app = builder.Build();
app.Run();

class ShelfLocationsConsumer : IConsumer<ShelfLocationsAssigned>
{
    public async Task Consume(ConsumeContext<ShelfLocationsAssigned> ctx)
    {
        var cfg = ctx.Message.Config;
        var totalSkus = ctx.Message.Locations.Select(l => l.SkuId).Distinct().Count();
        int racksNeeded = (int)Math.Ceiling(totalSkus / (double)cfg.MaxSkusPerRack);
        var racks = Enumerable.Range(1, racksNeeded)
            .Select(i => new Rack($"R{i}", cfg.LevelsPerRack, cfg.SlotsPerRack, cfg.MaxWeightPerRackKg))
            .ToList();
        await ctx.Publish(new RackLayoutCalculated(ctx.Message.RunId, cfg, racks, ctx.Message.Locations, ctx.Message.Demand, ctx.Message.Records));
    }
}