using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;  // <-- this was missing
using Yarp.ReverseProxy;

var builder = WebApplication.CreateBuilder(args);

// Add reverse proxy service
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

// Enable proxy routing
app.MapReverseProxy();

app.Run();
