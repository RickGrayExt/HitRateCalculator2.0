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
        var demand = ctx.Message.Demand;
        var n = demand.Count;
        var groups = new List<SkuGroup>();
        int aCut = (int)Math.Ceiling(n * 0.2);
        int bCut = (int)Math.Ceiling(n * 0.5);
        var a = demand.Take(aCut).Select(d=>d.SkuId).ToList();
        var b = demand.Skip(aCut).Take(bCut - aCut).Select(d=>d.SkuId).ToList();
        var c = demand.Skip(bCut).Select(d=>d.SkuId).ToList();
        if (a.Count>0) groups.Add(new SkuGroup("A", a));
        if (b.Count>0) groups.Add(new SkuGroup("B", b));
        if (c.Count>0) groups.Add(new SkuGroup("C", c));
        await ctx.Publish(new SkuGroupsCreated(ctx.Message.RunId, ctx.Message.Config, groups, demand, ctx.Message.Records));
    }
}