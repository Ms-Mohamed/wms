# 🚀 Démarrage Rapide - WMS

## Commandes Rapides (3 Terminaux)

### Terminal 1 - Backend C#
```powershell
cd backend-csharp\WMS.API
dotnet run
```

### Terminal 2 - Backend Python
```powershell
cd backend-python
python -m uvicorn main:app --reload --port 8000
```

### Terminal 3 - Frontend React
```powershell
cd frontend
npm run dev
```

## URLs de Vérification

- **Frontend** : http://localhost:3000
- **Backend C# Swagger** : http://localhost:5000/swagger
- **Backend Python Docs** : http://localhost:8000/docs

## Test Critique Rapide

1. Frontend → **Produits** → Créer produit `TEST-001`
2. SQL : Créer stock de 5 unités pour ce produit
3. Frontend → **Commandes** → Créer commande de 10 unités
4. ✅ Vérifier : Erreur "Stock insuffisant" + Stock reste à 5

Voir `VALIDATION_TEST.md` pour les détails complets.

