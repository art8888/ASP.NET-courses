# 🔐 Auth/AuthZ Remediation Guide - HotelListing.API

**Based on:** api-auth-hardening SKILL.md  
**Date:** August 16, 2026  
**Estimated Time:** 14-18 hours  
**Difficulty:** Intermediate

---

## Priority Order for Fixes

1. **FIX #1:** Add JWT authentication middleware (CRITICAL)
2. **FIX #2:** Fix middleware order (CRITICAL)
3. **FIX #3:** Add authorization attributes (CRITICAL)
4. **FIX #4:** Implement identity service (CRITICAL)
5. **FIX #5:** Add authorization policies (HIGH)
6. **FIX #6:** Secure JWT key (HIGH)
7. **FIX #7:** Add security logging (HIGH)

---

## FIX #1: Add JWT Authentication Middleware

### Step 1.1: Add NuGet Packages

```bash
cd /home/arturas/Desktop/works/dotNet/HotelListing.API/.net-learnings/HotelListing.API

dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add package System.IdentityModel.Tokens.Jwt
```

### Step 1.2: Create JWT Configuration

Create: `Config/JwtSettings.cs`

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

### Step 1.3: Update appsettings.json

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
  }
}
```

### Step 1.4: Add Authentication Extension

Update: `Extensions/ServiceExtensions.cs`

```csharp
// Add at the top of the file
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using HotelListing.API.Config;

// Add this new method to ServiceExtensions class
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
                ClockSkew = TimeSpan.Zero  // Be strict with expiration
            };

            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    var logger = context.HttpContext.RequestServices
                        .GetRequiredService<ILogger<object>>();
                    logger.LogWarning("Authentication failed: {Message}",
                        context.Exception?.Message);
                    return Task.CompletedTask;
                }
            };
        });

    return services;
}
```

### Step 1.5: Register in Program.cs

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add this line FIRST
builder.Services.AddAuthenticationServices(builder.Configuration);

// Then add other services
builder.Services
    .AddApplicationServices()
    .AddApiConfiguration();
```

---

## FIX #2: Fix Middleware Order (CRITICAL!)

Update: `Extensions/MiddlewareExtensions.cs`

**REPLACE:**
```csharp
public static WebApplication UseSecurityAndRoutingMiddleware(this WebApplication app)
{
    app.UseHttpsRedirection();
    app.UseAuthorization();
    app.MapControllers();

    return app;
}
```

**WITH:**
```csharp
public static WebApplication UseSecurityAndRoutingMiddleware(this WebApplication app)
{
    app.UseHttpsRedirection();
    app.UseAuthentication();       // ← BEFORE Authorization
    app.UseAuthorization();        // ← AFTER Authentication
    app.MapControllers();

    return app;
}
```

**This is CRITICAL per SKILL.md:** "Ensure authentication middleware runs before authorization middleware"

---

## FIX #3: Add Authorization Attributes to Controllers

### Step 3.1: Update CountriesController.cs

**ADD** to top of file:
```csharp
using Microsoft.AspNetCore.Authorization;
```

**REPLACE** the class declaration:
```csharp
[Route("api/[controller]")]
[ApiController]
[Authorize]  // ← Require auth for all endpoints
public class CountriesController : ControllerBase
{
    // ... existing code ...

    [HttpGet]
    [AllowAnonymous]  // ← Allow public read access
    public async Task<ActionResult<IEnumerable<Country>>> GetCountries()
    { ... }

    [HttpGet("{id:int}")]
    [AllowAnonymous]  // ← Allow public access
    public async Task<ActionResult<Country>> GetCountry(int id)
    { ... }

    [HttpPost]
    [Authorize(Roles = "Admin")]  // ← Only admins
    public async Task<ActionResult<Country>> CreateCountry([FromBody] Country country)
    { ... }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]  // ← Only admins
    public async Task<ActionResult> UpdateCountry(int id, [FromBody] Country updatedCountry)
    { ... }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]  // ← Only admins
    public async Task<ActionResult> DeleteCountry(int id)
    { ... }
}
```

### Step 3.2: Update HotelsControllers.cs

**ADD** to top of file:
```csharp
using Microsoft.AspNetCore.Authorization;
```

**REPLACE** the class declaration and methods with same pattern as CountriesController.

---

## FIX #4: Create Authentication Service

Create: `Services/IAuthenticationService.cs`

```csharp
namespace HotelListing.API.Services;

public interface IAuthenticationService
{
    string GenerateToken(string userId, string username, string role);
    bool ValidateToken(string token);
}
```

Create: `Services/AuthenticationService.cs`

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using HotelListing.API.Config;
using Microsoft.IdentityModel.Tokens;

namespace HotelListing.API.Services;

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

