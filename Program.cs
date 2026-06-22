using FulfillmentCenterService.Api;
using FulfillmentCenterService.Application;
using FulfillmentCenterService.Application.Ports;
using FulfillmentCenterService.Infrastructure.FeatureFlags;
using FulfillmentCenterService.Infrastructure.Mocks;
using FulfillmentCenterService.Infrastructure.Outbox;
using FulfillmentCenterService.Infrastructure.Persistence;
using FulfillmentCenterService.Infrastructure.Workers;
using Microsoft.EntityFrameworkCore;

using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);
var serviceName = builder.Environment.ApplicationName;
var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:5107";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint)))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddPrometheusExporter());

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.Configure<FeatureFlagsOptions>(builder.Configuration.GetSection(FeatureFlagsOptions.SectionName));
var useMockRepositories = builder.Configuration.GetValue<bool>("FeatureFlags:UseMockRepositories");

builder.Services.AddDbContext<FulfillmentDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("FulfillmentDb"));
});

builder.Services.AddScoped<CandidateSearchService>();
builder.Services.AddScoped<CapacityReservationService>();
builder.Services.AddScoped<CapacityManagementService>();
builder.Services.AddScoped<OperationalCalendarService>();

if (useMockRepositories)
{
    builder.Services.AddSingleton<IFulfillmentCenterRepository, MockFulfillmentCenterRepository>();
    builder.Services.AddSingleton<ICapacityRepository, MockCapacityRepository>();
    builder.Services.AddSingleton<ICapacityReservationRepository, MockCapacityReservationRepository>();
    builder.Services.AddSingleton<IOperationalCalendarRepository, MockOperationalCalendarRepository>();
}
else
{
    builder.Services.AddScoped<IFulfillmentCenterRepository, FulfillmentCenterRepository>();
    builder.Services.AddScoped<ICapacityRepository, CapacityRepository>();
    builder.Services.AddScoped<ICapacityReservationRepository, CapacityReservationRepository>();
    builder.Services.AddScoped<IOperationalCalendarRepository, OperationalCalendarRepository>();
}
builder.Services.AddScoped<IOutboxWriter, OutboxWriter>();

builder.Services.AddHostedService<ReservationExpirationWorker>();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<FulfillmentDbContext>();

var app = builder.Build();

app.UseOpenTelemetryPrometheusScrapingEndpoint("/metrics");

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/health");
app.MapFulfillmentEndpoints();

app.Run();
