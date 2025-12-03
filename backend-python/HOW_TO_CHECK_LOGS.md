# 📋 Comment Vérifier les Logs du Backend Python

## 🎯 Méthode Simple : Utiliser le Script de Test

### Option 1 : Script PowerShell (Recommandé)

1. **Ouvrez PowerShell** dans le dossier `backend-python`
2. **Exécutez le script** :
   ```powershell
   .\test_optimize_endpoint.ps1
   ```
3. **Le script affichera l'erreur complète** avec tous les détails

### Option 2 : Script de Diagnostic Complet

```powershell
.\diagnostic.ps1
```

Ce script teste les deux endpoints et affiche les erreurs détaillées.

---

## 🔍 Méthode Avancée : Vérifier le Terminal PowerShell du Backend Python

### Étape 1 : Trouver le Terminal PowerShell du Backend Python

1. **Regardez vos fenêtres PowerShell ouvertes**
   - Vous devriez avoir plusieurs fenêtres PowerShell
   - Une d'entre elles devrait afficher des messages comme :
     ```
     INFO:     Uvicorn running on http://127.0.0.1:8000
     INFO:     Application startup complete.
     ```

2. **Si vous ne voyez pas cette fenêtre** :
   - Le Backend Python n'est peut-être pas démarré
   - Redémarrez-le avec :
     ```powershell
     cd C:\Users\DELL\Desktop\clients\wms\backend-python
     python -m uvicorn main:app --reload --port 8000
     ```

### Étape 2 : Tester l'Endpoint et Voir les Logs

1. **Dans le terminal PowerShell du Backend Python**, vous devriez voir des messages comme :
   ```
   [DEBUG] optimize_purchases appelé pour product_id=5
   [DEBUG] Connexion à PostgreSQL: postgres@localhost:5432/wms_db
   [DEBUG] Mot de passe chargé: OUI
   ```

2. **Si une erreur se produit**, vous verrez :
   ```
   [ERROR] Exception non gérée: ...
   [ERROR] Erreur PostgreSQL: ...
   ```

3. **Copiez ces messages** et partagez-les pour diagnostic

---

## 🧪 Test Direct dans PowerShell

### Test Simple

```powershell
# Test de l'endpoint optimize
Invoke-WebRequest -Uri "http://localhost:8000/api/analytics/optimize/5" -Method GET
```

### Test avec Affichage de l'Erreur

```powershell
try {
    $response = Invoke-WebRequest -Uri "http://localhost:8000/api/analytics/optimize/5" -Method GET
    Write-Host "SUCCÈS !" -ForegroundColor Green
    $response.Content
} catch {
    Write-Host "ERREUR:" -ForegroundColor Red
    $stream = $_.Exception.Response.GetResponseStream()
    $reader = New-Object System.IO.StreamReader($stream)
    $responseBody = $reader.ReadToEnd()
    Write-Host $responseBody
}
```

---

## 📊 Vérification Rapide

### Vérifier que le Backend Python est Actif

```powershell
Get-NetTCPConnection -LocalPort 8000 -State Listen
```

**Résultat attendu** : Une connexion sur le port 8000

### Tester la Documentation

Ouvrez dans votre navigateur : **http://localhost:8000/docs**

Si la page s'affiche, le Backend Python est actif.

---

## 🆘 Si Vous Ne Voyez Aucun Log

1. **Vérifiez que le Backend Python est démarré** :
   ```powershell
   Get-Process -Name "python","uvicorn" | Where-Object { (Get-NetTCPConnection -OwningProcess $_.Id -LocalPort 8000) }
   ```

2. **Si aucun processus n'est trouvé**, redémarrez le Backend Python :
   ```powershell
   cd C:\Users\DELL\Desktop\clients\wms\backend-python
   python -m uvicorn main:app --reload --port 8000
   ```

3. **Vérifiez les logs [STARTUP] au démarrage** :
   - Vous devriez voir : `[STARTUP] Fichier .env chargé depuis: ...`
   - Vous devriez voir : `[STARTUP] DB_PASSWORD: OUI`

---

## 💡 Astuce

**Utilisez le script `test_optimize_endpoint.ps1`** - c'est la méthode la plus simple pour voir l'erreur complète sans avoir à chercher dans les logs !

