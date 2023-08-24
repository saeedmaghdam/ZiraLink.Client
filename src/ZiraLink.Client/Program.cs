using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using Duende.Bff.Yarp;
using IdentityModel;
using IdentityModel.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using ZiraLink.Client;
using ZiraLink.Client.Helpers;
using ZiraLink.Client.Application;
using ZiraLink.Client.Services;
using ZiraLink.Client.Framework.Services;
using ZiraLink.Client.Framework.Application;
using ZiraLink.Client.Framework.Helpers;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);

var pathToExe = Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location);
Directory.SetCurrentDirectory(pathToExe!);

IConfiguration Configuration = new ConfigurationBuilder()
    .SetBasePath(pathToExe)
    .Add(new CustomConfigurationSource())
    .AddEnvironmentVariables()
    .Build();

// Add services to the container.

builder.Services.AddAuthorization();
builder.Services
    .AddBff()
    .AddRemoteApis();
JwtSecurityTokenHandler.DefaultMapInboundClaims = false;
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = "oidc";
    options.DefaultSignOutScheme = "oidc";
})
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddOpenIdConnect("oidc", options =>
            {
                options.RequireHttpsMetadata = false;
                options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.Authority = Configuration["ZIRALINK_URL_IDS"]!;
                options.ClientId = "client";
                options.ClientSecret = "secret";
                options.ResponseType = OidcConstants.ResponseTypes.Code;
                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");
                options.SaveTokens = true;

                options.GetClaimsFromUserInfoEndpoint = true;

                if (Configuration["ASPNETCORE_ENVIRONMENT"] != "Production")
                {
                    HttpClientHandler handler = new HttpClientHandler();
                    //handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                    options.BackchannelHttpHandler = handler;
                }
            });

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
builder.Services.AddHostedService<Worker>();

builder.Services.AddSingleton(serviceProvider =>
{
    var factory = new ConnectionFactory();
    factory.DispatchConsumersAsync = true;
    factory.Uri = new Uri(Configuration["ZIRALINK_CONNECTIONSTRINGS_RABBITMQ"]!);
    var connection = factory.CreateConnection();
    var channel = connection.CreateModel();

    return channel;
});

builder.Services.AddMemoryCache();

var app = builder.Build();

var hostHelper = app.Services.GetRequiredService<IHostsHelper>();
hostHelper.ConfigureDns();

var certificateHelper = app.Services.GetRequiredService<ICertificateHelper>();
certificateHelper.InstallCertificate();

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
app.UseAuthorization();
app.UseBff();

app.UseEndpoints(endpoints =>
{
    endpoints.MapBffManagementEndpoints();
    endpoints.MapGet("/", async (HttpContext context, IConfiguration configuration, IHttpContextAccessor httpContextAccessor, ISignalService signalService) =>
    {
        if (System.IO.File.Exists("profile"))
            return "Authorized";

        if (!context.User.Identity.IsAuthenticated)
        {
            context.Response.Redirect("/bff/login");
            return "";
        }

        var userId = httpContextAccessor.HttpContext.User.Claims.SingleOrDefault(claim => claim.Type == "sub");

        var token = await context.GetTokenAsync("access_token");
        Uri baseUri = new Uri(configuration["ZIRALINK_URL_IDS"]);
        Uri uri = new Uri(baseUri, "connect/userinfo");
        var userInfoRequest = new UserInfoRequest
        {
            Address = uri.ToString(),
            Token = token
        };

        var client = new HttpClient();
        var userInfoResponse = await client.GetUserInfoAsync(userInfoRequest);

        if (userInfoResponse.IsError)
        {
            context.Response.Redirect("/bff/login");
            return "";
        }

        var result = await userInfoResponse.HttpResponse.Content.ReadAsStringAsync();

        System.IO.File.WriteAllText("profile", result);
        signalService.Set();

        return result;
    }).ExcludeFromDescription();
});


app.Run();
