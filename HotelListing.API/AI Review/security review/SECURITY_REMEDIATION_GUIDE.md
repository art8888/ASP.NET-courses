# 🔐 HotelListing.API - Security Remediation Guide

**Date:** August 16, 2026  
**Purpose:** Step-by-step implementation guide to fix all critical security issues  
**Estimated Time:** 20-30 hours  
**Difficulty:** Intermediate to Advanced

---

## Phase 1: Critical Fixes (Days 1-2, ~12 hours)

### Step 1: Implement JWT Authentication

#### 1.1 Add Required NuGet Packages

```bash
cd /home/arturas/Desktop/works/dotNet/HotelListing.API/.net-learnings/HotelListing.API

dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add package System.IdentityModel.Tokens.Jwt
```

#### 1.2 Create JWT Configuration Model

Create file: `Config/JwtSettings.cs`

```csharp
namespace HotelListing.API.Config;

public class JwtSettings
{
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpirationMinutes { get; set; } = 60;
}
```

#### 1.3 Update appsettings.json (Base - NO SECRETS)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "yourdomain.com,www.yourdomain.com",
  "Jwt": {
    "Issuer": "https://yourdomain.com",
    "Audience": "https://yourdomain.com",
    "ExpirationMinutes": 60
    // Key comes from User Secrets or Environment Variables
  },
  "ConnectionStrings": {
    "DefaultConnection": ""
  }
}
```

#### 1.4 Update appsettings.Development.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information"
    }
  },
  "AllowedHosts": "localhost,127.0.0.1"
}
```

#### 1.5 Create User Secrets

```bash
# Initialize secrets
dotnet user-secrets init

# Set JWT Key (minimum 32 characters)
dotnet user-secrets set "Jwt:Key" "your-super-secret-key-minimum-32-characters!!!!"

# Verify it was set
dotnet user-secrets list
```

#### 1.6 Create Authentication Service

Create file: `Services/AuthenticationService.cs`

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using HotelListing.API.Config;
using Microsoft.IdentityModel.Tokens;

namespace HotelListing.API.Services;

public interface IAuthenticationService
{
    string GenerateToken(string userId, string username, string role);
    bool ValidateToken(string token);
}

public class AuthenticationService : IAuthenticationService
{
    private readonly IConfiguration _configuration;
    private readonly JwtSettings _jwtSettings;

    public AuthenticationService(IConfiguration configuration)
    {
        _configuration = configuration;
        _jwtSettings = _configuration.GetSection("Jwt").Get<JwtSettings>()
            ?? throw new InvalidOperationException("JWT settings not configured");
    }

    public string GenerateToken(string userId, string username, string role)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_jwtSettings.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, role),
            new Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
            new Claim("nonce", Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public bool ValidateToken(string token)
    {
        try
        {
            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_jwtSettings.Key));

            var tokenHandler = new JwtSecurityTokenHandler();
            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidIssuer = _jwtSettings.Issuer,
                ValidateAudience = true,
                ValidAudience = _jwtSettings.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out SecurityToken validatedToken);

            return true;
        }
        catch
        {
            return false;
        }
    }
}
```

#### 1.7 Create Authentication Extension

Add to file: `Extensions/ServiceExtensions.cs`

```csharp
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using HotelListing.API.Config;
using HotelListing.API.Services;

namespace HotelListing.API.Extensions;

public static partial class ServiceExtensions
{
    public static IServiceCollection AddAuthenticationServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection("Jwt").Get<JwtSettings>()
            ?? throw new InvalidOperationException("JWT settings not configured");

