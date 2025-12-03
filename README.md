# 🏭 Warehouse Management System (WMS)

Plateforme Web de Gestion de Stock d'entreprise avec architecture **Polyglot Microservices**.

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Python](https://img.shields.io/badge/Python-3.10+-3776AB?logo=python)](https://www.python.org/)
[![React](https://img.shields.io/badge/React-18-61DAFB?logo=react)](https://reactjs.org/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-14+-336791?logo=postgresql)](https://www.postgresql.org/)

## 📋 Table des Matières

- [Vue d'Ensemble](#vue-densemble)
- [Architecture](#architecture)
- [Fonctionnalités](#fonctionnalités)
- [Technologies](#technologies)
- [Prérequis](#prérequis)
- [Installation](#installation)
- [Configuration](#configuration)
- [Démarrage](#démarrage)
- [Documentation](#documentation)
- [Structure du Projet](#structure-du-projet)
- [API Endpoints](#api-endpoints)
- [Contribution](#contribution)
- [License](#license)

## 🎯 Vue d'Ensemble

WMS est une plateforme complète de gestion de stock d'entreprise avec :
- ✅ **Gestion transactionnelle** robuste (commandes, stock, factures)
- ✅ **Analyses prédictives** avec Machine Learning
- ✅ **Optimisation des achats** (Point de Commande, EOQ)
- ✅ **Interface moderne** et multilingue (FR/EN)
- ✅ **Architecture microservices** polyglotte

## 🏗️ Architecture

### Polyglot Microservices Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Frontend React (TypeScript)               │
│                    Port 3000                                 │
└──────┬───────────────────────────────┬───────────────────────┘
       │                               │
       │ Requêtes API                  │ Requêtes API
       │                               │
┌──────▼──────────┐          ┌─────────▼─────────┐
│  Backend C#     │          │  Backend Python  │
│  ASP.NET Core   │          │  FastAPI         │
│  Port 5000      │          │  Port 8000       │
│                 │          │                   │
│  • Orders       │          │  • Forecasting    │
│  • Stock        │          │  • Optimization   │
│  • Invoices     │          │  • Analytics      │
└──────┬──────────┘          └─────────┬─────────┘
       │                               │
       └──────────────┬─────────────────┘
                      │
              ┌───────▼────────┐
              │   PostgreSQL   │
              │   Database      │
              └────────────────┘
```

**Pourquoi Polyglot ?**
- **C#** : Performance et robustesse pour les transactions
- **Python** : Écosystème data science pour les analyses ML
- **PostgreSQL** : Source de vérité unique partagée

📖 [Documentation Architecture Complète](./ARCHITECTURE.md)

## ✨ Fonctionnalités

### Core Transactionnel (C#)
- ✅ Gestion des commandes clients avec transactions atomiques
- ✅ Décrément automatique du stock
- ✅ Génération automatique de factures
- ✅ Gestion des produits (CRUD complet)
- ✅ Gestion multi-entrepôts et emplacements
- ✅ Traçabilité (Lots/Numéros de série)
- ✅ Valorisation CUMP (Coût Unitaire Moyen Pondéré)

### Service Analytique (Python)
- ✅ **Prévision de demande** : ML pour prédire les ventes (3 mois)
- ✅ **Optimisation des achats** : Calcul du Point de Commande et EOQ
- ✅ **Alertes de stock** : Notifications automatiques
- ✅ Mode lecture seule (ne modifie jamais la DB)

### Frontend React
- ✅ Dashboard interactif avec statistiques en temps réel
- ✅ Gestion des commandes avec interface moderne
- ✅ Visualisation du stock par entrepôt
- ✅ Graphiques analytiques (Recharts)
- ✅ Page de facturation professionnelle et imprimable
- ✅ Internationalisation (Français/Anglais)
- ✅ Optimisations de performance (memoization, code splitting)

## 🛠️ Technologies

### Backend C#
- **Framework** : ASP.NET Core 8.0
- **ORM** : Entity Framework Core avec Npgsql
- **Architecture** : Clean Architecture (Data, Business, API)
- **Base de données** : PostgreSQL

### Backend Python
- **Framework** : FastAPI
- **Data Science** : Pandas, NumPy, Scikit-learn
- **Database** : Psycopg2-binary
- **Server** : Uvicorn

### Frontend
- **Framework** : React 18 avec TypeScript
- **Styling** : Tailwind CSS + Chakra UI
- **State Management** : React Query
- **Charts** : Recharts
- **i18n** : react-i18next
- **Build Tool** : Vite

### Base de Données
- **SGBD** : PostgreSQL 14+
- **ORM C#** : Entity Framework Core
- **ORM Python** : Psycopg2 (raw SQL)

## 📦 Prérequis

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download)
- [Python 3.10+](https://www.python.org/downloads/)
- [PostgreSQL 14+](https://www.postgresql.org/download/)
- [Node.js 18+](https://nodejs.org/)
- [Git](https://git-scm.com/)

## 🚀 Installation

### 1. Cloner le Repository

```bash
git clone https://github.com/votre-username/wms.git
cd wms
```

### 2. Configuration de la Base de Données

#### Créer la Base de Données PostgreSQL

```sql
CREATE DATABASE wms_db;
CREATE USER postgres WITH PASSWORD 'votre_mot_de_passe';
GRANT ALL PRIVILEGES ON DATABASE wms_db TO postgres;
```

Ou utilisez le script fourni :

```bash
cd backend-csharp/scripts
psql -U postgres -f create-database.sql
```

### 3. Configuration des Backends

#### Backend C#

1. Copier le fichier de configuration exemple :
```bash
cd backend-csharp/WMS.API
cp appsettings.json.example appsettings.json
```

2. Modifier `appsettings.json` avec vos credentials PostgreSQL :
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=wms_db;Username=postgres;Password=votre_mot_de_passe"
  }
}
```

3. Appliquer les migrations :
```bash
cd backend-csharp/WMS.API
dotnet ef database update
```

#### Backend Python

1. Créer le fichier `.env` :
```bash
cd backend-python
cp .env.example .env
```

2. Modifier `.env` avec vos credentials :
```env
DB_HOST=localhost
DB_PORT=5432
DB_NAME=wms_db
DB_USER=postgres
DB_PASSWORD=votre_mot_de_passe
```

3. Installer les dépendances :
```bash
pip install -r requirements.txt
```

### 4. Configuration du Frontend

```bash
cd frontend
npm install
```

## ⚙️ Configuration

### Variables d'Environnement

#### Backend C#
- `appsettings.json` : Configuration principale
- `appsettings.Development.json` : Configuration développement

#### Backend Python
- `.env` : Variables d'environnement (ne pas commiter !)

#### Frontend
- Les URLs des APIs sont configurées dans `vite.config.ts`

## 🏃 Démarrage

### Option 1 : Scripts PowerShell (Recommandé)

```powershell
# Démarrer tous les services
.\start-all-services.ps1

# Ou démarrer individuellement
.\start-backend-csharp.ps1
.\start-backend-python.ps1
.\start-frontend.ps1
```

### Option 2 : Démarrage Manuel

#### Backend C# (Terminal 1)
```bash
cd backend-csharp/WMS.API
dotnet run
```
→ Service disponible sur `http://localhost:5000`
→ Swagger UI : `http://localhost:5000/swagger`

#### Backend Python (Terminal 2)
```bash
cd backend-python
python -m uvicorn main:app --reload --port 8000
```
→ Service disponible sur `http://localhost:8000`
→ Documentation : `http://localhost:8000/docs`

#### Frontend (Terminal 3)
```bash
cd frontend
npm run dev
```
→ Application disponible sur `http://localhost:3000`

### Accès à l'Application

- **Frontend** : http://localhost:3000
- **API C# Swagger** : http://localhost:5000/swagger
- **API Python Docs** : http://localhost:8000/docs

## 📚 Documentation

### Documentation Principale

- [Architecture](./ARCHITECTURE.md) - Architecture polyglotte détaillée
- [Comment ça fonctionne](./HOW_IT_WORKS.md) - Flux de données et scénarios
- [Lien C# et Python](./C_SHARP_PYTHON_CONNECTION.md) - Communication entre services
- [Production Ready](./PRODUCTION_READINESS.md) - Checklist pour la production
- [Quick Wins](./QUICK_WINS_PRODUCTION.md) - Améliorations rapides

### Documentation par Service

- [Backend C#](./backend-csharp/README.md)
- [Backend Python](./backend-python/README.md)
- [Frontend](./frontend/README.md)

## 📁 Structure du Projet

```
wms/
├── backend-csharp/              # Service transactionnel (C#)
│   ├── WMS.API/                # Couche API (Controllers, Program.cs)
│   ├── WMS.Business/           # Logique métier (Services, DTOs)
│   ├── WMS.Data/               # Accès aux données (Entities, DbContext)
│   └── scripts/                # Scripts SQL
│
├── backend-python/              # Service analytique (Python)
│   ├── main.py                 # Application FastAPI
│   ├── requirements.txt        # Dépendances Python
│   └── .env                    # Variables d'environnement (non commité)
│
├── frontend/                    # Application React
│   ├── src/
│   │   ├── components/         # Composants React
│   │   ├── pages/              # Pages de l'application
│   │   ├── services/           # Services API
│   │   └── types/              # Types TypeScript
│   └── package.json
│
└── Documentation/
    ├── ARCHITECTURE.md
    ├── HOW_IT_WORKS.md
    └── ...
```

## 🔌 API Endpoints

### Backend C# (Port 5000)

#### Commandes
- `POST /api/orders` - Créer une commande (avec transaction)
- `GET /api/orders` - Lister les commandes
- `GET /api/orders/{id}` - Détails d'une commande

#### Factures
- `GET /api/invoices/{orderId}` - Obtenir une facture

#### Produits
- `GET /api/products` - Lister les produits
- `POST /api/products` - Créer un produit
- `PUT /api/products/{id}` - Modifier un produit
- `DELETE /api/products/{id}` - Supprimer un produit

#### Stock
- `GET /api/stocks` - Voir les stocks

### Backend Python (Port 8000)

#### Analytics
- `GET /api/analytics/predict/{productId}` - Prévision de demande (3 mois)
- `GET /api/analytics/optimize/{productId}` - Optimisation (Point de Commande, EOQ)
- `GET /api/analytics/alerts` - Alertes de stock

📖 Documentation complète : http://localhost:5000/swagger et http://localhost:8000/docs

## 🧪 Tests

### Tests d'Intégrité
```bash
# Voir VALIDATION_TEST.md pour les tests complets
```

### Tests Manuels
1. Créer une commande avec stock insuffisant → Doit échouer
2. Créer une commande valide → Stock décrémenté
3. Consulter les analytics → Prédictions affichées
4. Voir une facture → Format professionnel

## 🚀 Déploiement

Voir [PRODUCTION_READINESS.md](./PRODUCTION_READINESS.md) pour la checklist complète.

### Points Critiques
- ✅ Authentification et autorisation
- ✅ Gestion des secrets (Azure Key Vault, etc.)
- ✅ Backups automatiques PostgreSQL
- ✅ Health checks
- ✅ Logging structuré
- ✅ HTTPS/SSL
- ✅ Rate limiting

## 🤝 Contribution

Les contributions sont les bienvenues ! Pour contribuer :

1. Fork le projet
2. Créez une branche (`git checkout -b feature/AmazingFeature`)
3. Committez vos changements (`git commit -m 'Add some AmazingFeature'`)
4. Push vers la branche (`git push origin feature/AmazingFeature`)
5. Ouvrez une Pull Request

## 📝 License

Ce projet est sous licence MIT. Voir le fichier `LICENSE` pour plus de détails.

## 👥 Auteurs

- **Votre Nom** - *Développement initial*

## 🙏 Remerciements

- Entity Framework Core
- FastAPI
- React Community
- PostgreSQL

## 📞 Support

Pour toute question ou problème :
- Ouvrir une [Issue](https://github.com/votre-username/wms/issues)
- Consulter la [Documentation](./ARCHITECTURE.md)

---

⭐ Si ce projet vous a aidé, n'hésitez pas à lui donner une étoile !
