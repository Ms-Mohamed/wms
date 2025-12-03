-- Script de création de la base de données WMS
-- Exécuter ce script en tant qu'utilisateur postgres ou superutilisateur

-- Créer la base de données (si elle n'existe pas)
CREATE DATABASE wms_db
    WITH 
    OWNER = postgres
    ENCODING = 'UTF8'
    LC_COLLATE = 'French_France.1252'
    LC_CTYPE = 'French_France.1252'
    TABLESPACE = pg_default
    CONNECTION LIMIT = -1;

-- Commentaire sur la base de données
COMMENT ON DATABASE wms_db IS 'Base de données du système de gestion de stock (WMS)';

-- Note: Les tables seront créées automatiquement par Entity Framework Core
-- lors du premier démarrage de l'application avec EnsureCreated()

