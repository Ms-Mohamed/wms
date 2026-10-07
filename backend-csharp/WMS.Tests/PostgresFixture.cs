using Microsoft.EntityFrameworkCore;
using Npgsql;
using WMS.Business.Services;
using WMS.Data;
using WMS.Data.Entities;
using Xunit;

namespace WMS.Tests;

/// <summary>
/// One throw-away PostgreSQL database per test class, created by running the REAL EF migrations
/// (so the integrity layer in Sql/stock_integrity.sql is exactly what production gets).
/// Point it at a server with WMS_TEST_CONNECTION, e.g.
///   Host=localhost;Port=5432;Username=postgres;Password=...;Database=postgres
/// </summary>
public class PostgresFixture : IAsyncLifetime
{
    private string _adminConnection = "";
    private string _dbName = "";
    public string ConnectionString { get; private set; } = "";

    public async Task InitializeAsync()
    {
        _adminConnection = Environment.GetEnvironmentVariable("WMS_TEST_CONNECTION")
            ?? "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=postgres";
        _dbName = "wms_test_" + Guid.NewGuid().ToString("N")[..10];

        await using (var admin = new NpgsqlConnection(_adminConnection))
        {
            await admin.OpenAsync();
            await using var cmd = new NpgsqlCommand($"CREATE DATABASE \"{_dbName}\"", admin);
            await cmd.ExecuteNonQueryAsync();
        }

        var csb = new NpgsqlConnectionStringBuilder(_adminConnection) { Database = _dbName, MaxPoolSize = 200, IncludeErrorDetail = true };
        ConnectionString = csb.ConnectionString;

        await using var db = NewContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection(_adminConnection);
        await admin.OpenAsync();
        await using var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_dbName}\" WITH (FORCE)", admin);
        await cmd.ExecuteNonQueryAsync();
    }

    public WmsDbContext NewContext()
        => new(new DbContextOptionsBuilder<WmsDbContext>().UseNpgsql(ConnectionString).Options);

    /// <summary>A fresh context + services, as one HTTP request would get.</summary>
    public Scope NewScope()
    {
        var db = NewContext();
        var ledger = new StockLedger(db);
        return new Scope(db, ledger, new OrderService(db, ledger));
    }

    public record Scope(WmsDbContext Db, StockLedger Ledger, OrderService Orders) : IDisposable
    {
        public void Dispose() => Db.Dispose();
    }

    // ---- data helpers -------------------------------------------------------------------------
    public async Task<(int productId, int warehouseId, int locationId, int stockId)> SeedStockAsync(decimal quantity, decimal unitCost = 10m)
    {
        using var s = NewScope();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var warehouse = new Warehouse { Code = "W" + tag, Name = "WH " + tag };
        s.Db.Warehouses.Add(warehouse);
        await s.Db.SaveChangesAsync();
        var location = new Location { WarehouseId = warehouse.Id, Code = "L" + tag, Name = "Loc " + tag };
        var product = new Product { Code = "P" + tag, Name = "Product " + tag, UnitPrice = 25m, CostPrice = unitCost };
        s.Db.AddRange(location, product);
        await s.Db.SaveChangesAsync();

        var stockId = await s.Ledger.GetOrCreateStockIdAsync(product.Id, warehouse.Id, location.Id);
        if (quantity > 0)
            await s.Ledger.ReceiveAsync(stockId, quantity, unitCost, MovementType.Inbound, "SEED", null);
        return (product.Id, warehouse.Id, location.Id, stockId);
    }

    public async Task<decimal> QuantityAsync(int stockId)
    {
        await using var db = NewContext();
        return await db.Stocks.AsNoTracking().Where(s => s.Id == stockId).Select(s => s.Quantity).SingleAsync();
    }

    public async Task<int> DriftRowsAsync()
    {
        await using var db = NewContext();
        return (await db.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM wms_stock_drift").ToListAsync())[0];
    }
}
