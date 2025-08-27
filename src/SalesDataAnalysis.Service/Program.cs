using Contracts;
using MassTransit;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<StartRunConsumer>();
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["Rabbit:Host"] ?? "rabbitmq", "/", h => { });
        cfg.ConfigureEndpoints(ctx);
    });
});
var app = builder.Build();
app.Run();

class StartRunConsumer : IConsumer<StartRunCommand>
{
    public async Task Consume(ConsumeContext<StartRunCommand> ctx)
    {
        var path = ctx.Message.DatasetPath;
        var records = new List<SalesRecord>();
        using var sr = new StreamReader(path);
        string? header = sr.ReadLine();
        while (!sr.EndOfStream)
        {
            var line = sr.ReadLine();
            if (string.IsNullOrWhiteSpace(line)) continue;
            var cols = line.Split(',');
            var od = DateOnly.Parse(cols[0]);
            var tm = TimeOnly.Parse(cols[1]);
            var rec = new SalesRecord(od, tm, cols[2], cols[3], cols[4], decimal.Parse(cols[5], CultureInfo.InvariantCulture), int.Parse(cols[6]), cols[7]);
            records.Add(rec);
        }
        // demand per SKU
        var demand = records.GroupBy(r => r.Product).Select(g => {
            int totalUnits = g.Sum(x => x.Qty);
            int orders = g.Select(x => x.CustomerId + x.OrderDate.ToString()).Distinct().Count();
            double velocity = orders == 0 ? 0 : (double)totalUnits / orders;
            // Seasonal if >40% of units in top 2 months
            var byMonth = g.GroupBy(x => x.OrderDate.Month).Select(m => m.Sum(x => x.Qty)).OrderByDescending(x => x).ToList();
            bool seasonal = byMonth.Count>=2 && (byMonth[0] + (byMonth.Count>1?byMonth[1]:0)) > 0.4 * totalUnits;
            return new SkuDemand(g.Key, totalUnits, orders, velocity, seasonal);
        }).OrderByDescending(d=>d.Velocity).ToList();

        await ctx.Publish(new SalesPatternsIdentified(ctx.Message.RunId, ctx.Message.Config, demand, records));
    }
}