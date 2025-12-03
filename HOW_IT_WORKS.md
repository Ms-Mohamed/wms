# Comment Fonctionne le Projet WMS

## 📋 Vue d'Ensemble

Le projet WMS (Warehouse Management System) est une plateforme de gestion de stock d'entreprise avec trois composants principaux qui travaillent ensemble :

1. **Frontend React** - Interface utilisateur
2. **Backend C#** - Service transactionnel (gestion des commandes, stock, factures)
3. **Backend Python** - Service analytique (prévisions, optimisations)

Tous partagent la même base de données **PostgreSQL**.

---

## 🔄 Flux de Données Global

```
┌─────────────────────────────────────────────────────────────┐
│                    UTILISATEUR                               │
└────────────┬────────────────────────────────────────────────┘
             │
             │ Actions (créer commande, voir analytics, etc.)
             │
┌────────────▼────────────────────────────────────────────────┐
│              FRONTEND REACT (Port 3000)                      │
│  • Interface utilisateur                                     │
│  • Gestion d'état (React Query)                             │
│  • Internationalisation (FR/EN)                             │
│  • Optimisations (memoization, code splitting)              │
└──────┬───────────────────────────────┬───────────────────────┘
       │                               │
       │ Requêtes API                  │ Requêtes API
       │                               │
┌──────▼──────────┐          ┌─────────▼─────────┐
│  BACKEND C#     │          │  BACKEND PYTHON   │
│  Port 5000      │          │  Port 8000        │
│                 │          │                    │
│  • Orders       │          │  • Forecasting    │
│  • Stock        │          │  • Optimization   │
│  • Invoices     │          │  • Analytics      │
│  • Products     │          │                    │
└──────┬──────────┘          └─────────┬─────────┘
       │                                │
       └────────────┬───────────────────┘
                    │
            ┌────────▼────────┐
            │   PostgreSQL    │
            │   Database      │
            │                 │
            │  • Products     │
            │  • Orders       │
            │  • Stock        │
            │  • Analytics    │
            └─────────────────┘
```

---

## 🎯 Scénarios d'Utilisation

### 1. Création d'une Commande Client

#### Flux Complet :

```
1. UTILISATEUR
   └─> Sélectionne des produits et quantités dans le Frontend
       └─> Clique sur "Créer Commande"

2. FRONTEND REACT
   └─> Envoie POST /api/orders au Backend C#
       {
         "customerId": 1,
         "items": [
           {"productId": 5, "quantity": 10},
           {"productId": 3, "quantity": 5}
         ]
       }

3. BACKEND C# (OrdersController)
   └─> Démarre une TRANSACTION de base de données
       └─> Pour chaque produit dans la commande :
           ├─> Vérifie le stock disponible
           ├─> Si stock insuffisant → Lève InsufficientStockException
           └─> Si stock suffisant → Décrémente le stock
       └─> Enregistre la commande (Order)
       └─> Enregistre les lignes de commande (OrderItems)
       └─> Génère automatiquement une facture (Invoice)
       └─> COMMIT la transaction (tout ou rien)

4. POSTGRESQL
   └─> Exécute toutes les opérations atomiquement
       └─> Met à jour les tables :
           • Orders (nouvelle commande)
           • OrderItems (lignes de commande)
           • Stocks (stock décrémenté)
           • Invoices (facture générée)

5. BACKEND C# → FRONTEND
   └─> Retourne la commande créée avec son ID

6. FRONTEND
   └─> Affiche un message de succès
   └─> Met à jour l'affichage du stock en temps réel
```

#### Points Clés :
- ✅ **Transaction atomique** : Si une vérification échoue, tout est annulé
- ✅ **Intégrité des données** : Le stock ne peut jamais être négatif
- ✅ **Génération automatique** : La facture est créée automatiquement

---

### 2. Consultation des Analytics (Prévision de Demande)

#### Flux Complet :

```
1. UTILISATEUR
   └─> Va sur la page /analytics
       └─> Sélectionne un produit

2. FRONTEND REACT
   └─> Envoie GET /api/analytics/predict/5 au Backend Python

3. BACKEND PYTHON (FastAPI)
   └─> Se connecte à PostgreSQL en mode LECTURE SEULE
       └─> Récupère l'historique des commandes du produit :
           SELECT oi."Quantity", o."OrderDate"
           FROM "OrderItems" oi
           JOIN "Orders" o ON oi."OrderId" = o."Id"
           WHERE oi."ProductId" = 5
           AND o."OrderDate" >= NOW() - INTERVAL '3 months'
       
       └─> Traite les données avec Pandas
       └─> Entraîne un modèle de régression linéaire (scikit-learn)
       └─> Prédit la demande pour les 3 prochains mois
       └─> Retourne les prédictions :
           [
             {"mois": "2026-01", "prediction": 55},
             {"mois": "2026-02", "prediction": 62},
             {"mois": "2026-03", "prediction": 58}
           ]

4. FRONTEND
   └─> Reçoit les prédictions
   └─> Affiche un graphique (Recharts)
   └─> Montre les tendances de demande
```

