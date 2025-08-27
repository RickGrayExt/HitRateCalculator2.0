using Contracts;
using MassTransit;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<StationsAllocatedConsumer>();
    x.AddConsumer<ShelfEcho>();
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["Rabbit:Host"] ?? "rabbitmq", "/", h => { });
        cfg.ConfigureEndpoints(ctx);
    });
});

var app = builder.Build();
app.Run();

static class State { public static Dictionary<Guid,int> Racks = new(); public static Dictionary<Guid,RunParams> Params=new(); }
class ShelfEcho : IConsumer<ShelfLocationsAssigned>
{
    public Task Consume(ConsumeContext<ShelfLocationsAssigned> ctx) { State.Racks[ctx.Message.RunId]=ctx.Message.TotalRacks; return Task.CompletedTask; }
}

class StationsAllocatedConsumer : IConsumer<StationsAllocated>
{
    public async Task Consume(ConsumeContext<StationsAllocated> ctx)
    {
        int totalBatches = ctx.Message.Assignments.Sum(a => a.BatchIds.Count);
        int totalRacks = State.Racks.TryGetValue(ctx.Message.RunId, out var r) ? r : 1;
        // toy model: each batch causes one rack presentation per rack touched (~ half of racks)
        int rackPresentations = Math.Max(1, totalBatches * Math.Max(1,totalRacks/2));
        // items picked: assume 3 lines per batch * 2 qty avg
        int items = totalBatches * 3 * 2;
        double hitRate = rackPresentations == 0 ? 0 : (double)items / rackPresentations;
        var byRack = new Dictionary<string,double>();
        for (int i=1;i<=totalRacks;i++) byRack[$"R{i}"] = (double)items / Math.Max(1,totalRacks);
        await ctx.Publish(new HitRateCalculated(ctx.Message.RunId, new HitRateResult("PTO", hitRate, items, rackPresentations, byRack, totalRacks)));
    }
}