        var key = Encoding.UTF8.GetBytes(jwtSettings.Key);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ClockSkew = TimeSpan.Zero  // Be strict with token expiry
                };

                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        // Log authentication failures
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILogger<object>>();
                        logger.LogWarning(
                            "Authentication failed: {Message} from {RemoteIP}",
                            context.Exception?.Message,
                            context.HttpContext.Connection.RemoteIpAddress);
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminOnly", policy =>
                policy.RequireRole("Admin"));

            options.AddPolicy("UserOrAdmin", policy =>
                policy.RequireRole("User", "Admin"));
        });

        services.AddScoped<IAuthenticationService, AuthenticationService>();

        return services;
    }
}
```

#### 1.8 Create Auth Controller

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
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthenticationService authenticationService, ILogger<AuthController> logger)
    {
        _authenticationService = authenticationService;
        _logger = logger;
    }

    /// <summary>
    /// Login endpoint - returns JWT token
    /// </summary>
    [HttpPost("login")]
    public ActionResult<LoginResponse> Login([FromBody] LoginRequest request)
    {
        // TODO: Replace with actual user validation from database
        // This is demo only - validate against Identity or database
        if (string.IsNullOrWhiteSpace(request.Username) || 
            string.IsNullOrWhiteSpace(request.Password))
        {
            _logger.LogWarning("Login attempt with empty credentials from {RemoteIP}",
                HttpContext.Connection.RemoteIpAddress);
            return BadRequest(new { message = "Username and password are required" });
        }

        // TODO: Hash password and compare
        if (request.Username == "admin" && request.Password == "admin123")
        {
            var token = _authenticationService.GenerateToken("1", request.Username, "Admin");
            _logger.LogInformation("Successful login for user: {Username}", request.Username);
            return Ok(new LoginResponse { Token = token });
        }

        _logger.LogWarning("Failed login attempt for username: {Username} from {RemoteIP}",
            request.Username,
            HttpContext.Connection.RemoteIpAddress);
        
        return Unauthorized(new { message = "Invalid username or password" });
    }
}

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
}
```

#### 1.9 Update Program.cs

Modify `Program.cs`:

```csharp
using HotelListing.API.Extensions;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Add authentication services
builder.Services
    .AddApplicationServices()
    .AddAuthenticationServices(builder.Configuration)
    .AddApiConfiguration();

var app = builder.Build();

// Configure middleware - AUTH BEFORE AUTHZ!
app
    .UseDevelopmentMiddleware()
    .UseAuthentication()        // ← MUST BE BEFORE UseAuthorization
    .UseAuthorization()         // ← Authorization after authentication
    .UseSecurityAndRoutingMiddleware();

app.Run();
```

---

### Step 2: Add Authorization Attributes to Controllers

#### 2.1 Update CountriesController.cs

```csharp
using HotelListing.API.Data;
using HotelListing.API.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelListing.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]  // ← Require auth for all endpoints by default
public class CountriesController : ControllerBase
{
    // ... existing code ...

    [HttpGet]
    [AllowAnonymous]  // ← Allow public access to listing
    public async Task<ActionResult<IEnumerable<Country>>> GetCountries()
    {
        var countries = await _countryRepository.GetAllAsync();
        return Ok(countries);
    }

    [HttpGet("{id:int}")]
    [AllowAnonymous]  // ← Allow public access to details
    public async Task<ActionResult<Country>> GetCountry(int id)
    {
        var country = await _countryRepository.GetByIdAsync(id);
        if (country == null)
            return NotFound();
        return Ok(country);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]  // ← Only admins can create
    public async Task<ActionResult<Country>> CreateCountry([FromBody] Country country)
    {
        if (country == null)
            return BadRequest();

        if (await _countryRepository.ExistsAsync(country.Id))
            return BadRequest(new { message = "Country already exists" });

        var createdCountry = await _countryRepository.CreateAsync(country);
        return CreatedAtAction(nameof(GetCountry), new { id = createdCountry.Id }, createdCountry);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]  // ← Only admins can update
    public async Task<ActionResult> UpdateCountry(int id, [FromBody] Country updatedCountry)
    {
        var result = await _countryRepository.UpdateAsync(id, updatedCountry);
        if (!result)
            return NotFound();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]  // ← Only admins can delete
    public async Task<ActionResult> DeleteCountry(int id)
    {
        var result = await _countryRepository.DeleteAsync(id);
        if (!result)
            return NotFound();
        return NoContent();
    }
}
```