#### Points Clés :
- ✅ **Lecture seule** : Le service Python ne modifie jamais la base de données
- ✅ **Machine Learning** : Utilise scikit-learn pour les prédictions
- ✅ **Visualisation** : Graphiques interactifs dans le Frontend

---

### 3. Optimisation des Achats (Point de Commande, QEC)

#### Flux Complet :

```
1. UTILISATEUR
   └─> Va sur la page /analytics
       └─> Clique sur "Optimiser les achats" pour un produit

2. FRONTEND REACT
   └─> Envoie GET /api/analytics/optimize/5 au Backend Python

3. BACKEND PYTHON
   └─> Se connecte à PostgreSQL en mode LECTURE SEULE
       └─> Récupère les données du produit :
           • Stock actuel
           • Coût unitaire
           • Historique de consommation (90 derniers jours)
       
       └─> Calcule :
           • Demande moyenne quotidienne
           • Stock de sécurité
           • Point de commande (Reorder Point)
           • Quantité Économique de Commande (EOQ)
       
       └─> Retourne :
           {
             "product_id": 5,
             "current_stock": 400.0,
             "reorder_point": 19.44,
             "eoq": 159.21,
             "average_daily_demand": 1.11,
             "safety_stock": 11.67
           }

4. FRONTEND
   └─> Affiche les métriques d'optimisation
   └─> Affiche des alertes si le stock est en dessous du point de commande
```

#### Points Clés :
- ✅ **Calculs complexes** : Formules EOQ et Reorder Point
- ✅ **Recommandations** : Aide à la décision d'achat
- ✅ **Alertes** : Notifications automatiques

---

### 4. Visualisation d'une Facture

#### Flux Complet :

```
1. UTILISATEUR
   └─> Clique sur "Voir Facture" pour une commande

2. FRONTEND REACT
   └─> Envoie GET /api/invoices/{orderId} au Backend C#

3. BACKEND C# (InvoicesController)
   └─> Récupère la commande et ses lignes
       └─> Calcule :
           • Total HT (Hors Taxes)
           • TVA (20%)
           • Total TTC (Toutes Taxes Comprises)
       └─> Retourne la structure complète de la facture

4. FRONTEND
   └─> Affiche la facture dans un format professionnel
   └─> Permet l'impression ou l'export PDF
```

---

## 🔧 Technologies et Interactions

### Frontend React

**Technologies :**
- React 18 avec TypeScript
- React Router pour la navigation
- React Query pour la gestion des données
- Tailwind CSS + Chakra UI pour le design
- Recharts pour les graphiques
- i18next pour l'internationalisation

**Fonctionnalités :**
- Pages : Dashboard, Orders, Products, Stock, Analytics, Invoice
- Optimisations : memoization, code splitting, lazy loading
- Multilingue : Français et Anglais

### Backend C# (ASP.NET Core)

**Technologies :**
- ASP.NET Core 8.0
- Entity Framework Core avec Npgsql
- PostgreSQL comme base de données
- Clean Architecture (Data, Business, API layers)

**Endpoints Principaux :**
- `POST /api/orders` - Créer une commande (avec transaction)
- `GET /api/orders` - Lister les commandes
- `GET /api/invoices/{orderId}` - Obtenir une facture
- `GET /api/products` - Gérer les produits
- `GET /api/stocks` - Voir les stocks

**Fonctionnalités Clés :**
- Transactions atomiques pour l'intégrité
- Gestion des exceptions personnalisées
- Internationalisation des messages d'erreur

### Backend Python (FastAPI)

**Technologies :**
- FastAPI
- Pandas pour la manipulation de données
- Scikit-learn pour le machine learning
- Psycopg2 pour PostgreSQL
- Uvicorn comme serveur ASGI

**Endpoints Principaux :**
- `GET /api/analytics/predict/{productId}` - Prévision de demande
- `GET /api/analytics/optimize/{productId}` - Optimisation des achats
- `GET /api/analytics/alerts` - Alertes de stock

**Fonctionnalités Clés :**
- Mode lecture seule (ne modifie jamais la DB)
- Calculs analytiques complexes
- Modèles ML pour les prédictions

### Base de Données PostgreSQL

**Tables Principales :**
- `Products` - Informations sur les produits
- `Orders` - Commandes clients
- `OrderItems` - Lignes de commande
- `Stocks` - Stock par entrepôt
- `Invoices` - Factures générées
- `Customers` - Clients
- `Warehouses` - Entrepôts
- `Locations` - Emplacements dans les entrepôts

