using System;
using System.Threading.Tasks;
using Contracts;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<ResultConsumer>();
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["Rabbit:Host"] ?? "rabbitmq", "/", h => { });
        cfg.ConfigureEndpoints(ctx);
    });
});

var app = builder.Build();

app.MapPost("/runs", async (IPublishEndpoint bus, StartRequest req) =>
{
    var runId = Guid.NewGuid();
    await bus.Publish(new StartRunCommand(runId, req.DatasetPath, req.Mode));
    return Results.Accepted($"/runs/{runId}", new { runId, status = "Started" });
});

app.Run();

record StartRequest(string DatasetPath, string Mode);

class ResultConsumer : IConsumer<HitRateCalculated>
{
    public async Task Consume(ConsumeContext<HitRateCalculated> ctx)
    {
        await Console.Out.WriteLineAsync($"Run {ctx.Message.RunId} finished, hit rate {ctx.Message.Result.HitRate}");
    }
}
