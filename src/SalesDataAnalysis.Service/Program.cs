using Contracts;
using MassTransit;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient();
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
    private readonly IHttpClientFactory _http;
    public StartRunConsumer(IHttpClientFactory http) => _http = http;

    public async Task Consume(ConsumeContext<StartRunCommand> ctx)
    {
        var csv = await _http.CreateClient().GetStringAsync(ctx.Message.DataUrl);
        var sales = ParseCsv(csv);
        // demand per SKU
        var groups = sales.GroupBy(s => (s.Product, s.ProductCategory));
        var demand = new List<SkuDemand>();
        foreach (var g in groups)
        {
            int totalUnits = g.Sum(x => x.Qty);
            int orderCount = g.Count();
            double velocity = orderCount == 0 ? 0 : (double)totalUnits / orderCount;
            // simple seasonality: stddev across months
            var byMonth = g.GroupBy(x => x.OrderDate.Month).Select(m => m.Sum(x => x.Qty)).ToArray();
            double mean = byMonth.Length == 0 ? 0 : byMonth.Average();
            double std = byMonth.Length <= 1 ? 0 : Math.Sqrt(byMonth.Select(v => Math.Pow(v - mean, 2)).Average());
            bool seasonal = mean > 0 && std / mean > 0.5;
            demand.Add(new SkuDemand(g.Key.Product, g.Key.Product, g.Key.ProductCategory, totalUnits, orderCount, velocity, seasonal));
        }
        await ctx.Publish(new SalesPatternsIdentified(ctx.Message.RunId, demand.OrderByDescending(d => d.Velocity).ToList()));
    }

    static List<SalesRecord> ParseCsv(string csv)
    {
        var list = new List<SalesRecord>();
        using var sr = new StringReader(csv);
        var header = sr.ReadLine(); if (header == null) return list;
        var headers = header.Split(',').Select(h => h.Trim()).ToArray();
        int Col(string name) { for (int i=0;i<headers.Length;i++) if (string.Equals(headers[i], name, StringComparison.OrdinalIgnoreCase)) return i; return -1; }
        string? line;
        var ci = CultureInfo.InvariantCulture;
        while ((line = sr.ReadLine()) != null)
        {
            var cols = SplitCsv(line);
            DateOnly.TryParse(cols[Math.Max(0, Col("Order_Date"))], ci, DateTimeStyles.None, out var od);
            TimeOnly.TryParse(cols[Math.Max(0, Col("Time"))], ci, DateTimeStyles.None, out var tm);
            decimal.TryParse(cols[Math.Max(0, Col("Sales"))], NumberStyles.Any, ci, out var sale);
            int.TryParse(cols[Math.Max(0, Col("Quantity"))], out var qty);
            list.Add(new SalesRecord(
                od, tm,
                cols[Math.Max(0, Col("Customer_Id"))],
                cols[Math.Max(0, Col("Product_Category"))],
                cols[Math.Max(0, Col("Product"))],
                sale, qty,
                cols[Math.Max(0, Col("Order_Priority"))]
            ));
        }
        return list;
    }
    static string[] SplitCsv(string line)
    {
        var res = new List<string>(); var sb = new System.Text.StringBuilder(); bool q=false;
        foreach (var ch in line){ if(ch=='\"') q=!q; else if(ch==',' && !q){res.Add(sb.ToString()); sb.Clear();} else sb.Append(ch);}
        res.Add(sb.ToString()); return res.ToArray();
    }
}