**Relations :**
- Order → OrderItems (1 à plusieurs)
- Order → Invoice (1 à 1)
- Product → Stocks (1 à plusieurs)
- Product → OrderItems (1 à plusieurs)

---

## 🔐 Sécurité et Intégrité

### Intégrité des Données

1. **Transactions Atomiques**
   - Toutes les opérations critiques sont dans des transactions
   - Si une étape échoue, tout est annulé (ROLLBACK)
   - Garantit la cohérence des données

2. **Vérifications de Stock**
   - Vérification avant décrément
   - Exception si stock insuffisant
   - Stock ne peut jamais être négatif

3. **Contraintes de Base de Données**
   - Clés primaires et étrangères
   - Contraintes d'unicité
   - Types de données stricts

### Communication Inter-Services

- **CORS** configuré pour permettre les requêtes depuis le Frontend
- **Pas de communication directe** entre les deux backends
- **Base de données partagée** comme source de vérité unique

---

## 📊 Exemple Concret : Création d'une Commande

### Étape par Étape :

1. **Utilisateur crée une commande**
   ```
   Frontend → POST /api/orders
   {
     "customerId": 1,
     "items": [
       {"productId": 5, "quantity": 10}
     ]
   }
   ```

2. **Backend C# démarre une transaction**
   ```csharp
   using var transaction = await _context.Database.BeginTransactionAsync();
   ```

3. **Vérification du stock**
   ```csharp
   var product = await _context.Products
       .Include(p => p.Stocks)
       .FirstOrDefaultAsync(p => p.Id == productId);
   
   var totalStock = product.Stocks.Sum(s => s.Quantity);
   
   if (totalStock < quantity) {
       throw new InsufficientStockException();
   }
   ```

4. **Décrément du stock**
   ```csharp
   stock.Quantity -= quantity;
   await _context.SaveChangesAsync();
   ```

5. **Création de la commande**
   ```csharp
   var order = new Order { ... };
   _context.Orders.Add(order);
   await _context.SaveChangesAsync();
   ```

6. **Génération de la facture**
   ```csharp
   var invoice = new Invoice { OrderId = order.Id, ... };
   _context.Invoices.Add(invoice);
   await _context.SaveChangesAsync();
   ```

7. **Commit de la transaction**
   ```csharp
   await transaction.CommitAsync();
   ```

8. **Retour au Frontend**
   ```json
   {
     "id": 123,
     "orderDate": "2025-12-03",
     "status": "Completed",
     "items": [...]
   }
   ```

---

## 🚀 Démarrage du Projet

### 1. Démarrer PostgreSQL
```bash
# PostgreSQL doit être en cours d'exécution
# Port par défaut : 5432
```

### 2. Démarrer le Backend C#
```powershell
cd backend-csharp/WMS.API
dotnet run
# Port 5000
```

### 3. Démarrer le Backend Python
```powershell
cd backend-python
python -m uvicorn main:app --reload --port 8000
# Port 8000
```

### 4. Démarrer le Frontend
```powershell
cd frontend
npm install
npm run dev
# Port 3000
```

### 5. Accéder à l'Application
```
http://localhost:3000
```

---

## 📈 Flux de Données en Temps Réel

### Mise à Jour du Stock

1. Commande créée → Stock décrémenté dans PostgreSQL
2. Frontend peut recharger les données → Stock mis à jour
3. Page Analytics peut recalculer → Nouvelles recommandations

### Synchronisation

- **Pas de cache partagé** : Chaque service lit directement depuis PostgreSQL
- **Cohérence garantie** : PostgreSQL est la source de vérité unique
- **Pas de synchronisation nécessaire** : Les deux backends lisent la même DB

---

## 🎓 Concepts Clés

### 1. Microservices
Services indépendants qui communiquent via des APIs.

### 2. Polyglot Architecture
Utilisation de différents langages pour différents services.

### 3. Transaction ACID
Atomicité, Cohérence, Isolation, Durabilité.

### 4. Read-Only Service
Le service Python ne modifie jamais la base de données.

### 5. Single Source of Truth
PostgreSQL est la seule source de vérité pour toutes les données.

---

## ✅ Résumé

Le projet fonctionne comme suit :

1. **Frontend React** : Interface utilisateur qui appelle les APIs
2. **Backend C#** : Gère les opérations transactionnelles (commandes, stock)
3. **Backend Python** : Effectue les analyses et prédictions
4. **PostgreSQL** : Base de données partagée, source de vérité unique

Tous les services travaillent ensemble pour fournir une plateforme complète de gestion de stock avec des capacités analytiques avancées.

