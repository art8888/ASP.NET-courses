# HotelListing.API - Implementation Guide for Critical & High Priority Fixes

## 📌 Quick Reference: Critical Findings Implementation

---

## CRITICAL FIX #1: Implement Entity Framework Core + Database

### Step 1: Add Required NuGet Packages

```bash
cd /home/arturas/Desktop/works/dotNet/HotelListing.API/.net-learnings/HotelListing.API

dotnet add package Microsoft.EntityFrameworkCore
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
dotnet add package Microsoft.EntityFrameworkCore.Tools
```

### Step 2: Create DbContext

Create file: `Data/HotelListingDbContext.cs`

```csharp
using Microsoft.EntityFrameworkCore;

namespace HotelListing.API.Data;

public class HotelListingDbContext : DbContext
{
    public HotelListingDbContext(DbContextOptions<HotelListingDbContext> options) 
        : base(options)
    {
    }

    public DbSet<Country> Countries { get; set; }
    public DbSet<Hotel> Hotels { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Seed initial data
        modelBuilder.Entity<Country>().HasData(
            new Country { Id = 1, Name = "United States", ShortName = "US" },
            new Country { Id = 2, Name = "Canada", ShortName = "CA" },
            new Country { Id = 3, Name = "United Kingdom", ShortName = "UK" }
        );

        modelBuilder.Entity<Hotel>().HasData(
            new Hotel { Id = 1, Name = "Grand Plaza", Address = "123 Main St", Rating = 4.5 },
            new Hotel { Id = 2, Name = "Ocean View", Address = "456 Beach Rd", Rating = 4.8 }
        );
    }
}
```

### Step 3: Update Country Model with Validation

File: `Data/Country.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace HotelListing.API.Data;

public class Country
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(2, MinimumLength = 2)]
    public string ShortName { get; set; } = string.Empty;
}
```

### Step 4: Update Hotel Model with Validation

File: `Data/Hotel.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace HotelListing.API.Data;

public class Hotel
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string Address { get; set; } = string.Empty;

    [Range(0, 5)]
    public double Rating { get; set; }
}
```

### Step 5: Update appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=HotelListingDb;Integrated Security=true;"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

### Step 6: Update CountryRepository to Use DbContext

File: `Repositories/CountryRepository.cs`

```csharp
using HotelListing.API.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelListing.API.Repositories;

public class CountryRepository : ICountryRepository
{
    private readonly HotelListingDbContext _context;

    public CountryRepository(HotelListingDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Country>> GetAllAsync()
    {
        return await _context.Countries.ToListAsync();
    }

    public async Task<Country?> GetByIdAsync(int id)
    {
        return await _context.Countries.FindAsync(id);
    }

    public async Task<Country> CreateAsync(Country country)
    {
        _context.Countries.Add(country);
        await _context.SaveChangesAsync();
        return country;
    }

    public async Task<bool> UpdateAsync(int id, Country updatedCountry)
    {
        var country = await _context.Countries.FindAsync(id);

        if (country == null)
        {
            return false;
        }

        country.Name = updatedCountry.Name;
        country.ShortName = updatedCountry.ShortName;

        _context.Countries.Update(country);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var country = await _context.Countries.FindAsync(id);

        if (country == null)
        {
            return false;
        }

        _context.Countries.Remove(country);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> ExistsAsync(int id)
    {
        return await _context.Countries.AnyAsync(c => c.Id == id);
    }
}
```

### Step 7: Update Program.cs

```csharp
using HotelListing.API.Data;
using HotelListing.API.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add database context
builder.Services.AddDbContext<HotelListingDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

// Add repositories
builder.Services.AddScoped<ICountryRepository, CountryRepository>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// Run migrations automatically
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<HotelListingDbContext>();
    dbContext.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
```

### Step 8: Create Initial Migration

