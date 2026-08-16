# Extension Methods Best Practices Guide

**For:** HotelListing.API Project  
**Date:** August 16, 2026  
**Purpose:** Guidelines for extending the refactored startup configuration

---

## 📋 Quick Reference

### Current Extension Methods Structure

```
Extensions/
├── ServiceExtensions.cs
│   ├── AddApplicationServices()      // Repositories
│   └── AddApiConfiguration()          // Controllers, OpenAPI
└── MiddlewareExtensions.cs
    ├── UseDevelopmentMiddleware()     // Dev-only features
    └── UseSecurityAndRoutingMiddleware() // HTTPS, Auth, Routes
```

---

## 🎯 When to Create New Extension Methods

### Rule 1: Create a New Extension When Adding a Feature Area

**Do this:** Create a new extension method for each major feature
```csharp
// Add to ServiceExtensions.cs
public static IServiceCollection AddAuthenticationServices(
    this IServiceCollection services, IConfiguration configuration)
{
    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options => /* config */);
    services.AddAuthorization();
    return services;
}

// In Program.cs
builder.Services
    .AddApplicationServices()
    .AddAuthenticationServices(builder.Configuration) // NEW
    .AddApiConfiguration();
```

### Rule 2: Group Related Services Together

**Do this:** Related services in one method
```csharp
// GOOD: Grouped by concern
public static IServiceCollection AddDatabaseServices(
    this IServiceCollection services, IConfiguration configuration)
{
    services.AddDbContext<HotelListingDbContext>();
    services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
    services.AddUnitOfWork();
    return services;
}

// AVOID: Scattered across methods
services.AddDbContext();
// Later...
services.AddRepositories();
// Later...
services.AddUnitOfWork();
```

### Rule 3: Environment-Specific Configuration

**Do this:** Encapsulate environment checks
```csharp
// In MiddlewareExtensions.cs
public static WebApplication UseLoggingMiddleware(this WebApplication app)
{
    if (app.Environment.IsDevelopment())
    {
        app.UseSerilogRequestLogging();
    }
    return app;
}

// In Program.cs
app.UseLoggingMiddleware();
```

---

## 📂 File Organization Guide

### ServiceExtensions.cs - Add By Category

**Current:**
```csharp
public static class ServiceExtensions
{
    public static IServiceCollection AddApplicationServices(...)
    public static IServiceCollection AddApiConfiguration(...)
}
```

**When you need authentication (add this):**
```csharp
public static class ServiceExtensions
{
    public static IServiceCollection AddApplicationServices(...)
    
    public static IServiceCollection AddAuthenticationServices(...)  // NEW
    
    public static IServiceCollection AddApiConfiguration(...)
}
```

**Suggested order of methods:**
1. `AddApplicationServices()` - Core business logic (repositories)
2. `AddDatabaseServices()` - Database and ORM (when needed)
3. `AddAuthenticationServices()` - Auth and authorization (when needed)
4. `AddLoggingServices()` - Logging framework (when needed)
5. `AddValidationServices()` - Input validation (when needed)
6. `AddApiConfiguration()` - API-specific (controllers, Swagger)

### MiddlewareExtensions.cs - Add By Priority

**Current:**
```csharp
public static class MiddlewareExtensions
{
    public static WebApplication UseDevelopmentMiddleware(...)
    public static WebApplication UseSecurityAndRoutingMiddleware(...)
}
```

**Middleware order matters! Follow this sequence:**

```csharp
// CORRECT ORDER (Security -> Routing)
public static WebApplication ConfigurePipeline(this WebApplication app)
{
    app
        .UseLoggingAndErrorHandling()           // 1. Error handling FIRST
        .UseDevelopmentMiddleware()              // 2. Dev features
        .UseSecurityAndRoutingMiddleware();      // 3. Security & routing
    return app;
}

// DON'T MIX: Don't do authentication AFTER routing
```

---

## 💡 Common Extension Examples

### Example 1: Adding Database Services

Create new file or add to `ServiceExtensions.cs`:

```csharp
/// <summary>
/// Adds Entity Framework Core and database services.
/// </summary>
public static IServiceCollection AddDatabaseServices(
    this IServiceCollection services, IConfiguration configuration)
{
    services.AddDbContext<HotelListingDbContext>(options =>
    {
        options.UseSqlServer(
            configuration.GetConnectionString("DefaultConnection"));
    });

    services.AddScoped(typeof(IRepository<>), typeof(GenericRepository<>));

    return services;
}

// Usage in Program.cs
builder.Services
    .AddApplicationServices()
    .AddDatabaseServices(builder.Configuration)  // NEW
    .AddApiConfiguration();
```

