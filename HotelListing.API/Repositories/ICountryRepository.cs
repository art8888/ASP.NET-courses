using HotelListing.API.Data;

namespace HotelListing.API.Repositories;

public interface ICountryRepository
{
    Task<IEnumerable<Country>> GetAllAsync();
    Task<Country?> GetByIdAsync(int id);
    Task<Country> CreateAsync(Country country);
    Task<bool> UpdateAsync(int id, Country country);
    Task<bool> DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
}

