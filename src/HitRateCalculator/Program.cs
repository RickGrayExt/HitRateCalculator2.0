
using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<FormOptions>(o => { o.MultipartBodyLengthLimit = 1024L * 1024L * 200; });
var app = WebApplication.Create(builder.Configuration);
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/health", () => Results.Ok(new { ok = true }));

app.MapPost("/api/calculate", async (HttpRequest req) =>
{
    var form = await req.ReadFormAsync();
    var cfg = new SystemConfiguration
    {
        UsePickToLine = form["usePickToLine"] == "on",
        StationCapacity = TryInt(form["stationCapacity"], 5),
        NumberOfStations = TryInt(form["numberOfStations"], 10),
        WaveSize = TryInt(form["waveSize"], 100),
        EnableSeasonality = form["enableSeasonality"] == "on",
        SKUsPerRack = TryInt(form["skusPerRack"], 12),
        MaxOrdersPerBatch = TryInt(form["maxOrdersPerBatch"], 10),
        MaxStationsOpen = TryInt(form["maxStationsOpen"], 5),
        DataSource = string.IsNullOrWhiteSpace(form["dataUrl"]) ? "upload" : "github",
        DataPath = form["dataUrl"]
    };

    var calculator = new HitRateCalculator();

    try
    {
        List<SalesRecord> sales;
        if (cfg.DataSource == "github")
        {
            if (string.IsNullOrWhiteSpace(cfg.DataPath))
                return Results.BadRequest("Data URL is empty.");
            using var http = new HttpClient();
            var csv = await http.GetStringAsync(cfg.DataPath);
            sales = DataImportService.ParseCsv(csv);
        }
        else
        {
            if (req.Form.Files.Count == 0) return Results.BadRequest("No file uploaded.");
            var file = req.Form.Files[0];
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var csv = Encoding.UTF8.GetString(ms.ToArray());
            sales = DataImportService.ParseCsv(csv);
        }

        var result = await calculator.CalculateAsync(cfg, sales);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

app.Run();

static int TryInt(string? s, int d) => int.TryParse(s, out var v) ? v : d;

// Domain types and services (same as previous message, condensed)

public class SalesRecord
{
    public DateTime Order_Date { get; set; }
    public TimeSpan Time { get; set; }
    public string Customer_Id { get; set; } = "";
    public string Product_Category { get; set; } = "";
    public string Product { get; set; } = "";
    public decimal Sales { get; set; }
    public int Quantity { get; set; }
    public string Order_Priority { get; set; } = "";
}
public class SKU { public string SKUId { get; set; } = ""; public string Product { get; set; } = ""; public string Category { get; set; } = ""; public int Group { get; set; } public decimal Velocity { get; set; } public string ShelfLocation { get; set; } = ""; public int RackId { get; set; } }
public class OrderBatch { public int BatchId { get; set; } public List<string> SKUs { get; set; } = new(); public List<string> OrderIds { get; set; } = new(); public int StationId { get; set; } public int TotalUnits { get; set; } }
public class RackPresentation { public int RackId { get; set; } public List<string> AvailableSKUs { get; set; } = new(); public int ItemsPickable { get; set; } public int PresentationCount { get; set; } }
public class HitRateResult { public double HitRate { get; set; } public int TotalRackPresentations { get; set; } public int TotalItemsPicked { get; set; } public string CalculationMethod { get; set; } = ""; public Dictionary<int, int> RackUtilization { get; set; } = new(); public int TotalRacks { get; set; } }
public class SystemConfiguration { public bool UsePickToLine { get; set; } = false; public int StationCapacity { get; set; } = 5; public int NumberOfStations { get; set; } = 10; public int WaveSize { get; set; } = 100; public bool EnableSeasonality { get; set; } = true; public string DataSource { get; set; } = "upload"; public string DataPath { get; set; } = ""; public int SKUsPerRack { get; set; } = 12; public int MaxOrdersPerBatch { get; set; } = 10; public int MaxStationsOpen { get; set; } = 5; }

public static class DataImportService
{
    public static List<SalesRecord> ParseCsv(string csv)
    {
        var list = new List<SalesRecord>();
        using var sr = new StringReader(csv);
        var header = sr.ReadLine();
        if (header == null) return list;
        var headers = header.Split(',').Select(h => h.Trim()).ToArray();
        int idx(string name){ for(int i=0;i<headers.Length;i++) if(string.Equals(headers[i], name, StringComparison.OrdinalIgnoreCase)) return i; return -1;}
        string? line;
        var ci = CultureInfo.InvariantCulture;
        while((line = sr.ReadLine())!=null){
            var cols = SplitCsv(line);
            DateTime.TryParse(cols[Math.Max(0, idx("Order_Date"))], ci, DateTimeStyles.None, out var od);
            TimeSpan.TryParse(cols[Math.Max(0, idx("Time"))], out var ts);
            decimal.TryParse(cols[Math.Max(0, idx("Sales"))], NumberStyles.Any, ci, out var sale);
            int.TryParse(cols[Math.Max(0, idx("Quantity"))], out var qty);
            list.Add(new SalesRecord{
                Order_Date = od, Time = ts,
                Customer_Id = cols[Math.Max(0, idx("Customer_Id"))],
                Product_Category = cols[Math.Max(0, idx("Product_Category"))], // note: will fix below
                Product = cols[Math.Max(0, idx("Product"))],
                Sales = sale, Quantity = qty,
                Order_Priority = cols[Math.Max(0, idx("Order_Priority"))]
            });
        }
        return list;
    }
    static string[] SplitCsv(string line){ var res=new List<string>(); var sb=new System.Text.StringBuilder(); bool q=false; foreach(var ch in line){ if(ch=='"') q=!q; else if(ch==',' && !q){ res.Add(sb.ToString()); sb.Clear(); } else sb.Append(ch);} res.Add(sb.ToString()); return res.ToArray(); }
}
