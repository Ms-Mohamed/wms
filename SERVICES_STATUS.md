# 🚀 Statut des Services WMS

## Services Démarrés

Trois terminaux PowerShell ont été ouverts pour démarrer les services :

### ✅ Terminal 1 - Backend C# (Port 5000)
- **Service** : Core Transactionnel (ASP.NET Core)
- **Port** : 5000
- **URL** : http://localhost:5000
- **Swagger UI** : http://localhost:5000/swagger
- **Statut** : En cours de démarrage...

### ✅ Terminal 2 - Backend Python (Port 8000)
- **Service** : Service Analytique (FastAPI)
- **Port** : 8000
- **URL** : http://localhost:8000
- **Documentation** : http://localhost:8000/docs
- **Statut** : En cours de démarrage...

### ✅ Terminal 3 - Frontend React (Port 3000)
- **Service** : Interface Utilisateur (React + TypeScript)
- **Port** : 3000
- **URL** : http://localhost:3000
- **Statut** : En cours de démarrage...

## ⏱️ Temps de Démarrage

- **Backend C#** : ~10-15 secondes
- **Backend Python** : ~3-5 secondes
- **Frontend React** : ~5-10 secondes

## ✅ Vérification

Une fois les services démarrés, vérifiez :

1. **Backend C#** : Ouvrez http://localhost:5000/swagger
   - Vous devriez voir l'interface Swagger avec tous les endpoints

2. **Backend Python** : Ouvrez http://localhost:8000/docs
   - Vous devriez voir la documentation interactive FastAPI

3. **Frontend React** : Ouvrez http://localhost:3000
   - Vous devriez voir l'interface WMS avec le dashboard

## 🐛 Dépannage

Si un service ne démarre pas :

1. **Vérifiez les logs** dans le terminal correspondant
2. **Vérifiez que les ports ne sont pas utilisés** :
   ```powershell
   netstat -ano | findstr ":5000"
   netstat -ano | findstr ":8000"
   netstat -ano | findstr ":3000"
   ```
3. **Vérifiez les prérequis** :
   - PostgreSQL doit être en cours d'exécution
   - Base de données `wms_db` doit exister
   - .NET 8.0 SDK installé
   - Python 3.10+ installé
   - Node.js 18+ installé

## 📝 Commandes Utiles

Pour redémarrer un service, fermez le terminal et relancez :

**Backend C#** :
```powershell
cd backend-csharp\WMS.API
dotnet run
```

**Backend Python** :
```powershell
cd backend-python
python -m uvicorn main:app --reload --port 8000
```

**Frontend React** :
```powershell
cd frontend
npm run dev
```

Ou utilisez le script :
```powershell
.\start-all-services.ps1
```

