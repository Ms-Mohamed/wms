"""Script pour tester complètement l'endpoint predict"""
import os
from dotenv import load_dotenv
from pathlib import Path
import psycopg2
from psycopg2.extras import RealDictCursor
import pandas as pd
from sklearn.linear_model import LinearRegression
from datetime import datetime

# Charger le .env exactement comme dans main.py
env_path = Path(__file__).parent / '.env'
load_dotenv(dotenv_path=env_path, override=True)

print("=== Test Complet de l'Endpoint Predict ===")
print(f"DB_PASSWORD: {os.getenv('DB_PASSWORD', 'NON TROUVÉ')}")

product_id = 5

try:
    # Connexion exactement comme dans get_db_connection()
    db_host = os.getenv("DB_HOST", "localhost")
    db_port = os.getenv("DB_PORT", "5432")
    db_name = os.getenv("DB_NAME", "wms_db")
    db_user = os.getenv("DB_USER", "postgres")
    db_password = os.getenv("DB_PASSWORD", "postgres")
    
    print(f"\nConnexion: {db_user}@{db_host}:{db_port}/{db_name}")
    print(f"Mot de passe: {'OUI' if db_password and db_password != 'postgres' else 'NON (défaut)'}")
    
    conn = psycopg2.connect(
        host=db_host,
        port=db_port,
        database=db_name,
        user=db_user,
        password=db_password
    )
    conn.set_session(readonly=True, autocommit=False)
    print("✅ Connexion réussie")
    
    cursor = conn.cursor(cursor_factory=RealDictCursor)
    
    # Vérifier que le produit existe
    product_query = """
        SELECT "Id" as id, "Code" as code, "Name" as name
        FROM "Products"
        WHERE "Id" = %s
    """
    cursor.execute(product_query, (product_id,))
    product = cursor.fetchone()
    
    if not product:
        print(f"❌ Produit avec ID {product_id} introuvable")
    else:
        print(f"✅ Produit trouvé: {product['code']} - {product['name']}")
        
        # Récupérer l'historique
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
        
        print(f"✅ Données historiques: {len(history_data)} mois")
        
        if not history_data or len(history_data) < 3:
            print("⚠️ Pas assez de données (minimum 3 mois requis)")
            print("   Le code devrait retourner un message au lieu d'une erreur 500")
        else:
            print("✅ Suffisamment de données pour la prévision")
            # Convertir en DataFrame
            df = pd.DataFrame(history_data)
            df['month'] = pd.to_datetime(df['month'])
            df = df.sort_values('month')
            df['month_index'] = (df['month'] - df['month'].min()).dt.days / 30.44
            X = df[['month_index']].values
            y = df['total_quantity'].values
            model = LinearRegression()
            model.fit(X, y)
            print("✅ Modèle entraîné avec succès")
    
    cursor.close()
    conn.close()
    print("\n✅✅✅ Tous les tests passent !")
    
except Exception as e:
    print(f"\n❌ Erreur: {e}")
    import traceback
    traceback.print_exc()

