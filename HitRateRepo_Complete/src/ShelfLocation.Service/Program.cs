using Contracts;
using MassTransit;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<SkuGroupsConsumer>();
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["Rabbit:Host"] ?? "rabbitmq", "/", h => { });
        cfg.ConfigureEndpoints(ctx);
    });
});
var app = builder.Build();
app.Run();

class SkuGroupsConsumer : IConsumer<SkuGroupsCreated>
{
    public async Task Consume(ConsumeContext<SkuGroupsCreated> ctx)
    {
        var demand = ctx.Message.Demand.OrderByDescending(d=>d.Velocity).ToList();
        var cfg = ctx.Message.Config;
        var locations = new List<ShelfLocation>();
        for (int i=0;i<demand.Count;i++)
        {
            var d = demand[i];
            int rackIndex = (i / cfg.MaxSkusPerRack) + 1;
            int slotIndex = (i % cfg.MaxSkusPerRack) + 1;
            locations.Add(new ShelfLocation(d.SkuId, $"R{rackIndex}", $"S{slotIndex}", i+1));
        }
        await ctx.Publish(new ShelfLocationsAssigned(ctx.Message.RunId, cfg, locations, demand, ctx.Message.Records));
    }
}