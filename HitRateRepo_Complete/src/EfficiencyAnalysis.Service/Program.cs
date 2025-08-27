using Contracts;
using MassTransit;
using System.Net.Http.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<StationsAllocatedConsumer>();
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["Rabbit:Host"] ?? "rabbitmq", "/", h => { });
        cfg.ConfigureEndpoints(ctx);
    });
});
var app = builder.Build();
app.Run();

class StationsAllocatedConsumer : IConsumer<StationsAllocated>
{
    public async Task Consume(ConsumeContext<StationsAllocated> ctx)
    {
        var batches = ctx.Message.Batches;
        int totalItems = batches.Sum(b => b.Lines.Sum(l => l.Qty));
        // presentations = count distinct rack visits per batch
        int presentations = 0;
        var byRackStats = new Dictionary<string, (int items, int visits)>();
        foreach (var b in batches)
        {
            var racksInBatch = b.Lines.Select(l => l.RackId).Distinct().ToList();
            presentations += racksInBatch.Count;
            foreach (var r in racksInBatch)
            {
                int itemsInRack = b.Lines.Where(l=>l.RackId==r).Sum(l=>l.Qty);
                if (!byRackStats.ContainsKey(r)) byRackStats[r] = (0,0);
                var (it, vi) = byRackStats[r];
                byRackStats[r] = (it + itemsInRack, vi + 1);
            }
        }
        double hitRate = presentations==0 ? 0 : (double)totalItems / presentations;
        var byRack = byRackStats.ToDictionary(kv => kv.Key, kv => kv.Value.visits==0?0.0: (double)kv.Value.items / kv.Value.visits);
        var result = new HitRateResult(ctx.Message.Config.Mode, Math.Round(hitRate,4), totalItems, presentations, byRack);

        // notify orchestrator to store result
        using var http = new HttpClient();
        var payload = new { RunId = ctx.Message.RunId, Result = result };
        await http.PostAsJsonAsync("http://orchestrator:8080/_internal/complete", payload);
        await ctx.Publish(new HitRateCalculated(ctx.Message.RunId, ctx.Message.Config, result));
    }
}