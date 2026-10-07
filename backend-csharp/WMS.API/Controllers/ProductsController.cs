using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Authorization;
using WMS.API.Resources;
using WMS.Business.DTOs;
using WMS.Data;
using WMS.Data.Entities;

namespace WMS.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly WmsDbContext _context;
    private readonly ILogger<ProductsController> _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public ProductsController(WmsDbContext context, ILogger<ProductsController> logger, IStringLocalizer<SharedResources> localizer)
    {
        _context = context;
        _logger = logger;
        _localizer = localizer;
    }

    /// <summary>Paged: ?page=1&amp;pageSize=500 (default and max 500). Total in the X-Total-Count header.</summary>
    [HttpGet]
    public async Task<ActionResult> GetAllProducts([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct)
    {
        try
        {
            var (p, size) = Paging.Normalize(page, pageSize);

            var query = _context.Products.AsNoTracking();
            Response.Headers["X-Total-Count"] = (await query.CountAsync(ct)).ToString();

            var products = await query
                .OrderBy(x => x.Name).ThenBy(x => x.Id)
                .Skip((p - 1) * size).Take(size)
                .Select(x => new
                {
                    x.Id,
                    x.Code,
                    x.Name,
                    x.Description,
                    x.UnitPrice,
                    x.CostPrice,
                    x.Unit,
                    x.RequiresLotTracking,
                    x.RequiresSerialTracking,
                    x.DefaultLocationId,
                    StockQuantity = x.Stocks.Sum(st => (decimal?)st.Quantity) ?? 0
                })
                .ToListAsync(ct);

            return Ok(products);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la récupération des produits");
            return StatusCode(500, new { error = "Une erreur est survenue lors de la récupération des produits" });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult> GetProduct(int id)
    {
        var product = await _context.Products
            .Include(p => p.Stocks)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null)
        {
            return NotFound();
        }

        return Ok(new
        {
            product.Id,
            product.Code,
            product.Name,
            product.Description,
            product.UnitPrice,
            product.CostPrice,
            product.Unit,
            product.RequiresLotTracking,
            product.RequiresSerialTracking,
            product.DefaultLocationId,
            StockQuantity = product.Stocks.Sum(s => s.Quantity),
            Stocks = product.Stocks.Select(s => new
            {
                s.WarehouseId,
                s.Quantity,
                s.AvailableQuantity
            })
        });
    }

    [HttpPost]
    public async Task<ActionResult> CreateProduct([FromBody] CreateProductDto createProductDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Vérifier si le code existe déjà
        var existingProduct = await _context.Products
            .FirstOrDefaultAsync(p => p.Code == createProductDto.Code);

        if (existingProduct != null)
        {
            var errorMessage = _localizer["ProductCodeExists"];
            return BadRequest(new { error = errorMessage.Value });
        }

        var product = new Product
        {
            Code = createProductDto.Code,
            Name = createProductDto.Name,
            Description = createProductDto.Description,
            UnitPrice = createProductDto.UnitPrice,
            CostPrice = createProductDto.CostPrice,
            Unit = createProductDto.Unit,
            RequiresLotTracking = createProductDto.RequiresLotTracking,
            RequiresSerialTracking = createProductDto.RequiresSerialTracking,
            DefaultLocationId = createProductDto.DefaultLocationId > 0 ? createProductDto.DefaultLocationId : null,
            CreatedAt = DateTime.UtcNow
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Produit créé: {ProductCode} - {ProductName}", product.Code, product.Name);

        return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, new
        {
            product.Id,
            product.Code,
            product.Name,
            product.Description,
            product.UnitPrice,
            product.CostPrice,
            product.Unit,
            product.RequiresLotTracking,
            product.RequiresSerialTracking,
            product.DefaultLocationId
        });
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> UpdateProduct(int id, [FromBody] UpdateProductDto updateProductDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var product = await _context.Products.FindAsync(id);

        if (product == null)
        {
            return NotFound();
        }

        // Vérifier si le nouveau code existe déjà (si modifié)
        if (!string.IsNullOrEmpty(updateProductDto.Code) && updateProductDto.Code != product.Code)
        {
            var existingProduct = await _context.Products
                .FirstOrDefaultAsync(p => p.Code == updateProductDto.Code && p.Id != id);

            if (existingProduct != null)
            {
                var errorMessage = _localizer["ProductCodeExists"];
                return BadRequest(new { error = errorMessage.Value });
            }
        }

        // Mettre à jour les propriétés
        if (!string.IsNullOrEmpty(updateProductDto.Code))
            product.Code = updateProductDto.Code;
        if (!string.IsNullOrEmpty(updateProductDto.Name))
            product.Name = updateProductDto.Name;
        if (updateProductDto.Description != null)
            product.Description = updateProductDto.Description;
        if (updateProductDto.UnitPrice.HasValue)
            product.UnitPrice = updateProductDto.UnitPrice.Value;
        if (updateProductDto.CostPrice.HasValue)
            product.CostPrice = updateProductDto.CostPrice.Value;
        if (!string.IsNullOrEmpty(updateProductDto.Unit))
            product.Unit = updateProductDto.Unit;
        if (updateProductDto.RequiresLotTracking.HasValue)
            product.RequiresLotTracking = updateProductDto.RequiresLotTracking.Value;
        if (updateProductDto.RequiresSerialTracking.HasValue)
            product.RequiresSerialTracking = updateProductDto.RequiresSerialTracking.Value;
        
        if (updateProductDto.DefaultLocationId.HasValue)
            product.DefaultLocationId = updateProductDto.DefaultLocationId.Value > 0 ? updateProductDto.DefaultLocationId : null;

        product.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Produit mis à jour: {ProductCode} - {ProductName}", product.Code, product.Name);

        return Ok(new
        {
            product.Id,
            product.Code,
            product.Name,
            product.Description,
            product.UnitPrice,
            product.CostPrice,
            product.Unit,
            product.RequiresLotTracking,
            product.RequiresSerialTracking,
            product.DefaultLocationId
        });
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteProduct(int id)
    {
        var product = await _context.Products
            .Include(p => p.OrderItems)
            .Include(p => p.Stocks)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null)
        {
            return NotFound();
        }

        // Vérifier si le produit est utilisé dans des commandes
        if (product.OrderItems.Any())
        {
            var errorMessage = _localizer["ProductInUse"];
            return BadRequest(new { error = errorMessage.Value });
        }

        // Vérifier si le produit a du stock
        if (product.Stocks.Any(s => s.Quantity > 0))
        {
            var errorMessage = _localizer["ProductHasStock"];
            return BadRequest(new { error = errorMessage.Value });
        }

        _context.Products.Remove(product);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Produit supprimé: {ProductCode} - {ProductName}", product.Code, product.Name);

        return NoContent();
    }
}