Register in `ServiceExtensions.cs`:

```csharp
// Add to AddAuthenticationServices method
services.AddScoped<IAuthenticationService, AuthenticationService>();
```

---

## FIX #5: Create Auth Controller

Create: `Controllers/AuthController.cs`

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

    public AuthController(IAuthenticationService authenticationService, 
        ILogger<AuthController> logger)
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
        if (string.IsNullOrWhiteSpace(request.Username) || 
            string.IsNullOrWhiteSpace(request.Password))
        {
            _logger.LogWarning("Login attempt with empty credentials");
            return BadRequest(new { message = "Username and password required" });
        }

        // TODO: Replace with actual user validation from database
        // This is demo only
        if (request.Username == "admin" && request.Password == "admin123")
        {
            var token = _authenticationService.GenerateToken("1", request.Username, "Admin");
            _logger.LogInformation("Successful login for: {Username}", request.Username);
            return Ok(new LoginResponse { Token = token });
        }

        _logger.LogWarning("Failed login attempt for: {Username}", request.Username);
        return Unauthorized(new { message = "Invalid credentials" });
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

---

## FIX #6: Add Authorization Policies

Update: `ServiceExtensions.cs` - Add to `AddAuthenticationServices` method:

```csharp
services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("Admin"));

    options.AddPolicy("UserOrAdmin", policy =>
        policy.RequireRole("User", "Admin"));

    options.AddPolicy("CanDeleteCountries", policy =>
        policy.RequireRole("Admin")
              .RequireClaim("CanDeleteCountries", "true"));
});
```

---

## FIX #7: Secure JWT Key (User Secrets)

### For Development:

```bash
# Initialize user secrets
dotnet user-secrets init

# Set JWT key (NEVER commit this!)
dotnet user-secrets set "Jwt:Key" "your-super-secret-key-minimum-32-characters!!!!"

# Verify
dotnet user-secrets list
```

Add to `.gitignore`:
```
user-secrets/
appsettings.Production.json
appsettings.*.local.json
*.env
```

### For Production:

Use environment variables or Key Vault:
```bash
export Jwt__Key="from-azure-keyvault"
export ASPNETCORE_ENVIRONMENT=Production
```

---

## FIX #8: Add Security Event Logging

Update `AuthController.cs` to log all attempts (already in example above).

Add to controllers:
```csharp
[Authorize]
[HttpDelete("{id:int}")]
public async Task<ActionResult> DeleteCountry(int id)
{
    var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    
    _logger.LogWarning("User {UserId} attempting to delete country {CountryId}", 
        userId, id);

    var result = await _countryRepository.DeleteAsync(id);
    
    if (result)
    {
        _logger.LogInformation("Country {CountryId} deleted by user {UserId}", 
            id, userId);
        return NoContent();
    }

    return NotFound();
}
```

---

## Testing the Fixes

### Test 1: Get JWT Token

```bash
curl -X POST http://localhost:5049/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"admin123"}'

# Response:
# {"token":"eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."}
```

### Test 2: Access Protected Endpoint Without Token

```bash
# Should fail (401 Unauthorized)
curl -X POST http://localhost:5049/api/countries \
  -H "Content-Type: application/json" \
  -d '{"id":99,"name":"Test","shortName":"TS"}'
```

### Test 3: Access Protected Endpoint With Token

```bash
# Save token from Test 1
TOKEN="eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."

# Should succeed (201 Created)
curl -X POST http://localhost:5049/api/countries \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"id":99,"name":"Test","shortName":"TS"}'
```

### Test 4: Access Public Endpoint (No Token Needed)

```bash
# Should succeed (200 OK)
curl -X GET http://localhost:5049/api/countries
```

### Test 5: Test with Expired Token

```bash
# Create token and wait, then test
# Should fail (401 Unauthorized - token expired)
```

---

## Verification Checklist

- [ ] JWT authentication middleware added
- [ ] Middleware order is CORRECT (Auth BEFORE AuthZ)
- [ ] Authorization attributes on controllers
- [ ] AuthController created with login endpoint
- [ ] Authentication service implemented
- [ ] Authorization policies configured
- [ ] JWT key secured (User Secrets)
- [ ] Security logging added
- [ ] Build successful
- [ ] All tests passing
- [ ] No secrets in appsettings.json
- [ ] .gitignore updated

---

## SKILL.md Compliance After Fixes

```
✓ Authentication middleware runs before authorization middleware
✓ JWT validation remains strict
✓ Admin endpoints protected
✓ User identity available
✓ Secrets out of source control
✓ Security events logged
✓ No passwords exposed in logs
```

---

**Estimated Time:** 14-18 hours total

*Authentication and Authorization Remediation Guide*

