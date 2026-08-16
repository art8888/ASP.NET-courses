using HotelListing.API.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the dependency injection container
builder.Services
    .AddApplicationServices()
    .AddApiConfiguration();

var app = builder.Build();

// Configure the HTTP request pipeline
app
    .UseDevelopmentMiddleware()
    .UseSecurityAndRoutingMiddleware();

app.Run();
