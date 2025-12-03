# Guide de Démarrage - Services WMS

## 🚀 Démarrage des Services dans l'Ordre

### Prérequis
- ✅ PostgreSQL en cours d'exécution
- ✅ Base de données `wms_db` créée
- ✅ .NET 8.0 SDK installé
- ✅ Python 3.10+ installé
- ✅ Node.js 18+ installé

---

## 1️⃣ Backend C# (Core Transactionnel) - Port 5000

**Ouvrez un terminal PowerShell et exécutez :**

```powershell
cd backend-csharp\WMS.API
dotnet run
```

**Ou utilisez le script :**
```powershell
.\start-backend-csharp.ps1
```

**Vérification :**
- ✅ Swagger UI accessible : http://localhost:5000/swagger
- ✅ API répond : http://localhost:5000/api/products

**Logs attendus :**
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
```

---

## 2️⃣ Backend Python (Service Analytique) - Port 8000

**Ouvrez un NOUVEAU terminal PowerShell et exécutez :**

```powershell
cd backend-python
python -m uvicorn main:app --reload --port 8000
```

**Ou utilisez le script :**
```powershell
.\start-backend-python.ps1
```

**Vérification :**
- ✅ Documentation accessible : http://localhost:8000/docs
- ✅ API répond : http://localhost:8000/

**Logs attendus :**
```
INFO:     Uvicorn running on http://0.0.0.0:8000
```

---

## 3️⃣ Frontend React - Port 3000

**Ouvrez un NOUVEAU terminal PowerShell et exécutez :**

```powershell
cd frontend
npm install  # Si pas encore fait
npm run dev
```

**Ou utilisez le script :**
```powershell
.\start-frontend.ps1
```

**Vérification :**
- ✅ Application accessible : http://localhost:3000
- ✅ Interface utilisateur s'affiche

**Logs attendus :**
```
  VITE v7.x.x  ready in xxx ms

  ➜  Local:   http://localhost:3000/
```

---

## ✅ Vérification Complète

Une fois les 3 services démarrés, vérifiez :

| Service | URL | Statut |
|---------|-----|--------|
| Backend C# | http://localhost:5000/swagger | ✅ |
| Backend Python | http://localhost:8000/docs | ✅ |
| Frontend React | http://localhost:3000 | ✅ |

---

## 🧪 Test Critique d'Intégrité - Instructions Détaillées

### Étape 1 : Préparer le Test

1. **Ouvrez le Frontend** : http://localhost:3000
2. **Créez un produit de test** :
   - Menu : **Produits**
   - Cliquez : **Créer un produit**
   - Remplissez :
     - Code : `TEST-001`
     - Nom : `Produit Test`
     - Prix unitaire : `10.00`
     - Prix de revient : `5.00`
   - Cliquez : **Enregistrer**

3. **Créez un stock initial de 5 unités** :

   **Option A - Via SQL (Recommandé) :**
   ```sql
   -- Trouver l'ID du produit
   SELECT id FROM products WHERE code = 'TEST-001';
   
   -- Créer le stock (remplacer PRODUCT_ID par l'ID trouvé)
   INSERT INTO stocks (product_id, warehouse_id, quantity, reserved_quantity, average_cost, reorder_point, last_updated)
   VALUES (PRODUCT_ID, 1, 5, 0, 5.00, 2, NOW());
   ```

   **Option B - Via Swagger UI :**
   - Accédez à http://localhost:5000/swagger
   - Utilisez l'endpoint POST `/api/stocks` (si disponible)

### Étape 2 : Test d'Intégrité Transactionnelle

1. **Créez une commande avec stock insuffisant** :
   - Menu : **Commandes**
   - Cliquez : **Créer une commande**
   - Remplissez :
     - Nom du client : `Client Test`
     - Email : `test@example.com`
   - Cliquez : **Ajouter un article**
   - Sélectionnez : Produit `TEST-001`
   - Sélectionnez : Entrepôt (généralement le premier)
   - **Quantité : 10** (supérieure au stock de 5)
   - Cliquez : **Créer la commande**

### Étape 3 : Vérification du Résultat

**✅ Comportement Attendu :**

1. **Frontend** :
   - ❌ La commande NE DOIT PAS être créée
   - ✅ Un message d'erreur s'affiche :
     ```
     Stock insuffisant pour le produit TEST-001. 
     Quantité requise: 10, Quantité disponible: 5
     ```

2. **Base de Données** :
   ```sql
   -- Vérifier que le stock n'a pas changé
   SELECT p.code, s.quantity 
   FROM products p
   JOIN stocks s ON p.id = s.product_id
   WHERE p.code = 'TEST-001';
   ```
   **Résultat attendu :** `quantity = 5` (inchangé)

3. **Vérifier qu'aucune commande n'a été créée** :
   ```sql
   SELECT * FROM orders WHERE customer_name = 'Client Test';
   ```
   **Résultat attendu :** Aucune ligne

### Étape 4 : Test de Succès (Validation Positive)

1. **Créez une commande avec stock suffisant** :
   - Même processus mais avec **Quantité : 3** (inférieure à 5)
   - La commande DOIT être créée avec succès
   - Vous serez redirigé vers la page de facture

2. **Vérifiez le stock** :
   ```sql
   SELECT p.code, s.quantity 
   FROM products p
   JOIN stocks s ON p.id = s.product_id
   WHERE p.code = 'TEST-001';
   ```
   **Résultat attendu :** `quantity = 2` (5 - 3 = 2)

---

## 📊 Test de la Page Analytique

Une fois le test d'intégrité réussi :

1. **Naviguez vers Analytique** (menu latéral)
2. **Sélectionnez un produit** dans le menu déroulant
3. **Vérifiez** :
   - ✅ Les statistiques d'optimisation s'affichent
   - ✅ Le graphique de prévision des 3 prochains mois s'affiche
   - ✅ Les données proviennent du Backend Python

---

## 🐛 Dépannage Rapide

### Port déjà utilisé
```powershell
# Trouver le processus utilisant le port
netstat -ano | findstr :5000
netstat -ano | findstr :8000
netstat -ano | findstr :3000

# Tuer le processus (remplacer PID)
taskkill /PID <PID> /F
```

### Erreur de connexion PostgreSQL
- Vérifiez que PostgreSQL est démarré
- Vérifiez les credentials dans `appsettings.json` (Backend C#)
- Vérifiez les variables d'environnement dans `.env` (Backend Python)

### Erreur CORS
- Vérifiez que les backends autorisent `http://localhost:3000`
- Vérifiez la configuration CORS dans les fichiers `Program.cs` et `main.py`

---

## ✅ Checklist de Validation Finale

- [ ] Backend C# démarré et accessible sur port 5000
- [ ] Backend Python démarré et accessible sur port 8000
- [ ] Frontend React démarré et accessible sur port 3000
- [ ] Produit de test créé avec stock de 5 unités
- [ ] Tentative de commande de 10 unités échoue avec erreur
- [ ] Stock reste à 5 dans la base de données (intégrité préservée)
- [ ] Commande de 3 unités réussit
- [ ] Stock décrémenté à 2 après commande réussie
- [ ] Page Analytique fonctionne avec graphiques

**🎉 Si tous les tests passent, le système WMS est validé et prêt pour la production !**