### Example 2: Adding Authentication

Create new file or add to `ServiceExtensions.cs`:

```csharp
/// <summary>
/// Adds JWT authentication and authorization services.
/// </summary>
public static IServiceCollection AddAuthenticationServices(
    this IServiceCollection services, IConfiguration configuration)
{
    var jwtSettings = configuration.GetSection("Jwt");

    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtSettings["Key"]))
            };
        });

    services.AddAuthorization(options =>
    {
        options.AddPolicy("AdminOnly", policy =>
            policy.RequireRole("Admin"));
    });

    return services;
}

// Usage in Program.cs
builder.Services
    .AddApplicationServices()
    .AddAuthenticationServices(builder.Configuration)  // NEW
    .AddApiConfiguration();
```

### Example 3: Adding Logging

Create new file or add to `ServiceExtensions.cs`:

```csharp
/// <summary>
/// Adds Serilog structured logging services.
/// </summary>
public static IServiceCollection AddLoggingServices(
    this IServiceCollection services)
{
    services.AddLogging(configure =>
    {
        configure.ClearProviders();
        configure.AddSerilog();
    });

    return services;
}

// Usage in Program.cs
builder.Host.UseSerilog();

builder.Services
    .AddLoggingServices()  // NEW
    .AddApplicationServices()
    .AddApiConfiguration();
```

### Example 4: Adding Validation

Create new file or add to `ServiceExtensions.cs`:

```csharp
/// <summary>
/// Adds FluentValidation services.
/// </summary>
public static IServiceCollection AddValidationServices(
    this IServiceCollection services)
{
    services.AddValidatorsFromAssemblyContaining<Program>();
    services.AddFluentValidationAutoValidation();

    return services;
}

// Usage in Program.cs
builder.Services
    .AddApplicationServices()
    .AddValidationServices()  // NEW
    .AddApiConfiguration();
```

### Example 5: Adding CORS

Create new file or add to `ServiceExtensions.cs`:

```csharp
/// <summary>
/// Adds Cross-Origin Resource Sharing (CORS) policy.
/// </summary>
public static IServiceCollection AddCorsPolicy(
    this IServiceCollection services, IConfiguration configuration)
{
    var corsOrigins = configuration.GetSection("Cors:AllowedOrigins")
        .Get<string[]>() ?? Array.Empty<string>();

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

// Usage in Program.cs
builder.Services
    .AddApplicationServices()
    .AddCorsPolicy(builder.Configuration)  // NEW
    .AddApiConfiguration();
```

### Example 6: Adding Error Handling Middleware

Add to `MiddlewareExtensions.cs`:

```csharp
/// <summary>
/// Configures error handling and exception middleware.
/// </summary>
public static WebApplication UseErrorHandlingMiddleware(
    this WebApplication app)
{
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }

    return app;
}

// Usage in Program.cs
app
    .UseErrorHandlingMiddleware()  // NEW - ADD FIRST
    .UseDevelopmentMiddleware()
    .UseSecurityAndRoutingMiddleware();
```

---

## 🔍 Naming Conventions

### Service Extension Methods

**Pattern:** `Add[FeatureName]Services` or `Add[FeatureName]`

```csharp
// Good names
.AddApplicationServices()
.AddDatabaseServices()
.AddAuthenticationServices()
.AddValidationServices()
.AddCachingServices()
.AddApiConfiguration()

// Avoid
.AddAllStuff()
.Setup()
.Configure()
.RegisterServices()
```

### Middleware Extension Methods

**Pattern:** `Use[FeatureName]Middleware` or `Use[FeatureName]`

```csharp
// Good names
.UseDevelopmentMiddleware()
.UseSecurityAndRoutingMiddleware()
.UseErrorHandlingMiddleware()
.UseCorsMiddleware()
.UseAuthenticationMiddleware()
.UseLoggingMiddleware()

// Avoid
.ConfigureMiddleware()
.SetupPipeline()
.AddAllMiddleware()
```

---

## 📝 XML Documentation Template

Use this template for all extension methods:

