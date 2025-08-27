using Contracts;
using MassTransit;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<SalesPatternsConsumer>();
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["Rabbit:Host"] ?? "rabbitmq", "/", h => { });
        cfg.ConfigureEndpoints(ctx);
    });
});

var app = builder.Build();
app.Run();

class SalesPatternsConsumer : IConsumer<SalesPatternsIdentified>
{
    public async Task Consume(ConsumeContext<SalesPatternsIdentified> ctx)
    {
        // group by category first; within category, 3 velocity tiers
        var groups = new List<SkuGroup>();
        foreach (var byCat in ctx.Message.Demand.GroupBy(d => d.Category))
        {
            var ordered = byCat.OrderByDescending(d => d.Velocity).ToList();
            int n = ordered.Count;
            int tierSize = Math.Max(1, n/3);
            var g1 = ordered.Take(tierSize).Select(x=>x.SkuId).ToList();
            var g2 = ordered.Skip(tierSize).Take(tierSize).Select(x=>x.SkuId).ToList();
            var g3 = ordered.Skip(2*tierSize).Select(x=>x.SkuId).ToList();
            groups.Add(new SkuGroup($"{byCat.Key}-fast", g1));
            groups.Add(new SkuGroup($"{byCat.Key}-mid", g2));
            groups.Add(new SkuGroup($"{byCat.Key}-slow", g3));
        }
        await ctx.Publish(new SkuGroupsCreated(ctx.Message.RunId, groups));
    }
}
