"""
WMS Analytics Service - Service analytique en lecture seule
Ce service fournit des analyses prédictives et d'optimisation basées sur les données historiques.
Mode strictement lecture seule - aucune modification de la base de données.
"""

from fastapi import FastAPI, HTTPException, Request
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import JSONResponse
from fastapi.exceptions import RequestValidationError
from pydantic import BaseModel
from typing import List, Optional
from datetime import datetime, timedelta
import psycopg2
from psycopg2.extras import RealDictCursor
import pandas as pd
from sklearn.linear_model import LinearRegression
import numpy as np
import os
from dotenv import load_dotenv
from pathlib import Path
import traceback
from dateutil import parser as date_parser

# Charger le .env depuis le répertoire du script au démarrage
env_path = Path(__file__).parent / '.env'
if env_path.exists():
    load_dotenv(dotenv_path=env_path, override=False)
    print(f"[STARTUP] Fichier .env chargé depuis: {env_path}")
    print(f"[STARTUP] DB_PASSWORD: {'OUI' if os.getenv('DB_PASSWORD') and os.getenv('DB_PASSWORD') != 'postgres' else 'NON (défaut)'}")
else:
    print(f"[STARTUP] ATTENTION: Fichier .env non trouvé à {env_path}")
    load_dotenv()  # Essayer de charger depuis le répertoire courant

app = FastAPI(
    title="WMS Analytics Service",
    description="Service analytique pour prévisions de demande et optimisation des achats (Lecture seule)",
    version="1.0.0"
)

# Middleware de gestion d'erreurs global
@app.exception_handler(Exception)
async def global_exception_handler(request: Request, exc: Exception):
    """Gestionnaire d'erreurs global pour capturer toutes les exceptions"""
    try:
        error_details = traceback.format_exc()
        print(f"[ERROR] Exception non gérée capturée par le middleware global")
        print(f"[ERROR] Type: {type(exc).__name__}")
        print(f"[ERROR] Message: {str(exc)}")
        print(f"[ERROR] Traceback complet:")
        print(error_details)
        
        # Essayer de retourner une réponse JSON détaillée
        response_content = {
            "detail": f"Erreur interne du serveur: {str(exc)}",
            "type": type(exc).__name__,
            "traceback": error_details
        }
        
        return JSONResponse(
            status_code=500,
            content=response_content
        )
    except Exception as e:
        # Si même la création de la réponse échoue, retourner au moins quelque chose
        print(f"[ERROR] CRITIQUE: Impossible de créer la réponse d'erreur: {e}")
        return JSONResponse(
            status_code=500,
            content={"detail": f"Erreur critique: {str(exc)}"}
        )

