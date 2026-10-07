# WMS Backend - Core Transactionnel (C# ASP.NET Core)

Service transactionnel principal du système de gestion de stock (WMS).

## Architecture

Le projet suit une architecture en couches (Clean Architecture) :

- **WMS.Data** : Couche d'accès aux données (Entity Framework Core, modèles)
- **WMS.Business** : Couche logique métier (services, DTOs, exceptions)
- **WMS.API** : Couche API (contrôleurs, configuration)

## Prérequis

- .NET 8.0 SDK ou supérieur
- PostgreSQL 14+ installé et en cours d'exécution
- Visual Studio 2022 ou VS Code avec extension C#

## Configuration

Secrets come from environment variables / `appsettings.json` (copy `appsettings.json.example`; never commit the real file):

| Setting | Purpose |
|---|---|
| `ConnectionStrings__DefaultConnection` | PostgreSQL |
| `JwtSettings__SecretKey` | >= 32 chars; the API refuses to start without it |
| `WMS_ADMIN_PASSWORD` | creates the first `admin` user when the table is empty (min 8 chars) |
| `SeedDemoData` | `true` loads the demo catalogue (default: only in Development) |
| `Cors__Origins__0` ... | allowed browser origins |

Migrations are applied automatically at startup (they include `Sql/stock_integrity.sql`).

## Run

```bash
export ConnectionStrings__DefaultConnection="Host=localhost;Database=wms_db;Username=postgres;Password=..."
export JwtSettings__SecretKey="<>=32 random chars>"
export WMS_ADMIN_PASSWORD="<min 8 chars>"
cd WMS.API && dotnet run   # Swagger at /swagger, health at /health
```

Tests: `dotnet test` (needs PostgreSQL; set `WMS_TEST_CONNECTION`).

## Endpoints principaux

### Commandes
- `POST /api/orders` - Create an order (supports Idempotency-Key header; stock is issued on ship)
- `GET /api/orders` - Liste de toutes les commandes
- `GET /api/orders/{id}` - Détails d'une commande

### Factures
- `GET /api/invoices/{orderId}` - Récupérer la facture d'une commande
- `GET /api/invoices/id/{id}` - Récupérer une facture par ID

### Produits
- `GET /api/products` - Liste de tous les produits
- `GET /api/products/{id}` - Détails d'un produit

### Stocks
- `GET /api/stocks` - Liste de tous les stocks
- `GET /api/stocks/product/{productId}/warehouse/{warehouseId}` - Stock spécifique

### Entrepôts
- `GET /api/warehouses` - Liste de tous les entrepôts actifs

## Gestion des erreurs

### InsufficientStockException

Lors de la création d'une commande, si le stock est insuffisant, l'API retourne :

```json
{
  "error": "Stock insuffisant pour le produit PROD-001. Quantité requise: 100, Quantité disponible: 50",
  "productId": 1,
  "productCode": "PROD-001",
  "requiredQuantity": 100,
  "availableQuantity": 50
}
```

## Transactions

La création d'une commande utilise une transaction de base de données pour garantir :
- L'atomicité de l'opération
- Le décrément du stock
- La création de la commande
- La création automatique de la facture

En cas d'erreur, toutes les modifications sont annulées (rollback).

## Structure des données

### Commande (Order)
- Numéro de commande unique (format: CMD-YYYYMMDD-XXXX)
- Informations client
- Statut (Pending, Confirmed, Processing, Shipped, Delivered, Cancelled)
- Totaux (Sous-total, TVA, Total)

### Facture (Invoice)
- Numéro de facture unique (format: FAC-YYYYMMDD-XXXX)
- Liée à une commande
- Statut (Draft, Issued, Paid, Cancelled)
- Détails des lignes de facturation

### Stock
- Quantité disponible
- Quantité réservée
- CUMP (Coût Unitaire Moyen Pondéré)
- Point de commande
- Multi-localisation (Entrepôt + Emplacement)

## Tests

Pour tester l'API, utilisez Swagger UI ou un client HTTP comme Postman.

### Exemple de création de commande

```json
POST /api/orders
{
  "customerName": "Jean Dupont",
  "customerEmail": "jean.dupont@example.com",
  "customerAddress": "123 Rue Example, Paris",
  "taxRate": 0.20,
  "items": [
    {
      "productId": 1,
      "warehouseId": 1,
      "quantity": 2,
      "unitPrice": 899.99,
      "discount": 0
    },
    {
      "productId": 2,
      "warehouseId": 1,
      "quantity": 5,
      "unitPrice": 29.99,
      "discount": 10
    }
  ]
}
```

## Développement

### Migrations Entity Framework

Pour créer une migration :

```bash
cd WMS.API
dotnet ef migrations add NomDeLaMigration --project ../WMS.Data
```

Pour appliquer les migrations :

```bash
dotnet ef database update --project ../WMS.Data
```
