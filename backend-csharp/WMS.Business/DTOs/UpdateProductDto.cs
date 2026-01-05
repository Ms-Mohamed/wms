using System.ComponentModel.DataAnnotations;

namespace WMS.Business.DTOs;

public class UpdateProductDto
{
    [StringLength(50)]
    public string? Code { get; set; }

    [StringLength(200)]
    public string? Name { get; set; }

    public string? Description { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? UnitPrice { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? CostPrice { get; set; }

    [StringLength(20)]
    public string? Unit { get; set; }

    public bool? RequiresLotTracking { get; set; }

    public bool? RequiresSerialTracking { get; set; }

    public int? DefaultLocationId { get; set; }
}

