using HotelListing.API.Data;
using HotelListing.API.Extensions;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("HotelListingConnectionString");
builder.Services.AddDbContext<HotelListingDbContext>(options =>
    options.UseSqlServer(connectionString));
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
