# WMS Frontend - React TypeScript

Interface utilisateur moderne et performante pour le système de gestion de stock (WMS).

## Technologies

- **React 19** avec TypeScript
- **Vite** - Build tool rapide
- **Chakra UI** - Bibliothèque de composants moderne
- **Tailwind CSS** - Styling utilitaire
- **React Router** - Navigation
- **React Query** - Gestion d'état serveur
- **Recharts** - Graphiques
- **i18next** - Internationalisation (FR/EN)

## Prérequis

- Node.js 18+ et npm/yarn
- Backend C# en cours d'exécution sur le port 5000
- Backend Python en cours d'exécution sur le port 8000

## Installation

```bash
cd frontend
npm install
```

## Configuration

Le proxy est configuré dans `vite.config.ts` pour rediriger :
- `/api/*` → `http://localhost:5000/api/*` (Backend C#)
- `/analytics/*` → `http://localhost:8000/api/analytics/*` (Backend Python)

## Démarrage

```bash
npm run dev
```

L'application sera accessible sur : `http://localhost:3000`

## Structure

```
src/
├── components/       # Composants réutilisables
│   └── Layout/      # Layout principal (Sidebar, Header)
├── pages/           # Pages de l'application
│   ├── Dashboard.tsx
│   ├── Orders.tsx
│   ├── Products.tsx
│   ├── Stock.tsx
│   ├── Analytics.tsx
│   └── Invoice.tsx
├── services/        # Services API
│   └── api.ts      # Clients API et types
└── i18n/           # Configuration i18n
    ├── config.ts
    └── locales/    # Traductions FR/EN
```

## Fonctionnalités

### Dashboard Transactionnel
- Vue d'ensemble avec statistiques
- Gestion des commandes (création, visualisation)
- Gestion des produits (CRUD complet)
- Visualisation du stock

### Dashboard Analytique
- Prévision de demande (3 prochains mois)
- Optimisation des achats (Point de commande, QEC)
- Graphiques interactifs avec Recharts

### Facturation
- Page de facture professionnelle et imprimable
- Route `/invoices/:orderId`
- Format prêt pour impression/PDF

## Performance

- **Memoization** : Tous les composants de liste utilisent `React.memo`
- **Code Splitting** : Routes chargées avec `React.lazy` et `Suspense`
- **React Query** : Cache et invalidation intelligente des données

## Internationalisation

L'application supporte le Français et l'Anglais. Le sélecteur de langue est disponible dans le header.

## Build

```bash
npm run build
```

Les fichiers de production seront générés dans le dossier `dist/`.
