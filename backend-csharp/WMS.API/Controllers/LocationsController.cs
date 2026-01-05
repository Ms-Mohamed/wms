using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WMS.Data;
using WMS.Data.Entities;

namespace WMS.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class LocationsController : ControllerBase
{
    private readonly WmsDbContext _context;
    private readonly ILogger<LocationsController> _logger;

    public LocationsController(WmsDbContext context, ILogger<LocationsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // GET: api/Locations
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Location>>> GetLocations()
    {
        return await _context.Locations
            .Include(l => l.Warehouse)
            .Where(l => l.IsActive)
            .OrderBy(l => l.Code)
            .ToListAsync();
    }

    // GET: api/Locations/5
    [HttpGet("{id}")]
    public async Task<ActionResult<Location>> GetLocation(int id)
    {
        var location = await _context.Locations.FindAsync(id);

        if (location == null)
        {
            return NotFound();
        }

        return location;
    }

    // POST: api/Locations
    [HttpPost]
    public async Task<ActionResult<Location>> PostLocation(CreateLocationDto dto)
    {
        // Default to Warehouse 1 for now if not specified, until we have multi-warehouse UI
        int warehouseId = dto.WarehouseId > 0 ? dto.WarehouseId : 1;

        var location = new Location
        {
            Code = dto.Code.ToUpper(),
            Name = dto.Name,
            Zone = dto.Zone,
            WarehouseId = warehouseId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Locations.Add(location);
        await _context.SaveChangesAsync();

        return CreatedAtAction("GetLocation", new { id = location.Id }, location);
    }

    // PUT: api/Locations/5
    [HttpPut("{id}")]
    public async Task<IActionResult> PutLocation(int id, CreateLocationDto dto)
    {
        var location = await _context.Locations.FindAsync(id);
        if (location == null)
        {
            return NotFound();
        }

        // Update fields
        location.Code = dto.Code.ToUpper();
        location.Name = dto.Name;
        location.Zone = dto.Zone;
        location.WarehouseId = dto.WarehouseId > 0 ? dto.WarehouseId : 1;
        // Don't update CreatedAt or IsActive

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!LocationExists(id))
            {
                return NotFound();
            }
            else
            {
                throw;
            }
        }

        return NoContent();
    }

    private bool LocationExists(int id)
    {
        return _context.Locations.Any(e => e.Id == id);
    }

    // DELETE: api/Locations/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteLocation(int id)
    {
        var location = await _context.Locations.FindAsync(id);
        if (location == null)
        {
            return NotFound();
        }

        // Check if stock exists in this location
        var hasStock = await _context.Stocks.AnyAsync(s => s.LocationId == id && s.Quantity > 0);
        if (hasStock)
        {
            return BadRequest("Impossible de supprimer un emplacement contenant du stock.");
        }

        // Soft delete
        location.IsActive = false;
        await _context.SaveChangesAsync();

        return NoContent();
    }
}

public class CreateLocationDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Zone { get; set; }
    public int WarehouseId { get; set; }
}
