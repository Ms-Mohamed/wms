# WMS Analytics Service - Service Analytique Python

Service analytique en **lecture seule** pour le système de gestion de stock (WMS). Ce service fournit des analyses prédictives et d'optimisation basées sur les données historiques de la base PostgreSQL.

## ⚠️ Mode Lecture Seule

Ce service est **strictement en mode lecture seule**. Il ne modifie jamais la base de données PostgreSQL. Toutes les connexions sont configurées avec `readonly=True` pour garantir l'intégrité des données.

## Prérequis

- Python 3.10 ou supérieur
- PostgreSQL 14+ (même base de données que l'API C#)
- Accès en lecture à la base de données `wms_db`

## Installation

### 1. Créer un environnement virtuel (recommandé)

```bash
cd backend-python
python -m venv venv

# Sur Windows
venv\Scripts\activate

# Sur Linux/Mac
source venv/bin/activate
```

### 2. Installer les dépendances

```bash
pip install -r requirements.txt
```

### 3. Configurer les variables d'environnement

Copiez `.env.example` vers `.env` et modifiez les valeurs :

```bash
cp .env.example .env
```

Éditez `.env` avec vos paramètres PostgreSQL :

```env
DB_HOST=localhost
DB_PORT=5432
DB_NAME=wms_db
DB_USER=postgres
DB_PASSWORD=votre_mot_de_passe
```

## Exécution

### Mode développement

```bash
uvicorn main:app --reload --port 8000
```

### Mode production

```bash
uvicorn main:app --host 0.0.0.0 --port 8000
```

Le service sera accessible sur : `http://localhost:8000`

Documentation interactive (Swagger UI) : `http://localhost:8000/docs`

## Endpoints API

### 1. Prévision de la Demande

**GET** `/api/analytics/predict/{product_id}`

Prévoit la demande pour un produit spécifique pour les 3 prochains mois en utilisant un modèle de régression linéaire basé sur l'historique des ventes.

**Paramètres :**
- `product_id` (path) : ID du produit

**Réponse :**
```json
{
  "product_id": 1,
  "product_code": "PROD-001",
  "product_name": "Ordinateur Portable",
  "forecasts": [
    {
      "mois": "2026-01",
      "prediction": 55.5
    },
    {
      "mois": "2026-02",
      "prediction": 58.2
    },
    {
      "mois": "2026-03",
      "prediction": 60.8
    }
  ],
  "message": null
}
```

**Exemple de requête :**
```bash
curl http://localhost:8000/api/analytics/predict/1
```

### 2. Optimisation des Achats

**GET** `/api/analytics/optimize/{product_id}`

Calcule le Point de Commande et la Quantité Économique de Commande (QEC/EOQ) pour un produit spécifique.

**Paramètres :**
- `product_id` (path) : ID du produit

**Réponse :**
```json
{
  "product_id": 1,
  "product_code": "PROD-001",
  "product_name": "Ordinateur Portable",
  "current_stock": 50.0,
  "reorder_point": 15.5,
  "eoq": 45.2,
  "average_daily_demand": 2.1,
  "lead_time_days": 7,
  "safety_stock": 22.05,
  "unit_cost": 650.0
}
```

**Exemple de requête :**
```bash
curl http://localhost:8000/api/analytics/optimize/1
```

## Modèles Utilisés

### Prévision de Demande

- **Modèle** : Régression Linéaire (scikit-learn)
- **Données** : Historique des ventes sur 12 mois
- **Prévision** : 3 prochains mois
- **Minimum requis** : 3 mois de données historiques

### Optimisation des Achats

#### Point de Commande (Reorder Point)
```
Point de Commande = (Demande moyenne quotidienne × Délai de livraison) + Stock de sécurité
```

#### Quantité Économique de Commande (EOQ / QEC)
```
EOQ = sqrt((2 × D × S) / H)

où:
  D = Demande annuelle
  S = Coût de commande
  H = Coût de possession par unité par an
```

#### Stock de Sécurité
```
Stock de sécurité = Multiplicateur × Demande moyenne quotidienne × Délai de livraison
```

## Paramètres Configurables

Les paramètres suivants peuvent être configurés via les variables d'environnement dans `.env` :

- `LEAD_TIME_DAYS` : Délai de livraison en jours (défaut: 7)
- `ORDERING_COST` : Coût de commande (défaut: 50.0)
- `HOLDING_COST_RATE` : Taux de coût de possession par an (défaut: 0.20 = 20%)
- `SAFETY_STOCK_MULTIPLIER` : Multiplicateur pour le stock de sécurité (défaut: 1.5)

## Dépendances

- **fastapi** : Framework web moderne et rapide
- **uvicorn** : Serveur ASGI haute performance
- **pandas** : Manipulation et analyse de données
- **scikit-learn** : Machine Learning (régression linéaire)
- **psycopg2-binary** : Connexion PostgreSQL
- **python-dotenv** : Gestion des variables d'environnement
- **pydantic** : Validation de données

## Sécurité

- ✅ Mode lecture seule garanti au niveau de la connexion PostgreSQL
- ✅ Aucune modification de la base de données possible
- ✅ Validation des entrées avec Pydantic
- ✅ Gestion d'erreurs appropriée

## Tests

### Test manuel avec curl

```bash
# Test de prévision
curl http://localhost:8000/api/analytics/predict/1

# Test d'optimisation
curl http://localhost:8000/api/analytics/optimize/1
```

### Test avec Swagger UI

Accédez à `http://localhost:8000/docs` pour une interface interactive de test.

## Notes Techniques

1. **Lecture seule** : Toutes les connexions utilisent `conn.set_session(readonly=True)`
2. **Gestion des erreurs** : Toutes les exceptions sont capturées et retournées avec des messages appropriés
3. **Performance** : Les requêtes SQL sont optimisées pour la lecture
4. **Données minimales** : Si moins de 3 mois de données, la prévision retourne un message d'avertissement

## Support

Pour toute question ou problème, consultez la documentation ou contactez l'équipe de développement.