```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

---

## CRITICAL FIX #2: Implement Authentication & Authorization

### Step 1: Add JWT Authentication NuGet Packages

```bash
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add package System.IdentityModel.Tokens.Jwt
```

### Step 2: Update appsettings.json

```json
{
  "Jwt": {
    "Issuer": "https://yourdomain.com",
    "Audience": "https://yourdomain.com",
    "Key": "your-super-secret-key-minimum-32-characters-long!!!!"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=HotelListingDb;Integrated Security=true;"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

### Step 3: Create Authentication Service

Create file: `Services/AuthenticationService.cs`

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace HotelListing.API.Services;

public interface IAuthenticationService
{
    string GenerateToken(string userId, string username, string role);
}

public class AuthenticationService : IAuthenticationService
{
    private readonly IConfiguration _configuration;

    public AuthenticationService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(string userId, string username, string role)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, role)
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
```

### Step 4: Create Login Controller

Create file: `Controllers/AuthController.cs`

```csharp
using HotelListing.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace HotelListing.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authenticationService;

    public AuthController(IAuthenticationService authenticationService)
    {
        _authenticationService = authenticationService;
    }

    [HttpPost("login")]
    public ActionResult<dynamic> Login([FromBody] LoginRequest request)
    {
        // In a real application, validate against a database
        if (request.Username == "admin" && request.Password == "password")
        {
            var token = _authenticationService.GenerateToken("1", request.Username, "Admin");
            return Ok(new { token });
        }

        return Unauthorized(new { message = "Invalid credentials" });
    }
}

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
```

### Step 5: Update Program.cs with Authentication

```csharp
using HotelListing.API.Data;
using HotelListing.API.Repositories;
using HotelListing.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add JWT authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
var key = Encoding.UTF8.GetBytes(jwtSettings["Key"]!);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("Admin"));
    
    options.AddPolicy("UserOrAdmin", policy =>
        policy.RequireRole("User", "Admin"));
});

// Add services
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddDbContext<HotelListingDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});
builder.Services.AddScoped<ICountryRepository, CountryRepository>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecific", policy =>
    {
        policy.WithOrigins("https://yourdomain.com", "http://localhost:3000")
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<HotelListingDbContext>();
    dbContext.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("AllowSpecific");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
```

### Step 6: Add [Authorize] Attributes to Controllers

File: `Controllers/CountriesController.cs`

```csharp
using HotelListing.API.Data;
using HotelListing.API.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelListing.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class CountriesController : ControllerBase
{
    // ...existing code...

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<Country>>> GetCountries()
    {
        // Allow anonymous access to listing
        var countries = await _countryRepository.GetAllAsync();
        return Ok(countries);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<Country>> CreateCountry([FromBody] Country country)
    {
        // Only admins can create
        // ...existing code...
    }

    // Other methods remain authorized by default
}
```

---

## CRITICAL FIX #3: Implement Global Exception Handling

### Step 1: Create Exception Handling Middleware

Create file: `Middleware/ExceptionHandlingMiddleware.cs`

```csharp
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace HotelListing.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception has occurred.");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var response = new ProblemDetails
        {
            Title = "An error occurred",
            Detail = exception.Message
        };

        context.Response.StatusCode = exception switch
        {
            ValidationException => StatusCodes.Status400BadRequest,
            KeyNotFoundException => StatusCodes.Status404NotFound,
            UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status500InternalServerError
        };

        response.Status = context.Response.StatusCode;

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var json = JsonSerializer.Serialize(response, options);
        return context.Response.WriteAsync(json);
    }
}
```

### Step 2: Register Middleware in Program.cs

```csharp
using HotelListing.API.Middleware;

// ...existing code...

var app = builder.Build();

// Add exception handling middleware FIRST
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// ...rest of configuration...
```

---

## CRITICAL FIX #4: Implement Input Validation

### Step 1: Add FluentValidation Package

```bash
dotnet add package FluentValidation
dotnet add package FluentValidation.AspNetCore
```

### Step 2: Create Validators

Create file: `Validators/CountryValidator.cs`

```csharp
using FluentValidation;
using HotelListing.API.Data;

namespace HotelListing.API.Validators;

public class CountryValidator : AbstractValidator<Country>
{
    public CountryValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage("Country ID must be greater than 0");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Country name is required")
            .MaximumLength(100)
            .WithMessage("Country name cannot exceed 100 characters")
            .MinimumLength(1)
            .WithMessage("Country name must have at least 1 character");

        RuleFor(x => x.ShortName)
            .NotEmpty()
            .WithMessage("Country short name is required")
            .Length(2)
            .WithMessage("Country short name must be exactly 2 characters")
            .Matches("^[A-Z]{2}$")
            .WithMessage("Country short name must be 2 uppercase letters");
    }
}
```

Create file: `Validators/HotelValidator.cs`

```csharp
using FluentValidation;
using HotelListing.API.Data;

namespace HotelListing.API.Validators;

public class HotelValidator : AbstractValidator<Hotel>
{
    public HotelValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage("Hotel ID must be greater than 0");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Hotel name is required")
            .MaximumLength(200)
            .WithMessage("Hotel name cannot exceed 200 characters");

        RuleFor(x => x.Address)
            .NotEmpty()
            .WithMessage("Hotel address is required")
            .MaximumLength(500)
            .WithMessage("Hotel address cannot exceed 500 characters");

