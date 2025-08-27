using Contracts;
using MassTransit;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<RackLayoutConsumer>();
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["Rabbit:Host"] ?? "rabbitmq", "/", h => { });
        cfg.ConfigureEndpoints(ctx);
    });
});
var app = builder.Build();
app.Run();

class RackLayoutConsumer : IConsumer<RackLayoutCalculated>
{
    public async Task Consume(ConsumeContext<RackLayoutCalculated> ctx)
    {
        var cfg = ctx.Message.Config;
        // Build order lines from records; fabricate OrderId = CustomerId+Date
        var lines = ctx.Message.Records.Select(r => new OrderLine(r.CustomerId + r.OrderDate.ToString(), r.Product, r.Qty, ctx.Message.Locations.FirstOrDefault(l=>l.SkuId==r.Product)?.RackId ?? "R1")).ToList();
        List<Batch> batches = cfg.Mode.ToUpper()=="PTO" ? BuildPTO(lines, cfg) : BuildPTL(lines, cfg);
        await ctx.Publish(new BatchesCreated(ctx.Message.RunId, cfg, batches, ctx.Message.Racks, ctx.Message.Locations, ctx.Message.Demand, ctx.Message.Records));
    }

    private List<Batch> BuildPTO(List<OrderLine> lines, RunConfig cfg)
    {
        var byOrder = lines.GroupBy(l=>l.OrderId).ToList();
        var batches = new List<Batch>();
        int idx=0;
        foreach (var chunk in byOrder.Chunk(cfg.OrdersPerBatch))
        {
            var batchLines = chunk.SelectMany(g=>g).ToList();
            batches.Add(new Batch($"B{++idx}", "", "PTO", batchLines));
        }
        return batches;
    }

    private List<Batch> BuildPTL(List<OrderLine> lines, RunConfig cfg)
    {
        var bySku = lines.GroupBy(l=>l.SkuId).SelectMany(g => g).ToList();
        var batches = new List<Batch>();
        int idx=0;
        foreach (var chunk in bySku.Chunk(cfg.MaxLinesPerBatch))
        {
            batches.Add(new Batch($"B{++idx}", "", "PTL", chunk.ToList()));
        }
        return batches;
    }
}