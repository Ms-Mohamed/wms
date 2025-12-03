# Guide de Validation - Test Critique d'Intégrité

## 🚀 Démarrage des Services

### Ordre de Démarrage Recommandé

1. **Backend C# (Core Transactionnel)** - Port 5000
2. **Backend Python (Service Analytique)** - Port 8000
3. **Frontend React** - Port 3000

### Commandes de Démarrage

#### Option 1 : Scripts PowerShell (Windows)

Ouvrez **3 terminaux PowerShell séparés** et exécutez :

**Terminal 1 - Backend C#:**
```powershell
.\start-backend-csharp.ps1
```

**Terminal 2 - Backend Python:**
```powershell
.\start-backend-python.ps1
```

**Terminal 3 - Frontend React:**
```powershell
.\start-frontend.ps1
```

#### Option 2 : Commandes Manuelles

**Terminal 1 - Backend C#:**
```powershell
cd backend-csharp\WMS.API
dotnet run
```

**Terminal 2 - Backend Python:**
```powershell
cd backend-python
python -m uvicorn main:app --reload --port 8000
```

**Terminal 3 - Frontend React:**
```powershell
cd frontend
npm run dev
```

### Vérification des Services

Une fois démarrés, vérifiez que les services sont accessibles :

- ✅ Backend C# : http://localhost:5000/swagger
- ✅ Backend Python : http://localhost:8000/docs
- ✅ Frontend React : http://localhost:3000

## 🧪 Test Critique d'Intégrité Transactionnelle

### Objectif
Valider que l'intégrité du stock est garantie lors de la création d'une commande avec stock insuffisant.

### Prérequis
- Les 3 services doivent être démarrés
- PostgreSQL doit être en cours d'exécution
- Base de données `wms_db` doit exister et être initialisée

### Étapes du Test

#### 1. Préparation - Création d'un Produit avec Stock Limité

1. Ouvrez le Frontend : http://localhost:3000
2. Naviguez vers **Produits** (menu latéral)
3. Cliquez sur **Créer un produit**
4. Remplissez le formulaire :
   - **Code** : `TEST-001`
   - **Nom** : `Produit Test Stock`
   - **Description** : `Produit pour test d'intégrité`
   - **Prix unitaire** : `10.00`
   - **Prix de revient** : `5.00`
   - **Unité** : `PIECE`
5. Cliquez sur **Enregistrer**

6. **Important** : Créez un stock initial pour ce produit
   - Vous pouvez le faire via l'API directement ou via une requête SQL :
   ```sql
   -- Trouver l'ID du produit créé
   SELECT id FROM products WHERE code = 'TEST-001';
   
   -- Créer un stock de 5 unités (remplacer PRODUCT_ID et WAREHOUSE_ID)
   INSERT INTO stocks (product_id, warehouse_id, quantity, reserved_quantity, average_cost, reorder_point, last_updated)
   VALUES (PRODUCT_ID, 1, 5, 0, 5.00, 2, NOW());
   ```

#### 2. Test Critique - Tentative de Commande avec Stock Insuffisant

1. Naviguez vers **Commandes** (menu latéral)
2. Cliquez sur **Créer une commande**
3. Remplissez le formulaire de commande :
   - **Nom du client** : `Client Test`
   - **Email** : `test@example.com`
   - **Adresse** : `123 Test Street`
4. Cliquez sur **Ajouter un article**
5. Sélectionnez le produit **TEST-001**
6. Sélectionnez un entrepôt (généralement "Entrepôt Principal")
7. **Définissez la quantité à 10** (supérieure au stock disponible de 5)
8. Cliquez sur **Créer la commande**

#### 3. Résultat Attendu ✅

**Comportement Correct :**
- ❌ La commande **NE DOIT PAS** être créée
- ✅ Le Frontend doit afficher un message d'erreur : **"Stock insuffisant pour le produit TEST-001. Quantité requise: 10, Quantité disponible: 5"**
- ✅ Le stock du produit dans la base de données **DOIT RESTER À 5** (non modifié)

**Vérification dans la Base de Données :**
```sql
-- Vérifier que le stock n'a pas changé
SELECT p.code, p.name, s.quantity, s.reserved_quantity
FROM products p
JOIN stocks s ON p.id = s.product_id
WHERE p.code = 'TEST-001';
```

Le résultat doit montrer :
- `quantity = 5` (inchangé)
- `reserved_quantity = 0` (inchangé)

**Vérification des Commandes :**
```sql
-- Vérifier qu'aucune commande n'a été créée pour ce test
SELECT * FROM orders WHERE customer_name = 'Client Test';
```

Aucune commande ne doit exister.

### 4. Test de Succès - Commande avec Stock Suffisant

Pour valider que le système fonctionne correctement :

1. Créez une nouvelle commande avec une quantité de **3** (inférieure au stock de 5)
2. La commande **DOIT** être créée avec succès
3. Le stock **DOIT** être décrémenté à **2** (5 - 3 = 2)
4. Une facture **DOIT** être générée automatiquement

**Vérification :**
```sql
-- Vérifier que le stock a été décrémenté
SELECT p.code, s.quantity 
FROM products p
JOIN stocks s ON p.id = s.product_id
WHERE p.code = 'TEST-001';
-- Résultat attendu: quantity = 2
```

## 📊 Test de la Page Analytique

Une fois le test d'intégrité réussi :

1. Naviguez vers **Analytique** (menu latéral)
2. Sélectionnez un produit dans le menu déroulant
3. **Vérifications** :
   - ✅ Les statistiques d'optimisation s'affichent (Point de commande, QEC, etc.)
   - ✅ Le graphique de prévision des 3 prochains mois s'affiche
   - ✅ Les données proviennent du Backend Python (port 8000)

## ✅ Checklist de Validation

- [ ] Backend C# démarré sur le port 5000
- [ ] Backend Python démarré sur le port 8000
- [ ] Frontend React démarré sur le port 3000
- [ ] Produit de test créé avec stock de 5 unités
- [ ] Tentative de commande de 10 unités échoue avec message d'erreur
- [ ] Stock reste à 5 dans la base de données
- [ ] Aucune commande créée pour le test d'échec
- [ ] Commande de 3 unités réussit
- [ ] Stock décrémenté à 2 après commande réussie
- [ ] Page Analytique affiche les données correctement
- [ ] Graphiques de prévision fonctionnent

## 🐛 Dépannage

### Backend C# ne démarre pas
- Vérifiez que PostgreSQL est en cours d'exécution
- Vérifiez la chaîne de connexion dans `appsettings.json`
- Vérifiez que le port 5000 n'est pas utilisé

### Backend Python ne démarre pas
- Vérifiez que Python 3.10+ est installé
- Installez les dépendances : `pip install -r requirements.txt`
- Vérifiez que le port 8000 n'est pas utilisé

### Frontend ne démarre pas
- Installez les dépendances : `npm install`
- Vérifiez que le port 3000 n'est pas utilisé
- Vérifiez que les backends sont démarrés

### Erreur CORS
- Vérifiez que les backends autorisent les requêtes depuis `http://localhost:3000`
- Vérifiez la configuration CORS dans les backends

### Stock ne se décrémente pas
- Vérifiez les logs du Backend C# pour les erreurs
- Vérifiez que la transaction est bien commitée
- Vérifiez les mouvements de stock dans la table `stock_movements`

