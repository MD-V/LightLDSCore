using LightLDS;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<LightLdsBackgroundService>();

builder.Services.AddWindowsService();
builder.Services.AddSystemd();

var host = builder.Build();
await host.RunAsync();