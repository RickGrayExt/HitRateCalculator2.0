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
        // Create simple racks
        var racks = new List<Rack>();
        int total = Math.Max(1, ctx.Message.TotalRacks);
        for (int i=1;i<=total;i++)
            racks.Add(new Rack($"R{i}", LevelCount: 5, SlotPerLevel: 20, MaxWeightKg: 500));
        await ctx.Publish(new RackLayoutCalculated(ctx.Message.RunId, racks));
    }
}
