# 📋 Guide d'Application des Migrations PostgreSQL

## ❌ Erreur Identifiée

**Message d'exception** :
```
Npgsql.PostgresException: 28P01: password authentication failed for user "postgres"
```

**Cause** : Le mot de passe PostgreSQL dans `appsettings.json` ne correspond pas au mot de passe réel.

## ✅ Solution en 3 Étapes

### Étape 1 : Corriger le Mot de Passe PostgreSQL

1. **Ouvrez** : `backend-csharp/WMS.API/appsettings.json`

2. **Modifiez la ligne** :
   ```json
   "DefaultConnection": "Host=localhost;Port=5432;Database=wms_db;Username=postgres;Password=postgres"
   ```
   
   **Remplacez `Password=postgres`** par votre mot de passe PostgreSQL réel.

3. **Sauvegardez** le fichier.

### Étape 2 : Vérifier que la Base de Données Existe

Si la base `wms_db` n'existe pas, créez-la :

**Option A - Via psql** :
```bash
psql -U postgres
CREATE DATABASE wms_db;
\q
```

**Option B - Via pgAdmin** :
- Ouvrez pgAdmin
- Clic droit sur "Databases" → "Create" → "Database"
- Nom : `wms_db`
- Owner : `postgres`

### Étape 3 : Appliquer les Migrations

Une fois le mot de passe corrigé, exécutez :

```powershell
cd backend-csharp\WMS.API
dotnet ef database update --project ..\WMS.Data
```

**Résultat attendu** :
```
Applying migration '20251203194054_InitialCreate'.
Done.
```

## 🔍 Vérification

Après l'application des migrations, redémarrez le Backend C# :

```powershell
cd backend-csharp\WMS.API
dotnet run
```

Puis testez :
- Swagger : http://localhost:5000/swagger
- GET /api/products : Devrait retourner une liste (vide ou avec données)

## 📝 Exemple de Chaîne de Connexion

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=wms_db;Username=postgres;Password=MonMotDePasse123"
  }
}
```

## 🐛 Si l'Erreur Persiste

1. **Vérifiez que PostgreSQL est démarré** :
   ```powershell
   Get-Service -Name postgresql*
   ```

2. **Vérifiez les credentials** :
   - Utilisateur : `postgres`
   - Mot de passe : Votre mot de passe PostgreSQL
   - Port : `5432` (par défaut)
   - Base de données : `wms_db` (doit exister)

3. **Testez la connexion manuellement** :
   ```bash
   psql -U postgres -h localhost -d wms_db
   ```

4. **Vérifiez les logs du Backend C#** pour d'autres erreurs.

