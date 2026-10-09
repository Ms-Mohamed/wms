using Microsoft.EntityFrameworkCore;
using WMS.Data.Entities;

namespace WMS.Data;

public class WmsDbContext : DbContext
{
    public WmsDbContext(DbContextOptions<WmsDbContext> options) : base(options)
    {
    }

    public DbSet<Customer> Customers { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Supplier> Suppliers { get; set; }
    public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
    public DbSet<PurchaseOrderItem> PurchaseOrderItems { get; set; }
    public DbSet<ReturnOrder> ReturnOrders { get; set; } // [NEW]
    public DbSet<ReturnOrderItem> ReturnOrderItems { get; set; } // [NEW]
    public DbSet<Product> Products { get; set; }
    public DbSet<Warehouse> Warehouses { get; set; }
    public DbSet<Location> Locations { get; set; }
    public DbSet<Stock> Stocks { get; set; }
    public DbSet<StockMovement> StockMovements { get; set; }
    public DbSet<Lot> Lots { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
    public DbSet<Invoice> Invoices { get; set; }
    public DbSet<InvoiceItem> InvoiceItems { get; set; }
    public DbSet<IdempotencyKey> IdempotencyKeys { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Customer configuration
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.Property(e => e.Code).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
        });

        // Product configuration
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.Property(e => e.Code).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.UnitPrice).HasPrecision(18, 2);
            entity.Property(e => e.CostPrice).HasPrecision(18, 2);
        });

        // Warehouse configuration
        modelBuilder.Entity<Warehouse>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.Property(e => e.Code).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
        });

        // Location configuration
        modelBuilder.Entity<Location>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.WarehouseId, e.Code }).IsUnique();
            entity.Property(e => e.Code).IsRequired().HasMaxLength(50);
            entity.HasOne(e => e.Warehouse)
                .WithMany(w => w.Locations)
                .HasForeignKey(e => e.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Stock configuration
        modelBuilder.Entity<Stock>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ProductId, e.WarehouseId, e.LocationId }).IsUnique();
            entity.Property(e => e.Quantity).HasPrecision(18, 3);
            entity.Property(e => e.ReservedQuantity).HasPrecision(18, 3);
            entity.Ignore(e => e.AvailableQuantity);
            entity.Property(e => e.AverageCost).HasPrecision(18, 4);
            entity.Property(e => e.ReorderPoint).HasPrecision(18, 3);
            entity.HasOne(e => e.Product)
                .WithMany(p => p.Stocks)
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Warehouse)
                .WithMany(w => w.Stocks)
                .HasForeignKey(e => e.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Location)
                .WithMany(l => l.Stocks)
                .HasForeignKey(e => e.LocationId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // StockMovement configuration
        modelBuilder.Entity<StockMovement>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Quantity).HasPrecision(18, 3);
            entity.Property(e => e.Delta).HasPrecision(18, 3);
            entity.Property(e => e.UnitCost).HasPrecision(18, 4);
            entity.HasOne(e => e.Stock)
                .WithMany(s => s.Movements)
                .HasForeignKey(e => e.StockId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Lot configuration
        modelBuilder.Entity<Lot>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.StockId, e.LotNumber, e.SerialNumber });
            entity.Property(e => e.LotNumber).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Quantity).HasPrecision(18, 3);
            entity.HasOne(e => e.Stock)
                .WithMany(s => s.Lots)
                .HasForeignKey(e => e.StockId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Order configuration
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.OrderNumber).IsUnique();
            entity.Property(e => e.OrderNumber).IsRequired().HasMaxLength(50);
            entity.Property(e => e.CustomerName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.SubTotal).HasPrecision(18, 2);
            entity.Property(e => e.TaxRate).HasPrecision(5, 4);
            entity.Property(e => e.TaxAmount).HasPrecision(18, 2);
            entity.Property(e => e.TotalAmount).HasPrecision(18, 2);
            entity.HasOne(e => e.Customer)
                .WithMany(c => c.Orders)
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // OrderItem configuration
        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Quantity).HasPrecision(18, 3);
            entity.Property(e => e.ShippedQuantity).HasPrecision(18, 3);
            entity.Property(e => e.UnitPrice).HasPrecision(18, 2);
            entity.Property(e => e.UnitPriceAtSale).HasPrecision(18, 2);
            entity.Property(e => e.Discount).HasPrecision(18, 2);
            entity.Property(e => e.LineTotal).HasPrecision(18, 2);
            entity.HasOne(e => e.Order)
                .WithMany(o => o.Items)
                .HasForeignKey(e => e.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Product)
                .WithMany(p => p.OrderItems)
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Warehouse)
                .WithMany()
                .HasForeignKey(e => e.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Invoice configuration
        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.InvoiceNumber).IsUnique();
            entity.Property(e => e.InvoiceNumber).IsRequired().HasMaxLength(50);
            entity.Property(e => e.SubTotal).HasPrecision(18, 2);
            entity.Property(e => e.TaxRate).HasPrecision(5, 4);
            entity.Property(e => e.TaxAmount).HasPrecision(18, 2);
            entity.Property(e => e.TotalAmount).HasPrecision(18, 2);
            entity.HasOne(e => e.Order)
                .WithOne(o => o.Invoice)
                .HasForeignKey<Invoice>(e => e.OrderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // InvoiceItem configuration
        modelBuilder.Entity<InvoiceItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ProductName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ProductCode).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Quantity).HasPrecision(18, 3);
            entity.Property(e => e.UnitPrice).HasPrecision(18, 2);
            entity.Property(e => e.Discount).HasPrecision(18, 2);
            entity.Property(e => e.TaxRate).HasPrecision(5, 4);
            entity.Property(e => e.LineTotal).HasPrecision(18, 2);
            entity.HasOne(e => e.Invoice)
                .WithMany(i => i.Items)
                .HasForeignKey(e => e.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Product)
                .WithMany(p => p.InvoiceItems)
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Idempotency keys (table created by Sql/stock_integrity.sql)
        modelBuilder.Entity<IdempotencyKey>(entity =>
        {
            entity.ToTable("IdempotencyKeys");
            entity.HasKey(e => e.Key);
            entity.Property(e => e.Scope).IsRequired();
        });
    }
}

