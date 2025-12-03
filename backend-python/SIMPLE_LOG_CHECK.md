# 🔍 Comment Voir les Logs du Backend Python (Méthode Simple)

## 📺 Étape 1 : Trouver le Terminal PowerShell du Backend Python

1. **Regardez toutes vos fenêtres PowerShell ouvertes**
2. **Cherchez celle qui affiche des messages comme** :
   ```
   INFO:     Uvicorn running on http://127.0.0.1:8000
   INFO:     Application startup complete.
   ```
3. **C'est le terminal du Backend Python !**

---

## 🧪 Étape 2 : Tester l'Endpoint

1. **Dans un autre terminal PowerShell**, exécutez :
   ```powershell
   cd C:\Users\DELL\Desktop\clients\wms\backend-python
   .\test_optimize_endpoint.ps1
   ```

2. **OU testez directement** :
   ```powershell
   Invoke-WebRequest -Uri "http://localhost:8000/api/analytics/optimize/5" -Method GET
   ```

---

## 👀 Étape 3 : Regarder les Logs dans le Terminal du Backend Python

**Pendant que vous testez l'endpoint**, regardez le terminal PowerShell du Backend Python.

Vous devriez voir des messages comme :

### ✅ Si tout fonctionne :
```
[DEBUG] optimize_purchases appelé pour product_id=5
[DEBUG] Connexion à PostgreSQL: postgres@localhost:5432/wms_db
[DEBUG] Mot de passe chargé: OUI
```

### ❌ Si une erreur se produit :
```
[ERROR] Erreur PostgreSQL: ...
[ERROR] Exception non gérée: ...
Traceback (most recent call last):
  ...
```

---

## 📋 Étape 4 : Copier les Logs

1. **Sélectionnez tout le texte** dans le terminal PowerShell du Backend Python
2. **Copiez-le** (Ctrl+C)
3. **Collez-le ici** pour que je puisse voir l'erreur exacte

---

## 🆘 Si Vous Ne Trouvez Pas le Terminal

1. **Arrêtez tous les processus Python** :
   ```powershell
   Get-Process -Name "python","uvicorn" | Stop-Process -Force
   ```

2. **Redémarrez le Backend Python dans un nouveau terminal** :
   ```powershell
   cd C:\Users\DELL\Desktop\clients\wms\backend-python
   python -m uvicorn main:app --reload --port 8000
   ```

3. **Gardez ce terminal ouvert** - c'est là que vous verrez les logs !

4. **Dans un autre terminal**, testez :
   ```powershell
   .\test_optimize_endpoint.ps1
   ```

---

## 💡 Astuce

**Le terminal PowerShell du Backend Python affiche TOUS les messages en temps réel** :
- Les logs `[DEBUG]`
- Les logs `[ERROR]`
- Les tracebacks Python complets

C'est la meilleure façon de voir ce qui se passe !

