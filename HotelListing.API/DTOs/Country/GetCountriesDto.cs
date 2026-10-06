namespace HotelListing.API.DTOs.Country;

public record GetCountriesDto(
    int CountryId, 
    string Name, 
    string ShortName);