namespace HotelListing.API.DTOs.Hotel;

public record GetHotelsDto(
    int Id, 
    string Name, 
    string Address, 
    double Rating, 
    int CountryId
);

public record GetHotelsSlimDto(
    int Id,
    string Name,
    string ShortName
);