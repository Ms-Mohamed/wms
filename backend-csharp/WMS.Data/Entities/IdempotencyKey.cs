namespace WMS.Data.Entities;

/// <summary>Makes a retried POST return the original resource instead of creating a second one.</summary>
public class IdempotencyKey
{
    public string Key { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public int? ResourceId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
