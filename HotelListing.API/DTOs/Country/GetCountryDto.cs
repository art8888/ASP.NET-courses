using HotelListing.API.DTOs.Hotel;

namespace HotelListing.API.DTOs.Country;

public record GetCountryDto(
    int CountryId, 
    string Name, 
    string ShortName,
    List<GetHotelsSlimDto>? Hotels
    );