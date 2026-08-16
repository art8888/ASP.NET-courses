namespace HotelListing.API.Extensions;

/// <summary>
/// Extension methods for configuring the HTTP request pipeline middleware.
/// </summary>
public static class MiddlewareExtensions
{
    /// <summary>
    /// Configures development-specific middleware (e.g., OpenAPI documentation).
    /// </summary>
    /// <param name="app">The web application builder.</param>
    /// <returns>The web application for chaining.</returns>
    public static WebApplication UseDevelopmentMiddleware(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        return app;
    }

    /// <summary>
    /// Configures security and routing middleware for the HTTP request pipeline.
    /// </summary>
    /// <param name="app">The web application builder.</param>
    /// <returns>The web application for chaining.</returns>
    public static WebApplication UseSecurityAndRoutingMiddleware(this WebApplication app)
    {
        app.UseHttpsRedirection();
        app.UseAuthorization();
        app.MapControllers();

        return app;
    }
}

