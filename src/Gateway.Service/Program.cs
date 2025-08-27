using Yarp.ReverseProxy; // make sure YARP is imported
using Microsoft.AspNetCore.Builder;

var builder = WebApplication.CreateBuilder(args);

// Add reverse proxy service
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

// Enable proxy routing
app.MapReverseProxy();

app.Run();
