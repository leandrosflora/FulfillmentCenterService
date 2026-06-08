using FulfillmentCenterService.Api;
using FulfillmentCenterService.Application;
using FulfillmentCenterService.Application.Ports;
using FulfillmentCenterService.Infrastructure.Outbox;
using FulfillmentCenterService.Infrastructure.Persistence;
using FulfillmentCenterService.Infrastructure.Workers;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<FulfillmentDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("FulfillmentDb"));
});

builder.Services.AddScoped<CandidateSearchService>();
builder.Services.AddScoped<CapacityReservationService>();
builder.Services.AddScoped<CapacityManagementService>();
builder.Services.AddScoped<OperationalCalendarService>();

builder.Services.AddScoped<IFulfillmentCenterRepository, FulfillmentCenterRepository>();
builder.Services.AddScoped<ICapacityRepository, CapacityRepository>();
builder.Services.AddScoped<ICapacityReservationRepository, CapacityReservationRepository>();
builder.Services.AddScoped<IOperationalCalendarRepository, OperationalCalendarRepository>();
builder.Services.AddScoped<IOutboxWriter, OutboxWriter>();

builder.Services.AddHostedService<ReservationExpirationWorker>();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<FulfillmentDbContext>();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/health");
app.MapFulfillmentEndpoints();

app.Run();
