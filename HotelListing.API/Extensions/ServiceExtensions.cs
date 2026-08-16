using HotelListing.API.Repositories;

namespace HotelListing.API.Extensions;

/// <summary>
/// Extension methods for registering application services.
/// </summary>
public static class ServiceExtensions
{
    /// <summary>
    /// Adds application repositories and data access services to the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<ICountryRepository, CountryRepository>();
        
        return services;
    }

    /// <summary>
    /// Adds API configuration services including controllers and OpenAPI/Swagger.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddApiConfiguration(this IServiceCollection services)
    {
        services.AddControllers();
        services.AddOpenApi();

        return services;
    }
}

