﻿using System.Reflection;
using Microsoft.AspNetCore.HttpOverrides;
using ZiraLink.Client;
using ZiraLink.Client.Helpers;
using ZiraLink.Client.Application;
using ZiraLink.Client.Services;
using ZiraLink.Client.Framework.Services;
using ZiraLink.Client.Framework.Application;
using ZiraLink.Client.Framework.Helpers;
using RabbitMQ.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

var builder = WebApplication.CreateBuilder();

var pathToExe = Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location);

IConfiguration Configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", false, true)
    .AddEnvironmentVariables()
    .Build();

Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(Configuration)
                .CreateLogger();
builder.Host.UseSerilog();

// Add services to the container.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto;
    // Only loopback proxies are allowed by default.
    // Clear that restriction because forwarders are enabled by explicit
    // configuration.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddRazorPages();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IConfiguration>(Configuration);
builder.Services.AddSingleton<ISignalService, SignalService>();
builder.Services.AddSingleton<ICertificateHelper, CertificateHelper>();
builder.Services.AddSingleton<IHostsHelper, HostsHelper>();
builder.Services.AddSingleton<IWebSocketService, WebSocketService>();
builder.Services.AddSingleton<IWebSocketFactory, WebSocketFactory>();
builder.Services.AddSingleton<IHttpRequestHandlerService, HttpRequestHandlerService>();
builder.Services.AddSingleton<IWebSocketHandlerService, WebSocketHandlerService>();
builder.Services.AddSingleton<IHttpHelper, HttpHelper>();
builder.Services.AddSingleton<ICache, Cache>();
builder.Services.AddSingleton<IServerBusService, ServerBusService>();
builder.Services.AddSingleton<IClientBusService, ClientBusService>();
builder.Services.AddSingleton<ISharePortSocketService, SharePortSocketService>();
builder.Services.AddSingleton<IUsePortSocketService, UsePortSocketService>();
builder.Services.AddHostedService<Worker>();

builder.Services.AddSingleton(serviceProvider =>
{
    var remainingAttempts = 10;
    do
    {
        try
        {
            var factory = new ConnectionFactory();
            factory.DispatchConsumersAsync = true;
            factory.Uri = new Uri(Configuration["ZIRALINK_CONNECTIONSTRINGS_RABBITMQ"]!);
            var connection = factory.CreateConnection();
            var channel = connection.CreateModel();
            return channel;
        }
        catch (Exception ex)
        {
            Log.Logger.Warning(ex.Message);

            if (--remainingAttempts == 0)
                throw;

            Task.Delay(TimeSpan.FromSeconds(5)).Wait();
        }
    } while (true);
});

builder.Services.AddMemoryCache();

var app = builder.Build();

app.Use((context, next) =>
{
    context.Request.Scheme = "https";
    return next(context);
});
app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();

app.Run();

public partial class ProgramMock { }
