# Configuration du Backend Python

## Fichier .env

Le Backend Python nécessite un fichier `.env` dans le dossier `backend-python` avec la configuration PostgreSQL.

### Contenu du fichier .env

Créez un fichier `.env` dans `backend-python/` avec le contenu suivant :

```
DB_HOST=localhost
DB_PORT=5432
DB_NAME=wms_db
DB_USER=postgres
DB_PASSWORD=Sellam01!
```

**Important** : Remplacez `Sellam01!` par votre mot de passe PostgreSQL réel si différent.

### Vérification

Après avoir créé le fichier `.env`, redémarrez le Backend Python :

```powershell
cd backend-python
python -m uvicorn main:app --reload --port 8000
```

### Test

Testez les endpoints :
- http://localhost:8000/api/analytics/predict/5
- http://localhost:8000/api/analytics/optimize/5

