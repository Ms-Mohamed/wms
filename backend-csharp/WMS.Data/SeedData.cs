using Microsoft.EntityFrameworkCore;
using WMS.Data.Entities;

namespace WMS.Data;

public static class SeedData
{
    public static void SeedDatabase(WmsDbContext context)
    {
        // Vérifier si des données existent déjà
        if (context.Products.Any())
        {
            return; // Base de données déjà initialisée
        }

        // Créer des entrepôts
        var warehouse1 = new Warehouse
        {
            Code = "WH-001",
            Name = "Entrepôt Principal",
            Address = "123 Rue de l'Industrie",
            City = "Paris",
            Country = "France",
            IsActive = true
        };

        var warehouse2 = new Warehouse
        {
            Code = "WH-002",
            Name = "Entrepôt Secondaire",
            Address = "456 Avenue du Commerce",
            City = "Lyon",
            Country = "France",
            IsActive = true
        };

        context.Warehouses.AddRange(warehouse1, warehouse2);
        context.SaveChanges();

        // Créer des emplacements
        var location1 = new Location
        {
            WarehouseId = warehouse1.Id,
            Code = "A-01-01",
            Name = "Zone A - Rack 01 - Niveau 01",
            Zone = "A",
            IsActive = true
        };

        var location2 = new Location
        {
            WarehouseId = warehouse1.Id,
            Code = "A-01-02",
            Name = "Zone A - Rack 01 - Niveau 02",
            Zone = "A",
            IsActive = true
        };

        context.Locations.AddRange(location1, location2);
        context.SaveChanges();

        // Créer des produits
        var products = new List<Product>
        {
            new Product
            {
                Code = "PROD-001",
                Name = "Ordinateur Portable",
                Description = "Ordinateur portable professionnel 15 pouces",
                UnitPrice = 899.99m,
                CostPrice = 650.00m,
                Unit = "PIECE",
                RequiresLotTracking = false,
                RequiresSerialTracking = true
            },
            new Product
            {
                Code = "PROD-002",
                Name = "Souris Sans Fil",
                Description = "Souris optique sans fil ergonomique",
                UnitPrice = 29.99m,
                CostPrice = 15.00m,
                Unit = "PIECE",
                RequiresLotTracking = false,
                RequiresSerialTracking = false
            },
            new Product
            {
                Code = "PROD-003",
                Name = "Clavier Mécanique",
                Description = "Clavier mécanique RGB rétroéclairé",
                UnitPrice = 129.99m,
                CostPrice = 80.00m,
                Unit = "PIECE",
                RequiresLotTracking = false,
                RequiresSerialTracking = false
            },
            new Product
            {
                Code = "PROD-004",
                Name = "Écran 27 pouces",
                Description = "Écran LED 27 pouces 4K",
                UnitPrice = 399.99m,
                CostPrice = 280.00m,
                Unit = "PIECE",
                RequiresLotTracking = false,
                RequiresSerialTracking = true
            },
            new Product
            {
                Code = "PROD-005",
                Name = "Câble USB-C",
                Description = "Câble USB-C 2 mètres",
                UnitPrice = 19.99m,
                CostPrice = 8.00m,
                Unit = "PIECE",
                RequiresLotTracking = false,
                RequiresSerialTracking = false
            }
        };

        context.Products.AddRange(products);
        context.SaveChanges();

        // Créer des stocks initiaux
        var stocks = new List<Stock>
        {
            new Stock
            {
                ProductId = products[0].Id,
                WarehouseId = warehouse1.Id,
                LocationId = location1.Id,
                Quantity = 50,
                ReservedQuantity = 0,
                AverageCost = 650.00m,
                ReorderPoint = 10,
                LastUpdated = DateTime.UtcNow
            },
            new Stock
            {
                ProductId = products[1].Id,
                WarehouseId = warehouse1.Id,
                LocationId = location2.Id,
                Quantity = 200,
                ReservedQuantity = 0,
                AverageCost = 15.00m,
                ReorderPoint = 50,
                LastUpdated = DateTime.UtcNow
            },
            new Stock
            {
                ProductId = products[2].Id,
                WarehouseId = warehouse1.Id,
                LocationId = location1.Id,
                Quantity = 75,
                ReservedQuantity = 0,
                AverageCost = 80.00m,
                ReorderPoint = 20,
                LastUpdated = DateTime.UtcNow
            },
            new Stock
            {
                ProductId = products[3].Id,
                WarehouseId = warehouse1.Id,
                LocationId = location1.Id,
                Quantity = 30,
                ReservedQuantity = 0,
                AverageCost = 280.00m,
                ReorderPoint = 5,
                LastUpdated = DateTime.UtcNow
            },
            new Stock
            {
                ProductId = products[4].Id,
                WarehouseId = warehouse1.Id,
                LocationId = location1.Id,
                Quantity = 500,
                ReservedQuantity = 0,
                AverageCost = 8.00m,
                ReorderPoint = 100,
                LastUpdated = DateTime.UtcNow
            }
        };

        context.Stocks.AddRange(stocks);
        context.SaveChanges();

        // Créer des mouvements de stock initiaux
        foreach (var stock in stocks)
        {
            var movement = new StockMovement
            {
                StockId = stock.Id,
                Type = MovementType.Inbound,
                Quantity = stock.Quantity,
                UnitCost = stock.AverageCost,
                Reference = "INIT",
                Notes = "Stock initial"
            };
            context.StockMovements.Add(movement);
        }

        context.SaveChanges();
    }
}

