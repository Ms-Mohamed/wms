# 🔧 Guide de Dépannage - Backend Python

## ❌ Problème Actuel

Les endpoints `/api/analytics/predict/{id}` et `/api/analytics/optimize/{id}` retournent des erreurs 500 sans détails.

## ✅ Corrections Appliquées

1. ✅ Fichier `.env` créé avec le mot de passe PostgreSQL (`Sellam01!`)
2. ✅ Noms de tables corrigés : `"Products"`, `"Orders"`, `"OrderItems"`, `"Stocks"`
3. ✅ Noms de colonnes corrigés avec guillemets pour respecter la casse PostgreSQL
4. ✅ Chargement du `.env` depuis le répertoire du script
5. ✅ Logs [STARTUP], [DEBUG], et [ERROR] ajoutés
6. ✅ Middleware de gestion d'erreurs global ajouté

## 🔍 Diagnostic

### Vérification 1: Fichier .env

```powershell
cd C:\Users\DELL\Desktop\clients\wms\backend-python
Get-Content .env
```

**Résultat attendu** :
```
DB_HOST=localhost
DB_PORT=5432
DB_NAME=wms_db
DB_USER=postgres
DB_PASSWORD=Sellam01!
```

### Vérification 2: Test Local

```powershell
python test_full_endpoint.py
```

**Résultat attendu** : ✅ Tous les tests passent

### Vérification 3: Backend Python

1. **Ouvrez le terminal PowerShell du Backend Python**
2. **Vérifiez les logs au démarrage** :
   - Vous devriez voir `[STARTUP] Fichier .env chargé depuis: ...`
   - Vous devriez voir `[STARTUP] DB_PASSWORD: OUI`

3. **Testez un endpoint** et vérifiez les logs :
   - Vous devriez voir `[DEBUG] Connexion à PostgreSQL: ...`
   - Vous devriez voir `[DEBUG] Mot de passe chargé: OUI`
   - Si vous voyez `[ERROR]`, copiez le message complet

## 🔄 Redémarrage Complet

Si le Backend Python n'a pas été redémarré avec les dernières modifications :

1. **Arrêtez le Backend Python** :
   - Dans le terminal PowerShell, appuyez sur `Ctrl+C`
   - OU : `Get-Process -Name "python","uvicorn" | Where-Object { (Get-NetTCPConnection -OwningProcess $_.Id -LocalPort 8000) } | Stop-Process -Force`

2. **Redémarrez-le** :
   ```powershell
   cd C:\Users\DELL\Desktop\clients\wms\backend-python
   python -m uvicorn main:app --reload --port 8000
   ```

3. **Vérifiez les logs [STARTUP]** au démarrage

4. **Testez les endpoints** :
   ```powershell
   .\diagnostic.ps1
   ```

## 📋 Logs à Vérifier

### Au Démarrage (Logs [STARTUP])
```
[STARTUP] Fichier .env chargé depuis: C:\Users\DELL\Desktop\clients\wms\backend-python\.env
[STARTUP] DB_PASSWORD: OUI
```

### Lors d'une Requête (Logs [DEBUG])
```
[DEBUG] Connexion à PostgreSQL: postgres@localhost:5432/wms_db
[DEBUG] Mot de passe chargé: OUI
```

### En Cas d'Erreur (Logs [ERROR])
```
[ERROR] Erreur PostgreSQL: ...
[ERROR] Exception non gérée: ...
```

## 🐛 Si l'Erreur Persiste

1. **Vérifiez que le Backend Python a bien été redémarré** :
   - Les logs [STARTUP] doivent apparaître au démarrage
   - Le processus doit être récent (vérifiez avec `Get-Process`)

2. **Vérifiez les logs dans le terminal PowerShell** :
   - Copiez tous les messages [ERROR] et [DEBUG]
   - Partagez-les pour diagnostic

3. **Testez directement** :
   ```powershell
   Invoke-WebRequest -Uri "http://localhost:8000/api/analytics/predict/5" -Method GET
   ```

4. **Vérifiez la connexion PostgreSQL** :
   ```powershell
   python test_connection.py
   ```

## 📝 Fichiers de Test Disponibles

- `test_connection.py` - Test de connexion PostgreSQL
- `test_full_endpoint.py` - Test complet de l'endpoint predict
- `test_endpoint.py` - Test de l'endpoint predict
- `check_columns.py` - Vérification des colonnes de la base de données
- `diagnostic.ps1` - Script de diagnostic complet

## ✅ Solution Attendue

Une fois le Backend Python redémarré avec les dernières modifications :
- Les logs [STARTUP] doivent s'afficher au démarrage
- Les logs [DEBUG] doivent s'afficher lors des requêtes
- Les endpoints doivent retourner soit des données, soit des messages d'erreur détaillés (pas des erreurs 500 vides)