#### 2.2 Update HotelsControllers.cs

Apply the same pattern (add `[Authorize]` and `[Authorize(Roles = "Admin")]` where appropriate).

---

### Step 3: Implement Input Validation

#### 3.1 Add FluentValidation Package

```bash
dotnet add package FluentValidation
dotnet add package FluentValidation.AspNetCore
```

#### 3.2 Create Validators

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
            .MinimumLength(1)
            .WithMessage("Country name must have at least 1 character")
            .MaximumLength(100)
            .WithMessage("Country name cannot exceed 100 characters")
            .Matches(@"^[a-zA-Z\s\-'.]+$")
            .WithMessage("Country name contains invalid characters");

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

#### 3.3 Register Validators

Add to `Extensions/ServiceExtensions.cs`:

```csharp
using FluentValidation;
using HotelListing.API.Validators;

public static IServiceCollection AddValidationServices(this IServiceCollection services)
{
    services.AddValidatorsFromAssemblyContaining<Program>();
    services.AddFluentValidationAutoValidation();
    return services;
}
```

Update `Program.cs`:

```csharp
builder.Services
    .AddApplicationServices()
    .AddAuthenticationServices(builder.Configuration)
    .AddValidationServices()  // ← ADD THIS
    .AddApiConfiguration();
```

---

### Step 4: Fix AllowedHosts Configuration

Update `appsettings.json`:

```json
{
  "AllowedHosts": "yourdomain.com,www.yourdomain.com,api.yourdomain.com",
  ...
}
```

Update `appsettings.Development.json`:

```json
{
  "AllowedHosts": "localhost,127.0.0.1,yourdomain.local",
  ...
}
```

Create `appsettings.Production.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "yourdomain.com,www.yourdomain.com"
}
```

---

### Step 5: Implement Global Exception Handler

Create file: `Middleware/ExceptionHandlingMiddleware.cs`

```csharp
using System.Net;
using Microsoft.AspNetCore.Mvc;

namespace HotelListing.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next, 
        ILogger<ExceptionHandlingMiddleware> logger,
        IWebHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception has occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var response = new ProblemDetails
        {
            Title = "An error occurred",
            Status = StatusCodes.Status500InternalServerError,
            // ⚠️ Only expose details in development
            Detail = _environment.IsDevelopment()
                ? exception.Message
                : "An internal server error occurred"
        };

        context.Response.StatusCode = response.Status.Value;

        return context.Response.WriteAsJsonAsync(response);
    }
}
```

Add to `Extensions/MiddlewareExtensions.cs`:

```csharp
using HotelListing.API.Middleware;

public static WebApplication UseExceptionHandlingMiddleware(this WebApplication app)
{
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    return app;
}
```

Update `Program.cs`:

```csharp
app
    .UseExceptionHandlingMiddleware()  // ← FIRST
    .UseDevelopmentMiddleware()
    .UseAuthentication()
    .UseAuthorization()
    .UseSecurityAndRoutingMiddleware();
```

---

### Step 6: Secure Configuration Management

#### 6.1 Add .gitignore Entries

```bash
# Add to .gitignore
appsettings.Production.json
appsettings.*.local.json
http-client.env.json
.user-secrets/
secrets/
```

#### 6.2 Use Environment Variables for Production

```dockerfile
# Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0

ENV Jwt__Key="from-keyvault-or-secrets"
ENV ConnectionStrings__DefaultConnection="production-connection-string"
ENV ASPNETCORE_ENVIRONMENT=Production

COPY bin/Release/net10.0 /app
WORKDIR /app
ENTRYPOINT ["dotnet", "HotelListing.API.dll"]
```

---

## Phase 2: High Priority Fixes (Day 3, ~8 hours)

### Step 7: Add Rate Limiting

```bash
dotnet add package AspNetCoreRateLimit
```

