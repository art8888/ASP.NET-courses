using HotelListing.API.Data;
using HotelListing.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace HotelListing.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class CountriesController : ControllerBase
{
    private readonly ICountryRepository _countryRepository;

    public CountriesController(ICountryRepository countryRepository)
    {
        _countryRepository = countryRepository;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Country>>> GetCountries()
    {
        var countries = await _countryRepository.GetAllAsync();
        return Ok(countries);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Country>> GetCountry(int id)
    {
        var country = await _countryRepository.GetByIdAsync(id);

        if (country == null)
        {
            return NotFound();
        }

        return Ok(country);
    }

    [HttpPost]
    public async Task<ActionResult<Country>> CreateCountry([FromBody] Country country)
    {
        if (country == null)
        {
            return BadRequest();
        }

        if (await _countryRepository.ExistsAsync(country.Id))
        {
            return BadRequest(new { message = "Country already exists" });
        }

        var createdCountry = await _countryRepository.CreateAsync(country);
        return CreatedAtAction(nameof(GetCountry), new { id = createdCountry.Id }, createdCountry);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult> UpdateCountry(int id, [FromBody] Country updatedCountry)
    {
        var result = await _countryRepository.UpdateAsync(id, updatedCountry);

        if (!result)
        {
            return NotFound();
        }


        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult> DeleteCountry(int id)
    {
        var result = await _countryRepository.DeleteAsync(id);

        if (!result)
        {
            return NotFound();
        }

        return NoContent();
    }
}
