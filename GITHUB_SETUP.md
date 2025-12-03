# Guide de Configuration GitHub

## 📋 Étapes pour Sauvegarder le Projet sur GitHub

### 1. Initialiser Git Localement

```powershell
# Dans le dossier racine du projet (wms)
git init
```

### 2. Vérifier les Fichiers à Commiter

```powershell
# Voir les fichiers qui seront ajoutés
git status
```

**Important :** Vérifiez que les fichiers sensibles sont bien exclus :
- ✅ `.env` (backend-python)
- ✅ `appsettings.json` (backend-csharp/WMS.API)
- ✅ `appsettings.Development.json`
- ✅ `node_modules/`
- ✅ `bin/`, `obj/`
- ✅ `__pycache__/`

### 3. Ajouter les Fichiers

```powershell
# Ajouter tous les fichiers (sauf ceux dans .gitignore)
git add .
```

### 4. Créer le Premier Commit

```powershell
git commit -m "Initial commit: WMS Platform with Polyglot Microservices Architecture

- Backend C# (ASP.NET Core) for transactional operations
- Backend Python (FastAPI) for analytics and ML
- Frontend React with TypeScript
- PostgreSQL shared database
- Complete documentation"
```

### 5. Créer un Repository sur GitHub

1. Aller sur [GitHub](https://github.com)
2. Cliquer sur le bouton **"+"** en haut à droite
3. Sélectionner **"New repository"**
4. Remplir les informations :
   - **Repository name** : `wms` (ou `warehouse-management-system`)
   - **Description** : `Enterprise Warehouse Management System with Polyglot Microservices Architecture`
   - **Visibility** : Public ou Private (selon votre choix)
   - **NE PAS** initialiser avec README, .gitignore, ou license (on les a déjà)
5. Cliquer sur **"Create repository"**

### 6. Lier le Repository Local à GitHub

```powershell
# Remplacer 'votre-username' par votre nom d'utilisateur GitHub
git remote add origin https://github.com/votre-username/wms.git

# Vérifier que le remote est bien configuré
git remote -v
```

### 7. Pousser le Code vers GitHub

```powershell
# Pousser vers la branche main (ou master selon votre config)
git branch -M main
git push -u origin main
```

Si GitHub vous demande des identifiants :
- Utilisez un **Personal Access Token** (pas votre mot de passe)
- Pour créer un token : GitHub → Settings → Developer settings → Personal access tokens → Tokens (classic)

### 8. Vérifier sur GitHub

Allez sur votre repository GitHub et vérifiez que tous les fichiers sont bien présents.

## 🔒 Sécurité - Fichiers à NE JAMAIS Commiter

### Fichiers Sensibles (déjà dans .gitignore)

✅ **Déjà exclus automatiquement :**
- `.env` (backend-python)
- `appsettings.json` (backend-csharp/WMS.API)
- `appsettings.Development.json`
- Tous les secrets et mots de passe

### Fichiers Exemples à Garder

✅ **Ces fichiers SONT commités (sans secrets) :**
- `appsettings.json.example`
- `.env.example` (si vous en créez un)

### Vérification Avant le Push

```powershell
# Vérifier qu'aucun fichier sensible n'est dans le commit
git status

# Voir les fichiers qui seront commités
git ls-files | Select-String -Pattern "\.env$|appsettings\.json$"
```

**Si vous voyez des fichiers sensibles, retirez-les :**
```powershell
git reset HEAD fichier-sensible
# Puis ajoutez-le au .gitignore
```

## 📝 Structure Recommandée pour GitHub

### Fichiers à la Racine

```
wms/
├── README.md                    ✅ Documentation principale
├── .gitignore                   ✅ Exclusions Git
├── LICENSE                      ✅ (optionnel) Licence
├── ARCHITECTURE.md              ✅ Documentation architecture
├── HOW_IT_WORKS.md              ✅ Guide de fonctionnement
├── PRODUCTION_READINESS.md      ✅ Checklist production
└── ...
```

### Branches Recommandées

```powershell
# Branche principale
main (ou master)

# Branches de développement
develop
feature/nom-feature
bugfix/nom-bugfix
```

## 🏷️ Tags et Releases

### Créer un Tag pour une Version

```powershell
# Tag de version
git tag -a v1.0.0 -m "Version 1.0.0 - Initial Release"
git push origin v1.0.0
```

### Créer une Release sur GitHub

1. Aller sur votre repository GitHub
2. Cliquer sur **"Releases"** → **"Create a new release"**
3. Sélectionner le tag créé
4. Ajouter des notes de release
5. Publier

## 🔄 Workflow de Développement

### Faire des Modifications

```powershell
# Créer une branche pour une nouvelle fonctionnalité
git checkout -b feature/nouvelle-fonctionnalite

# Faire vos modifications
# ...

# Ajouter les fichiers modifiés
git add .

# Commiter
git commit -m "Description des modifications"

# Pousser la branche
git push origin feature/nouvelle-fonctionnalite
```

### Mettre à Jour depuis GitHub

```powershell
# Récupérer les dernières modifications
git pull origin main
```

## 📊 GitHub Actions (CI/CD) - Optionnel

Vous pouvez ajouter des workflows GitHub Actions pour :
- Tests automatiques
- Build automatique
- Déploiement automatique

Exemple de fichier `.github/workflows/ci.yml` :

```yaml
name: CI

on: [push, pull_request]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'
      - name: Build
        run: dotnet build
```

## ✅ Checklist Avant le Premier Push

- [ ] `.gitignore` vérifié et complet
- [ ] Aucun fichier `.env` ou `appsettings.json` avec secrets
- [ ] README.md complet et à jour
- [ ] Documentation créée
- [ ] Code commenté si nécessaire
- [ ] Tests fonctionnels (si disponibles)
- [ ] Repository GitHub créé
- [ ] Remote configuré correctement

## 🎉 Après le Push

1. ✅ Vérifier que tous les fichiers sont sur GitHub
2. ✅ Vérifier que les fichiers sensibles ne sont PAS visibles
3. ✅ Ajouter une description au repository
4. ✅ Ajouter des topics/tags (ex: `wms`, `microservices`, `csharp`, `python`, `react`)
5. ✅ Configurer les paramètres du repository (si nécessaire)

## 📞 Problèmes Courants

### Erreur : "remote origin already exists"
```powershell
git remote remove origin
git remote add origin https://github.com/votre-username/wms.git
```

### Erreur : "Authentication failed"
- Utilisez un Personal Access Token au lieu du mot de passe
- Vérifiez vos permissions sur le repository

### Fichiers Sensibles Déjà Commités
```powershell
# Retirer du cache Git
git rm --cached fichier-sensible
# Ajouter au .gitignore
echo "fichier-sensible" >> .gitignore
# Commiter
git commit -m "Remove sensitive file"
```

---

**Bon push ! 🚀**