```csharp
/// <summary>
/// Adds [feature name] services to the dependency injection container.
/// </summary>
/// <param name="services">The service collection.</param>
/// <param name="configuration">The application configuration (if needed).</param>
/// <returns>The service collection for chaining.</returns>
/// <example>
/// <code>
/// builder.Services.Add[FeatureName]Services(builder.Configuration);
/// </code>
/// </example>
public static IServiceCollection Add[FeatureName]Services(
    this IServiceCollection services, 
    IConfiguration? configuration = null)
{
    // Implementation
    return services;
}

/// <summary>
/// Configures [feature name] middleware in the HTTP request pipeline.
/// </summary>
/// <param name="app">The web application.</param>
/// <returns>The web application for chaining.</returns>
/// <remarks>
/// This middleware should be registered [when/where in pipeline].
/// </remarks>
/// <example>
/// <code>
/// app.Use[FeatureName]Middleware();
/// </code>
/// </example>
public static WebApplication Use[FeatureName]Middleware(
    this WebApplication app)
{
    // Implementation
    return app;
}
```

---

## ✅ Refactoring Checklist

When you're ready to add a new feature:

- [ ] Identify the feature (e.g., "Authentication")
- [ ] Determine if it's a service or middleware configuration
- [ ] Add method to appropriate extension class
- [ ] Include proper XML documentation
- [ ] Return IServiceCollection or WebApplication for chaining
- [ ] Update Program.cs to call the new method
- [ ] Verify build succeeds: `dotnet build --configuration Release`
- [ ] Test the feature
- [ ] Update this guide if new patterns emerge

---

## 🚫 Anti-Patterns to Avoid

### ❌ Don't: Mix Services and Middleware in One Method
```csharp
// BAD
public static IServiceCollection AddEverything(
    this IServiceCollection services)
{
    services.AddControllers();
    services.AddDbContext();
    // Wrong! This is services, not middleware
}
```

### ❌ Don't: Forget to Return for Chaining
```csharp
// BAD
public static IServiceCollection AddFeature(
    this IServiceCollection services)
{
    services.AddSomething();
    // Missing return!
}

// GOOD
public static IServiceCollection AddFeature(
    this IServiceCollection services)
{
    services.AddSomething();
    return services;  // ✓ Enables chaining
}
```

### ❌ Don't: Violate Middleware Order
```csharp
// BAD: Authentication AFTER routing
app.UseRouting();
app.UseAuthentication();  // Too late!

// GOOD: Authentication BEFORE routing
app.UseAuthentication();
app.UseRouting();
```

### ❌ Don't: Create Too Many Tiny Methods
```csharp
// BAD: Over-abstracted
.AddControllers()
.AddOpenApi()
.AddEndpoints()
.AddHealthChecks()
.AddSwagger()
.AddApiDocumentation()

// GOOD: Grouped logically
.AddApiConfiguration()  // Contains all of above
```

### ❌ Don't: Leave Extensions Undocumented
```csharp
// BAD: No documentation
public static IServiceCollection AddFeature(
    this IServiceCollection services)

// GOOD: Clear documentation
/// <summary>
/// Adds feature services to DI container.
/// </summary>
public static IServiceCollection AddFeature(
    this IServiceCollection services)
```

---

## 📊 Example: Complete Refactored Program.cs

After adding multiple features:

```csharp
using HotelListing.API.Extensions;
using Serilog;

// Configure Serilog first
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/app-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Register all services - organized by feature
    builder.Host.UseSerilog();

    builder.Services
        .AddDatabaseServices(builder.Configuration)
        .AddApplicationServices()
        .AddAuthenticationServices(builder.Configuration)
        .AddValidationServices()
        .AddCorsPolicy(builder.Configuration)
        .AddApiConfiguration(builder.Configuration);

    var app = builder.Build();

    // Configure the HTTP request pipeline - correct middleware order
    app
        .UseErrorHandlingMiddleware()
        .UseDevelopmentMiddleware()
        .UseSecurityAndRoutingMiddleware()
        .UseCorsMiddleware()
        .UseAuthenticationMiddleware()
        .UseLoggingMiddleware();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
```

**Key improvements:**
- ✅ All services grouped into meaningful feature areas
- ✅ Middleware in correct order with comments
- ✅ Easy to enable/disable features
- ✅ Self-documenting through method names
- ✅ Scales to large applications

---

## 🎓 Summary

The refactored startup configuration provides:

✅ **Scalability** - Easy to add new features  
✅ **Maintainability** - Related config grouped together  
✅ **Readability** - Self-documenting method names  
✅ **Testability** - Extension methods can be tested  
✅ **Flexibility** - Enable/disable features easily  

Follow this guide as you extend the application!

---

*Best Practices Guide - HotelListing.API*  
*Created: August 16, 2026*