        RuleFor(x => x.Rating)
            .InclusiveBetween(0, 5)
            .WithMessage("Hotel rating must be between 0 and 5");
    }
}
```

### Step 3: Register Validators in Program.cs

```csharp
using FluentValidation;
using HotelListing.API.Validators;

// Add FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddFluentValidationAutoValidation();

builder.Services.AddControllers();
```

---

## HIGH PRIORITY FIX #5: Implement Structured Logging (Serilog)

### Step 1: Add Serilog Packages

```bash
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Sinks.File
dotnet add package Serilog.Sinks.Console
dotnet add package Serilog.Enrichers.Environment
dotnet add package Serilog.Enrichers.Process
dotnet add package Serilog.Enrichers.Thread
```

### Step 2: Update Program.cs

```csharp
using Serilog;
using Serilog.Events;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File(
        "logs/application-.txt",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
    )
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

// Use Serilog
builder.Host.UseSerilog();

// ...rest of configuration...
```

### Step 3: Add Logging to Repositories

```csharp
private readonly ILogger<CountryRepository> _logger;

public CountryRepository(HotelListingDbContext context, ILogger<CountryRepository> logger)
{
    _context = context;
    _logger = logger;
}

public async Task<IEnumerable<Country>> GetAllAsync()
{
    _logger.LogInformation("Fetching all countries");
    try
    {
        var countries = await _context.Countries.ToListAsync();
        _logger.LogInformation("Successfully fetched {CountryCount} countries", countries.Count);
        return countries;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error fetching countries");
        throw;
    }
}
```

---

## HIGH PRIORITY FIX #6: Implement API Versioning

### Step 1: Add API Versioning Package

```bash
dotnet add package Asp.Versioning.Mvc.ApiExplorer
```

### Step 2: Update Program.cs

```csharp
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new Asp.Versioning.ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = Asp.Versioning.ApiVersionReader.Combine(
        new Asp.Versioning.UrlSegmentApiVersionReader(),
        new Asp.Versioning.HeaderApiVersionReader("api-version")
    );
})
.AddApiExplorer();

builder.Services.AddSwaggerGen();
```

### Step 3: Update Controllers with Versioning

```csharp
using Asp.Versioning;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
public class CountriesController : ControllerBase
{
    // ...existing code...
}
```

---

## 📝 Testing the Fixes

### Test Database Connection

```csharp
// In Program.cs, test the database
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<HotelListingDbContext>();
    if (await dbContext.Database.CanConnectAsync())
    {
        Console.WriteLine("✓ Database connection successful");
    }
}
```

### Test Authentication

```bash
# Login to get token
curl -X POST https://localhost:7285/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"password"}'

# Use token in request
curl -X GET https://localhost:7285/api/countries \
  -H "Authorization: Bearer YOUR_TOKEN_HERE"
```

### Test Validation

```bash
# This should fail validation
curl -X POST https://localhost:7285/api/countries \
  -H "Content-Type: application/json" \
  -d '{"id":0,"name":"","shortName":"USA"}'
```

---

## ✅ Implementation Checklist

- [ ] CRITICAL FIX #1: Entity Framework Core + Database
  - [ ] Add NuGet packages
  - [ ] Create DbContext
  - [ ] Update models with validation
  - [ ] Update appsettings.json with connection string
  - [ ] Update CountryRepository
  - [ ] Update Program.cs
  - [ ] Create and run migration

- [ ] CRITICAL FIX #2: Authentication & Authorization
  - [ ] Add JWT packages
  - [ ] Update appsettings.json with JWT settings
  - [ ] Create AuthenticationService
  - [ ] Create AuthController
  - [ ] Update Program.cs with JWT configuration
  - [ ] Add [Authorize] attributes to controllers

- [ ] CRITICAL FIX #3: Exception Handling
  - [ ] Create ExceptionHandlingMiddleware
  - [ ] Register middleware in Program.cs
  - [ ] Test exception handling

- [ ] CRITICAL FIX #4: Input Validation
  - [ ] Add FluentValidation packages
  - [ ] Create validators
  - [ ] Register validators in Program.cs
  - [ ] Test validation

- [ ] HIGH PRIORITY FIX #5: Structured Logging
  - [ ] Add Serilog packages
  - [ ] Configure Serilog in Program.cs
  - [ ] Add logging to repositories and services
  - [ ] Verify log file creation

- [ ] HIGH PRIORITY FIX #6: API Versioning
  - [ ] Add API versioning package
  - [ ] Configure in Program.cs
  - [ ] Update controller routes
  - [ ] Test versioning

---

*Implementation Guide for HotelListing.API*  
*Created: August 16, 2026*

