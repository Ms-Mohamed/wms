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

### 1. Base de données PostgreSQL

Assurez-vous que PostgreSQL est installé et en cours d'exécution.

Créez une base de données :

```sql
CREATE DATABASE wms_db;
```

### 2. Chaîne de connexion

Modifiez le fichier `WMS.API/appsettings.json` avec vos paramètres PostgreSQL :

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=wms_db;Username=postgres;Password=votre_mot_de_passe"
  }
}
```

### 3. Installation des dépendances

```bash
cd backend-csharp
dotnet restore
```

## Exécution

```bash
cd WMS.API
dotnet run
```

L'API sera accessible sur :
- HTTP: http://localhost:5000
- HTTPS: https://localhost:5001
- Swagger UI: http://localhost:5000/swagger

## Initialisation de la base de données

Lors du premier démarrage, la base de données sera automatiquement créée et initialisée avec des données de test :
- 2 entrepôts
- 2 emplacements
- 5 produits
- Stocks initiaux pour chaque produit

## Endpoints principaux

### Commandes
- `POST /api/orders` - Créer une commande (décrémente automatiquement le stock)
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

## Support

Pour toute question ou problème, consultez la documentation ou contactez l'équipe de développement.

