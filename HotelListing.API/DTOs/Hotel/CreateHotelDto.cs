using System.ComponentModel.DataAnnotations;

namespace HotelListing.API.DTOs.Hotel;

public class CreateHotelDto
{
    [Required]
    public required string Name { get; set;  }
    
    [MaxLength(150)]
    public required string Address { get; set;  }
    
    [Range(1, 5)]
    public double Rating { get; set;  }
    
    [Required]
    public string CountryId { get; set;  }
}