# CORS configuration
app.add_middleware(
    CORSMiddleware,
    allow_origins=["http://localhost:3000", "http://localhost:5173"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Configuration de la connexion PostgreSQL (Lecture seule)
def get_db_connection():
    """
    Crée une connexion PostgreSQL en mode lecture seule.
    Cette fonction garantit que seule la lecture est possible.
    """
    # Forcer le rechargement du .env à chaque connexion depuis le répertoire du script
    from pathlib import Path
    env_path = Path(__file__).parent / '.env'
    load_dotenv(dotenv_path=env_path, override=False)
    
    db_host = os.getenv("DB_HOST", "localhost")
    db_port = os.getenv("DB_PORT", "5432")
    db_name = os.getenv("DB_NAME", "wms_db")
    db_user = os.getenv("DB_USER", "postgres")
    db_password = os.getenv("DB_PASSWORD", "postgres")
    
    # Debug: Afficher la configuration
    print(f"[DEBUG] Connexion à PostgreSQL: {db_user}@{db_host}:{db_port}/{db_name}")
    print(f"[DEBUG] Mot de passe chargé: {'OUI' if db_password and db_password != 'postgres' else 'NON (utilise défaut)'}")
    
    conn = psycopg2.connect(
        host=db_host,
        port=db_port,
        database=db_name,
        user=db_user,
        password=db_password
    )
    # Définir le mode lecture seule au niveau de la session
    conn.set_session(readonly=True, autocommit=False)
    return conn

# Modèles Pydantic pour les réponses
class ForecastMonth(BaseModel):
    mois: str  # Format: 'YYYY-MM'
    prediction: float

class DemandForecastResponse(BaseModel):
    product_id: int
    product_code: str
    product_name: str
    forecasts: List[ForecastMonth]
    message: Optional[str] = None

class OptimizationResponse(BaseModel):
    product_id: int
    product_code: str
    product_name: str
    current_stock: float
    reorder_point: float
    eoq: float  # Quantité Économique de Commande
    average_daily_demand: float
    lead_time_days: int
    safety_stock: float
    unit_cost: float

@app.get("/")
def root():
    """Endpoint de base pour vérifier que le service est actif"""
    return {
        "message": "WMS Analytics Service",
        "version": "1.0.0",
        "mode": "read-only"
    }

@app.get("/test-simple")
async def test_simple():
    """Endpoint de test ultra-simple pour vérifier que FastAPI fonctionne"""
    print(f"[DEBUG] test_simple appelé")
    return {"message": "Test simple réussi", "status": "ok"}

@app.get("/test-optimize/{product_id}")
async def test_optimize_logic(product_id: int):
    """Endpoint de test pour déboguer l'optimize"""
    print(f"[DEBUG] test_optimize_logic appelé pour product_id={product_id}")
    try:
        # Simuler exactement ce que fait optimize_purchases
        conn = get_db_connection()
        cursor = conn.cursor(cursor_factory=RealDictCursor)
        
        product_query = """
            SELECT 
                p."Id",
                p."Code",
                p."Name",
                p."CostPrice"
            FROM "Products" p
            WHERE p."Id" = %s
        """
        cursor.execute(product_query, (product_id,))
        product = cursor.fetchone()
        
        if not product:
            raise HTTPException(status_code=404, detail=f"Produit avec ID {product_id} introuvable")
        
        product_dict = {
            'id': product['Id'],
            'code': product['Code'],
            'name': product['Name'],
            'unit_cost': product['CostPrice']
        }
        
        # Retourner juste les données du produit pour tester
        return {
            "product_id": product_id,
            "product_code": product_dict['code'],
            "product_name": product_dict['name'],
            "unit_cost": float(product_dict['unit_cost'] or 0),
            "test": "success"
        }
        
    except Exception as e:
        print(f"[ERROR] Erreur dans test_optimize_logic: {e}")
        import traceback
        traceback.print_exc()
        raise HTTPException(status_code=500, detail=f"Erreur: {str(e)}")
    finally:
        if cursor:
            cursor.close()
        if conn:
            conn.close()

class StatsResponse(BaseModel):
    total_stock_value: float
    total_products: int
    low_stock_count: int
    stock_value_currency: str = "€"

@app.get("/stats", response_model=StatsResponse)
async def get_stats():
    """
    Récupère les statistiques globales pour le tableau de bord.
    """
    conn = None
    cursor = None
    try:
        conn = get_db_connection()
        cursor = conn.cursor(cursor_factory=RealDictCursor)
        
        # 1. Valeur totale du stock
        # Somme de (Quantité en stock * Coût unitaire du produit)
        # Note: On prend le coût unitaire actuel du produit comme approximation
        value_query = """
            SELECT SUM(s."Quantity" * p."CostPrice") as total_value
            FROM "Stocks" s
            JOIN "Products" p ON s."ProductId" = p."Id"
        """
        cursor.execute(value_query)
        value_result = cursor.fetchone()
        total_value = float(value_result['total_value'] or 0)
        
        # 2. Nombre total de produits
        product_count_query = """
            SELECT COUNT(*) as count FROM "Products"
        """
        cursor.execute(product_count_query)
        product_count = int(cursor.fetchone()['count'])
        
        # 3. Produits en rupture ou stock faible (Quantity <= ReorderPoint)
        # On utilise le ReorderPoint s'il existe dans Stocks ou une valeur par défaut
        low_stock_query = """
            SELECT COUNT(*) as count
            FROM "Stocks" s
            WHERE s."Quantity" <= s."ReorderPoint"
        """
        cursor.execute(low_stock_query)
        low_stock_count = int(cursor.fetchone()['count'])
        
        return StatsResponse(
            total_stock_value=round(total_value, 2),
            total_products=product_count,
            low_stock_count=low_stock_count
        )
        
    except Exception as e:
        print(f"[ERROR] Erreur stats: {e}")
        import traceback
        traceback.print_exc()
        raise HTTPException(status_code=500, detail=str(e))
    finally:
        if cursor:
            cursor.close()
        if conn:
            conn.close()

        if conn:
            conn.close()

class SalesHistoryItem(BaseModel):
    month: str
    total_revenue: float
    total_orders: int

class LowStockItem(BaseModel):
    id: int
    product_name: str
    product_code: str
    current_stock: float
    reorder_point: float
    unit_cost: float

class LowStockResponse(BaseModel):
    items: List[LowStockItem]

@app.get("/sales-history", response_model=List[SalesHistoryItem])
async def get_sales_history():
    """
    Récupère l'historique des ventes (Chiffre d'affaires et volume) par mois pour les 6 derniers mois.
    """
    conn = None
    cursor = None
    try:
        conn = get_db_connection()
        cursor = conn.cursor(cursor_factory=RealDictCursor)
        
        # Requête pour grouper par mois les commandes validées
        # On suppose que Orders a une colonne TotalAmount, sinon on calcule depuis OrderItems
        query = """
            SELECT 
                TO_CHAR(o."OrderDate", 'YYYY-MM') as month,
                SUM(o."TotalAmount") as total_revenue,
                COUNT(o."Id") as total_orders
            FROM "Orders" o
            WHERE o."OrderDate" >= NOW() - INTERVAL '6 months'
            GROUP BY TO_CHAR(o."OrderDate", 'YYYY-MM')
            ORDER BY month ASC
        """
        cursor.execute(query)
        results = cursor.fetchall()
        
        history = []
        for row in results:
            history.append(SalesHistoryItem(
                month=row['month'],
                total_revenue=float(row['total_revenue'] or 0),
                total_orders=int(row['total_orders'] or 0)
            ))
            
        return history
        
    except Exception as e:
        print(f"[ERROR] Erreur sales history: {e}")
        import traceback
        traceback.print_exc()
        raise HTTPException(status_code=500, detail=str(e))
    finally:
        if cursor:
            cursor.close()
        if conn:
            conn.close()

@app.get("/low-stock", response_model=LowStockResponse)
async def get_low_stock():
    """
    Récupère la liste détaillée des produits en stock critique.
    """
    conn = None
    cursor = None
    try:
        conn = get_db_connection()
        cursor = conn.cursor(cursor_factory=RealDictCursor)
        
        query = """
            SELECT 
                p."Id",
                p."Name",
                p."Code",
                s."Quantity",
                s."ReorderPoint",
                p."CostPrice"
            FROM "Stocks" s
            JOIN "Products" p ON s."ProductId" = p."Id"
            WHERE s."Quantity" <= s."ReorderPoint"
            ORDER BY s."Quantity" ASC
        """
        cursor.execute(query)
        results = cursor.fetchall()
        
        items = []
        for row in results:
            items.append(LowStockItem(
                id=row['Id'],
                product_name=row['Name'],
                product_code=row['Code'],
                current_stock=float(row['Quantity'] or 0),
                reorder_point=float(row['ReorderPoint'] or 0),
                unit_cost=float(row['CostPrice'] or 0)
            ))
            
        return LowStockResponse(items=items)
        
    except Exception as e:
        print(f"[ERROR] Erreur low stock: {e}")
        import traceback
        traceback.print_exc()
        raise HTTPException(status_code=500, detail=str(e))
    finally:
        if cursor:
            cursor.close()
        if conn:
            conn.close()

@app.get("/predict/{product_id}", response_model=DemandForecastResponse)
async def predict_demand(product_id: int):
    """
    Prévision de la demande pour un produit spécifique pour les 3 prochains mois.
    
    Cette fonction :
    - Lit l'historique des ventes (OrderItems) du produit
    - Utilise un modèle de régression linéaire pour prédire la demande
    - Retourne les prévisions pour les 3 prochains mois
    """
    conn = None
    cursor = None
    
    try:
        conn = get_db_connection()
        cursor = conn.cursor(cursor_factory=RealDictCursor)
        
        # Vérifier que le produit existe
        product_query = """
            SELECT "Id", "Code", "Name"
            FROM "Products"
            WHERE "Id" = %s
        """
        cursor.execute(product_query, (product_id,))
        product = cursor.fetchone()
        
        if not product:
            raise HTTPException(status_code=404, detail=f"Produit avec ID {product_id} introuvable")
        
        # Convertir les clés en format attendu (RealDictCursor retourne les noms exacts de la DB)
        product_dict = {
            'id': product['Id'],
            'code': product['Code'],
            'name': product['Name']
        }
        product = product_dict
        
        # Récupérer l'historique des ventes (OrderItems) pour ce produit
        # Regrouper par mois pour avoir une série temporelle
        history_query = """
            SELECT 
                DATE_TRUNC('month', o."OrderDate") as month,
                SUM(oi."Quantity") as total_quantity
            FROM "OrderItems" oi
            JOIN "Orders" o ON oi."OrderId" = o."Id"
            WHERE oi."ProductId" = %s
            AND o."OrderDate" >= NOW() - INTERVAL '12 months'
            GROUP BY DATE_TRUNC('month', o."OrderDate")
            ORDER BY month ASC
        """
        
        cursor.execute(history_query, (product_id,))
        history_data = cursor.fetchall()
        
        if not history_data or len(history_data) < 3:
            # Pas assez de données historiques
            return DemandForecastResponse(
                product_id=product_id,
                product_code=product['code'],
                product_name=product['name'],
                forecasts=[],
                message="Pas assez de données historiques pour effectuer une prévision (minimum 3 mois requis)"
            )
        
        # Convertir en DataFrame pandas pour faciliter l'analyse
        df = pd.DataFrame(history_data)
        df['month'] = pd.to_datetime(df['month'])
        df = df.sort_values('month')
        
        # Préparer les données pour la régression linéaire
        # Créer un index temporel (nombre de mois depuis le début)
        df['month_index'] = (df['month'] - df['month'].min()).dt.days / 30.44  # Moyenne de jours par mois
        
        X = df[['month_index']].values
        y = df['total_quantity'].values
        
        # Entraîner le modèle de régression linéaire
        model = LinearRegression()
        model.fit(X, y)
        
        # Prévoir les 3 prochains mois
        last_month = df['month'].max()
        forecasts = []
        
        for i in range(1, 4):
            # Calculer le mois à prévoir
            forecast_month = last_month + pd.DateOffset(months=i)
            
            # Calculer l'index temporel pour ce mois
            month_index = (forecast_month - df['month'].min()).days / 30.44
            
            # Faire la prédiction
            predicted_quantity = model.predict([[month_index]])[0]
            
            # S'assurer que la prédiction n'est pas négative
            predicted_quantity = max(0, predicted_quantity)
            
            forecasts.append(ForecastMonth(
                mois=forecast_month.strftime("%Y-%m"),
                prediction=round(float(predicted_quantity), 2)
            ))
        
        return DemandForecastResponse(
            product_id=product_id,
            product_code=product['code'],
            product_name=product['name'],
            forecasts=forecasts,
            message=None
        )
        
    except HTTPException:
        raise
    except psycopg2.OperationalError as e:
        # Erreur de connexion (mot de passe, host, etc.)
        error_msg = str(e)
        print(f"[ERROR] Erreur PostgreSQL: {error_msg}")
        raise HTTPException(
            status_code=500,
            detail=f"Erreur de connexion PostgreSQL: {error_msg}"
        )
    except psycopg2.Error as e:
        # Autre erreur PostgreSQL
        error_msg = str(e)
        print(f"[ERROR] Erreur PostgreSQL: {error_msg}")
        raise HTTPException(
            status_code=500,
            detail=f"Erreur PostgreSQL: {error_msg}"
        )
    except Exception as e:
        # Erreur générale
        import traceback
        error_details = traceback.format_exc()
        print(f"[ERROR] Erreur lors de la prévision: {error_details}")
        raise HTTPException(
            status_code=500,
            detail=f"Erreur lors de la prévision de demande: {str(e)}"
        )
    finally:
        if cursor:
            cursor.close()
        if conn:
            conn.close()

@app.get("/optimize/{product_id}")
async def optimize_purchases(product_id: int):
    """
    Optimisation des achats pour un produit spécifique.
    
    Cette fonction calcule :
    - Le Point de Commande (Reorder Point)
    - La Quantité Économique de Commande (QEC / EOQ)
    
    Basé sur :
    - L'historique de consommation
    - Le stock actuel
    - Le coût unitaire du produit
    """
    print(f"[DEBUG] optimize_purchases appelé pour product_id={product_id}")
    conn = None
    cursor = None
    
    try:
        print(f"[DEBUG] Étape 1: Connexion à la base de données...")
        conn = get_db_connection()
        print(f"[DEBUG] Étape 2: Création du curseur...")
        cursor = conn.cursor(cursor_factory=RealDictCursor)
        print(f"[DEBUG] Étape 3: Exécution de la requête produit...")
        
        # Récupérer les informations du produit
        product_query = """
            SELECT 
                p."Id",
                p."Code",
                p."Name",
                p."CostPrice"
            FROM "Products" p
            WHERE p."Id" = %s
        """
        cursor.execute(product_query, (product_id,))
        product = cursor.fetchone()
        print(f"[DEBUG] Étape 4: Produit récupéré: {product}")
        
        if not product:
            raise HTTPException(status_code=404, detail=f"Produit avec ID {product_id} introuvable")
        
        # Convertir les clés en format attendu (RealDictCursor retourne les noms exacts de la DB)
        print(f"[DEBUG] Étape 5: Conversion du produit...")
        product_dict = {
            'id': product['Id'],
            'code': product['Code'],
            'name': product['Name'],
            'unit_cost': product['CostPrice']
        }
        product = product_dict
        print(f"[DEBUG] Étape 6: Produit converti: {product}")
        
        # Récupérer le stock actuel (somme de tous les entrepôts)
        stock_query = """
            SELECT 
                COALESCE(SUM(s."Quantity"), 0) as total_stock
            FROM "Stocks" s
            WHERE s."ProductId" = %s
        """
        print(f"[DEBUG] Étape 7: Récupération du stock...")
        cursor.execute(stock_query, (product_id,))
        stock_result = cursor.fetchone()
        current_stock = float(stock_result['total_stock'] or 0)
        print(f"[DEBUG] Étape 8: Stock récupéré: {current_stock}")
        
        # Récupérer l'historique de consommation sur les 90 derniers jours
        print(f"[DEBUG] Étape 9: Récupération de l'historique de consommation...")
        consumption_query = """
            SELECT 
                SUM(oi."Quantity") as total_consumption,
                COUNT(DISTINCT DATE(o."OrderDate")) as days_with_orders,
                MIN(o."OrderDate") as first_order_date
            FROM "OrderItems" oi
            JOIN "Orders" o ON oi."OrderId" = o."Id"
            WHERE oi."ProductId" = %s
            AND o."OrderDate" >= NOW() - INTERVAL '90 days'
        """
        cursor.execute(consumption_query, (product_id,))
        consumption_data = cursor.fetchone()
        print(f"[DEBUG] Étape 10: Données de consommation récupérées: {consumption_data}")
        
        total_consumption = float(consumption_data['total_consumption'] or 0)
        days_with_orders = int(consumption_data['days_with_orders'] or 1)
        print(f"[DEBUG] Étape 11: Consommation totale: {total_consumption}, Jours: {days_with_orders}")
        
        # Calculer la demande moyenne quotidienne
        # Utiliser 90 jours comme période de référence même si moins de commandes
        period_days = 90
        if consumption_data['first_order_date']:
            try:
                first_date = consumption_data['first_order_date']
                print(f"[DEBUG] Étape 11.2: first_date brut: {first_date}, type: {type(first_date)}")
                
                # SOLUTION SIMPLE ET DIRECTE: Extraire les composants de date et créer un datetime "naive"
                # Cette méthode fonctionne peu importe le type de first_date
                if hasattr(first_date, 'year'):
                    # C'est déjà un objet datetime-like, extraire les composants
                    first_date_naive = datetime(
                        year=first_date.year,
                        month=first_date.month,
                        day=first_date.day,
                        hour=first_date.hour if hasattr(first_date, 'hour') else 0,
                        minute=first_date.minute if hasattr(first_date, 'minute') else 0,
                        second=first_date.second if hasattr(first_date, 'second') else 0
                    )
                else:
                    # Convertir en string puis parser
                    first_date_str = str(first_date)
                    # Parser la date (gère les formats avec et sans timezone)
                    parsed_date = pd.to_datetime(first_date_str)
                    if hasattr(parsed_date, 'to_pydatetime'):
                        parsed_date = parsed_date.to_pydatetime()
                    # Extraire les composants pour créer un datetime "naive"
                    first_date_naive = datetime(
                        year=parsed_date.year,
                        month=parsed_date.month,
                        day=parsed_date.day,
                        hour=parsed_date.hour,
                        minute=parsed_date.minute,
                        second=parsed_date.second
                    )
                
                print(f"[DEBUG] Étape 11.3: first_date_naive créé: {first_date_naive}, type: {type(first_date_naive)}, tzinfo: {first_date_naive.tzinfo}")
                
                # Calculer les jours depuis la première commande
                # Maintenant first_date_naive est garanti "naive", utiliser datetime.now() qui est aussi "naive"
                now = datetime.now()
                print(f"[DEBUG] Étape 11.4: now: {now}, type: {type(now)}, tzinfo: {now.tzinfo}")
                
                days_since_first = (now - first_date_naive).days
                period_days = max(90, days_since_first + 1)
                print(f"[DEBUG] Étape 11.5: days_since_first: {days_since_first}, period_days: {period_days}")
            except Exception as e:
                print(f"[DEBUG] Erreur lors du calcul de period_days: {e}")
                import traceback
                traceback.print_exc()
                period_days = 90  # Utiliser la valeur par défaut
        
        average_daily_demand = total_consumption / period_days if period_days > 0 else 0
        
        # Paramètres pour les calculs (peuvent être configurés via variables d'environnement)
        lead_time_days = int(os.getenv("LEAD_TIME_DAYS", "7"))  # Délai de livraison en jours
        ordering_cost = float(os.getenv("ORDERING_COST", "50.0"))  # Coût de commande
        holding_cost_rate = float(os.getenv("HOLDING_COST_RATE", "0.20"))  # Taux de coût de possession (20% par an)
        safety_stock_multiplier = float(os.getenv("SAFETY_STOCK_MULTIPLIER", "1.5"))  # Multiplicateur pour stock de sécurité
        
        unit_cost = float(product['unit_cost'] or 0)
        if unit_cost == 0:
            unit_cost = 10.0  # Valeur par défaut si non définie
        
        # Calcul du stock de sécurité
        # Stock de sécurité = Multiplicateur * Demande moyenne quotidienne * Délai de livraison
        safety_stock = safety_stock_multiplier * average_daily_demand * lead_time_days
        
        # Calcul du Point de Commande (Reorder Point)
        # Point de Commande = (Demande moyenne quotidienne * Délai de livraison) + Stock de sécurité
        reorder_point = (average_daily_demand * lead_time_days) + safety_stock
        
        # Calcul de la Quantité Économique de Commande (EOQ / QEC)
        # Formule EOQ = sqrt((2 * D * S) / H)
        # où:
        #   D = Demande annuelle
        #   S = Coût de commande
        #   H = Coût de possession par unité par an
        annual_demand = average_daily_demand * 365
        holding_cost_per_unit_per_year = unit_cost * holding_cost_rate
        
        if holding_cost_per_unit_per_year > 0 and annual_demand > 0:
            eoq = np.sqrt((2 * annual_demand * ordering_cost) / holding_cost_per_unit_per_year)
        else:
            # Valeur par défaut si les calculs ne sont pas possibles
            eoq = max(10.0, average_daily_demand * 30)  # Au moins 1 mois de demande
        
        # Créer la réponse
        print(f"[DEBUG] Étape 17: Création de la réponse...")
        result_data = {
            "product_id": product_id,
            "product_code": product['code'],
            "product_name": product['name'],
            "current_stock": round(current_stock, 2),
            "reorder_point": round(reorder_point, 2),
            "eoq": round(eoq, 2),
            "average_daily_demand": round(average_daily_demand, 2),
            "lead_time_days": lead_time_days,
            "safety_stock": round(safety_stock, 2),
            "unit_cost": round(unit_cost, 2)
        }
        print(f"[DEBUG] Étape 18: Données préparées: {result_data}")
        
        try:
            result = OptimizationResponse(**result_data)
            print(f"[DEBUG] Étape 19: OptimizationResponse créé avec succès")
            return result
        except Exception as e:
            print(f"[ERROR] Erreur lors de la création de OptimizationResponse: {e}")
            import traceback
            traceback.print_exc()
            # Retourner un dictionnaire simple si le modèle Pydantic échoue
            print(f"[DEBUG] Étape 20: Retour d'un dictionnaire simple")
            return result_data
        
    except HTTPException:
        raise
    except psycopg2.OperationalError as e:
        # Erreur de connexion (mot de passe, host, etc.)
        error_msg = str(e)
        print(f"[ERROR] Erreur PostgreSQL (OperationalError): {error_msg}")
        import traceback
        traceback.print_exc()
        raise HTTPException(
            status_code=500,
            detail=f"Erreur de connexion PostgreSQL: {error_msg}"
        )
    except psycopg2.Error as e:
        # Autre erreur PostgreSQL
        error_msg = str(e)
        print(f"[ERROR] Erreur PostgreSQL: {error_msg}")
        import traceback
        traceback.print_exc()
        raise HTTPException(
            status_code=500,
            detail=f"Erreur PostgreSQL: {error_msg}"
        )
    except Exception as e:
        # Erreur générale
        import traceback
        error_details = traceback.format_exc()
        print(f"[ERROR] Erreur lors de l'optimisation: {error_details}")
        traceback.print_exc()
        raise HTTPException(
            status_code=500,
            detail=f"Erreur lors de l'optimisation: {str(e)}. Détails: {error_details[:500]}"
        )
    finally:
        if cursor:
            cursor.close()
        if conn:
            conn.close()

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8000)
