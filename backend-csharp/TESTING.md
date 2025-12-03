# Guide de Test - API WMS

Ce document décrit comment tester l'API C# pour s'assurer qu'elle fonctionne correctement.

## Prérequis

1. PostgreSQL installé et en cours d'exécution
2. Base de données `wms_db` créée (voir `scripts/create-database.sql`)
3. Chaîne de connexion configurée dans `appsettings.json`
4. API démarrée (`dotnet run` dans le dossier `WMS.API`)

## Tests à effectuer

### 1. Vérification de l'initialisation

**Endpoint**: `GET http://localhost:5000/api/products`

**Résultat attendu**: Liste de 5 produits (données de seed)

**Vérifications**:
- ✅ 5 produits retournés
- ✅ Codes produits: PROD-001 à PROD-005
- ✅ Prix unitaires définis

### 2. Vérification des stocks

**Endpoint**: `GET http://localhost:5000/api/stocks`

**Résultat attendu**: Liste des stocks pour chaque produit

**Vérifications**:
- ✅ Stocks initiaux présents
- ✅ Quantités disponibles > 0
- ✅ Points de commande définis

### 3. Test de création de commande (SUCCÈS)

**Endpoint**: `POST http://localhost:5000/api/orders`

**Body**:
```json
{
  "customerName": "Test Client",
  "customerEmail": "test@example.com",
  "customerAddress": "123 Test Street",
  "taxRate": 0.20,
  "items": [
    {
      "productId": 1,
      "warehouseId": 1,
      "quantity": 2,
      "unitPrice": 899.99,
      "discount": 0
    }
  ]
}
```

**Résultat attendu**: 
- ✅ Commande créée avec statut 201
- ✅ Numéro de commande généré (format: CMD-YYYYMMDD-XXXX)
- ✅ Stock décrémenté de 2 unités
- ✅ Facture créée automatiquement

**Vérifications post-création**:
1. `GET http://localhost:5000/api/orders/{orderId}` - Vérifier les détails
2. `GET http://localhost:5000/api/stocks/product/1/warehouse/1` - Vérifier que le stock a diminué
3. `GET http://localhost:5000/api/invoices/{orderId}` - Vérifier la facture

### 4. Test de création de commande (ÉCHEC - Stock insuffisant)

**Endpoint**: `POST http://localhost:5000/api/orders`

**Body**:
```json
{
  "customerName": "Test Client 2",
  "customerEmail": "test2@example.com",
  "taxRate": 0.20,
  "items": [
    {
      "productId": 1,
      "warehouseId": 1,
      "quantity": 1000,
      "unitPrice": 899.99,
      "discount": 0
    }
  ]
}
```

**Résultat attendu**: 
- ✅ Statut 400 (Bad Request)
- ✅ Message d'erreur: "Stock insuffisant..."
- ✅ Détails: productId, productCode, requiredQuantity, availableQuantity
- ✅ Aucune commande créée
- ✅ Stock non modifié (transaction rollback)

### 5. Test de récupération de facture

**Endpoint**: `GET http://localhost:5000/api/invoices/{orderId}`

**Résultat attendu**:
- ✅ Facture retournée avec tous les détails
- ✅ Numéro de facture (format: FAC-YYYYMMDD-XXXX)
- ✅ Totaux corrects (Sous-total, TVA, Total)
- ✅ Lignes de facture correspondant aux lignes de commande

### 6. Test de transaction atomique

**Scénario**: Créer une commande avec plusieurs produits, dont un avec stock insuffisant

**Body**:
```json
{
  "customerName": "Test Client 3",
  "taxRate": 0.20,
  "items": [
    {
      "productId": 1,
      "warehouseId": 1,
      "quantity": 2,
      "unitPrice": 899.99
    },
    {
      "productId": 2,
      "warehouseId": 1,
      "quantity": 10000,
      "unitPrice": 29.99
    }
  ]
}
```

**Résultat attendu**:
- ✅ Erreur retournée
- ✅ Aucune commande créée
- ✅ Aucun stock modifié (même pour le produit 1)
- ✅ Transaction complètement annulée

## Tests avec Swagger UI

1. Accéder à `http://localhost:5000/swagger`
2. Tester chaque endpoint directement depuis l'interface
3. Vérifier les schémas de requête/réponse

## Tests avec cURL (exemples)

### Créer une commande
```bash
curl -X POST "http://localhost:5000/api/orders" \
  -H "Content-Type: application/json" \
  -d '{
    "customerName": "Jean Dupont",
    "customerEmail": "jean@example.com",
    "taxRate": 0.20,
    "items": [
      {
        "productId": 1,
        "warehouseId": 1,
        "quantity": 1,
        "unitPrice": 899.99
      }
    ]
  }'
```

### Récupérer une commande
```bash
curl "http://localhost:5000/api/orders/1"
```

### Récupérer une facture
```bash
curl "http://localhost:5000/api/invoices/1"
```

## Checklist de validation

- [ ] Base de données créée et accessible
- [ ] API démarre sans erreur
- [ ] Données de seed présentes (5 produits, stocks)
- [ ] Création de commande réussie avec stock suffisant
- [ ] Création de commande échoue avec stock insuffisant
- [ ] Transaction atomique fonctionne (rollback en cas d'erreur)
- [ ] Facture créée automatiquement après commande
- [ ] Stock décrémenté correctement
- [ ] Tous les endpoints retournent des réponses valides
- [ ] Swagger UI accessible et fonctionnel

## Dépannage

### Erreur de connexion à PostgreSQL
- Vérifier que PostgreSQL est en cours d'exécution
- Vérifier la chaîne de connexion dans `appsettings.json`
- Vérifier les identifiants (username/password)

### Base de données non créée
- Exécuter le script `scripts/create-database.sql`
- Ou créer manuellement: `CREATE DATABASE wms_db;`

### Erreurs de compilation
- Exécuter `dotnet restore`
- Vérifier que .NET 8.0 SDK est installé
- Vérifier les versions des packages NuGet

### Données de seed absentes
- Vérifier les logs au démarrage
- Vérifier que `SeedData.SeedDatabase()` est appelé dans `Program.cs`
- Supprimer et recréer la base de données si nécessaire

