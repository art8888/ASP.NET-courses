using HotelListing.API.Data;

namespace HotelListing.API.Repositories;

public class CountryRepository : ICountryRepository
{
    private static readonly List<Country> countries = new()
    {
        new Country { Id = 1, Name = "United States", ShortName = "US" },
        new Country { Id = 2, Name = "Canada", ShortName = "CA" },
        new Country { Id = 3, Name = "United Kingdom", ShortName = "UK" }
    };

    public Task<IEnumerable<Country>> GetAllAsync()
    {
        return Task.FromResult(countries.AsEnumerable());
    }

    public Task<Country?> GetByIdAsync(int id)
    {
        var country = countries.FirstOrDefault(c => c.Id == id);
        return Task.FromResult(country);
    }

    public Task<Country> CreateAsync(Country country)
    {
        countries.Add(country);
        return Task.FromResult(country);
    }

    public Task<bool> UpdateAsync(int id, Country updatedCountry)
    {
        var country = countries.FirstOrDefault(c => c.Id == id);

        if (country == null)
        {
            return Task.FromResult(false);
        }

        country.Name = updatedCountry.Name;
        country.ShortName = updatedCountry.ShortName;

        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(int id)
    {
        var country = countries.FirstOrDefault(c => c.Id == id);

        if (country == null)
        {
            return Task.FromResult(false);
        }

        countries.Remove(country);
        return Task.FromResult(true);
    }

    public Task<bool> ExistsAsync(int id)
    {
        return Task.FromResult(countries.Any(c => c.Id == id));
    }
}

