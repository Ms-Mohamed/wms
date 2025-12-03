# 🔧 Correction de la Connexion PostgreSQL

## ❌ Erreur Identifiée

```
Npgsql.PostgresException: 28P01: password authentication failed for user "postgres"
```

**Cause** : Le mot de passe PostgreSQL dans `appsettings.json` ne correspond pas au mot de passe réel de votre installation PostgreSQL.

## ✅ Solution

### Étape 1 : Vérifier/Corriger le Mot de Passe

1. **Ouvrez le fichier** : `backend-csharp/WMS.API/appsettings.json`

2. **Vérifiez la chaîne de connexion** :
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Host=localhost;Port=5432;Database=wms_db;Username=postgres;Password=VOTRE_MOT_DE_PASSE"
     }
   }
   ```

3. **Remplacez `VOTRE_MOT_DE_PASSE`** par le mot de passe réel de votre utilisateur PostgreSQL `postgres`.

### Étape 2 : Vérifier que PostgreSQL est Accessible

Testez la connexion avec psql (si disponible) :
```bash
psql -U postgres -h localhost -d wms_db
```

Ou utilisez pgAdmin pour vérifier les credentials.

### Étape 3 : Créer la Base de Données (si elle n'existe pas)

Si la base de données `wms_db` n'existe pas, créez-la :

```sql
-- Connectez-vous à PostgreSQL en tant que superutilisateur
CREATE DATABASE wms_db
    WITH 
    OWNER = postgres
    ENCODING = 'UTF8'
    LC_COLLATE = 'French_France.1252'
    LC_CTYPE = 'French_France.1252'
    TABLESPACE = pg_default
    CONNECTION LIMIT = -1;
```

### Étape 4 : Appliquer les Migrations

Une fois la chaîne de connexion corrigée, appliquez les migrations :

```powershell
cd backend-csharp\WMS.API
dotnet ef database update --project ..\WMS.Data
```

### Étape 5 : Redémarrer le Backend C#

```powershell
cd backend-csharp\WMS.API
dotnet run
```

## 🔍 Vérification

Une fois corrigé, testez l'API :
- Swagger : http://localhost:5000/swagger
- Endpoint Products : http://localhost:5000/api/products

Si vous voyez les données (ou une liste vide), la connexion fonctionne !

## 📝 Exemple de Chaîne de Connexion Correcte

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=wms_db;Username=postgres;Password=monMotDePasse123"
  }
}
```

**Note** : Ne commitez JAMAIS le fichier `appsettings.json` avec un mot de passe réel en production. Utilisez les User Secrets ou des variables d'environnement.