Add to `Extensions/ServiceExtensions.cs`:

```csharp
public static IServiceCollection AddRateLimitingServices(
    this IServiceCollection services)
{
    services.AddMemoryCache();
    services.AddInMemoryRateLimiting();

    services.Configure<IpRateLimitOptions>(options =>
    {
        options.GeneralRules = new List<RateLimitRule>
        {
            new RateLimitRule
            {
                Endpoint = "*",
                Limit = 100,
                Period = "1m"
            },
            new RateLimitRule
            {
                Endpoint = "*/auth/login",
                Limit = 5,
                Period = "5m"
            }
        };
    });

    return services;
}
```

Update `Program.cs`:

```csharp
builder.Services.AddRateLimitingServices();
app.UseIpRateLimiting();
```

### Step 8: Add CORS Policy

Add to `Extensions/ServiceExtensions.cs`:

```csharp
public static IServiceCollection AddCorsPolicy(
    this IServiceCollection services, IConfiguration configuration)
{
    var corsOrigins = configuration.GetSection("Cors:AllowedOrigins")
        .Get<string[]>() ?? new[] { "http://localhost:3000" };

    services.AddCors(options =>
    {
        options.AddPolicy("AllowedOrigins", policy =>
        {
            policy
                .WithOrigins(corsOrigins)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        });
    });

    return services;
}
```

Add to `appsettings.json`:

```json
{
  "Cors": {
    "AllowedOrigins": ["http://localhost:3000", "https://yourdomain.com"]
  }
}
```

Update `Program.cs`:

```csharp
builder.Services.AddCorsPolicy(builder.Configuration);
app.UseCors("AllowedOrigins");
```

### Step 9: Add Security Headers

Create file: `Middleware/SecurityHeadersMiddleware.cs`

```csharp
namespace HotelListing.API.Middleware;

public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Prevent clickjacking
        context.Response.Headers.Add("X-Frame-Options", "DENY");
        
        // Prevent MIME-sniffing
        context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
        
        // Enable XSS protection
        context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
        
        // Referrer policy
        context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
        
        // Content Security Policy
        context.Response.Headers.Add("Content-Security-Policy", 
            "default-src 'self'; script-src 'self'");

        await _next(context);
    }
}
```

Add to `Program.cs`:

```csharp
app.UseMiddleware<SecurityHeadersMiddleware>();
```

### Step 10: Enhance HTTPS Enforcement

Update `Program.cs`:

```csharp
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();  // Add HSTS header
}
app.UseHttpsRedirection();
```

---

## Testing

### Test Authentication

```bash
# Get token
curl -X POST http://localhost:5049/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"admin123"}'

# Response:
# {"token":"eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."}

# Use token to access protected endpoint
curl -X POST http://localhost:5049/api/countries \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <token_from_above>" \
  -d '{"id":99,"name":"New Country","shortName":"NC"}'
```

### Test Authorization

```bash
# This should fail (403 Forbidden) if using user token on admin endpoint
curl -X POST http://localhost:5049/api/countries \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer USER_TOKEN" \
  -d '{"id":1,"name":"Test","shortName":"TS"}'
```

### Test Validation

```bash
# This should fail validation
curl -X POST http://localhost:5049/api/countries \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer ADMIN_TOKEN" \
  -d '{"id":1,"name":"","shortName":"TST"}' # ShortName too long, Name empty
```

---

## Verification Checklist

- [ ] JWT authentication configured
- [ ] Authorization attributes added to controllers
- [ ] Input validation enabled
- [ ] AllowedHosts configured
- [ ] Exception handler middleware added
- [ ] Configuration secured
- [ ] Rate limiting enabled
- [ ] CORS policy configured
- [ ] Security headers added
- [ ] HTTPS enforcement enabled
- [ ] All tests passing
- [ ] No secrets in version control
- [ ] Build successful

---

**Status:** Phase 1 & 2 Implementation Guide Complete

*Security Remediation Guide - HotelListing.API*

