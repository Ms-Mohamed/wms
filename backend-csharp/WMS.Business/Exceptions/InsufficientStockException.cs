namespace WMS.Business.Exceptions;

public class InsufficientStockException : Exception
{
    public int ProductId { get; }
    public string ProductCode { get; }
    public decimal RequiredQuantity { get; }
    public decimal AvailableQuantity { get; }

    public InsufficientStockException(int productId, string productCode, decimal requiredQuantity, decimal availableQuantity, string? message = null)
        : base(message ?? $"Stock insuffisant pour le produit {productCode}. Quantité requise: {requiredQuantity}, Quantité disponible: {availableQuantity}")
    {
        ProductId = productId;
        ProductCode = productCode;
        RequiredQuantity = requiredQuantity;
        AvailableQuantity = availableQuantity;
    }
}

