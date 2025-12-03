"""Script pour vérifier les noms de colonnes exacts"""
import os
from dotenv import load_dotenv
import psycopg2

load_dotenv()

conn = psycopg2.connect(
    host=os.getenv("DB_HOST", "localhost"),
    port=os.getenv("DB_PORT", "5432"),
    database=os.getenv("DB_NAME", "wms_db"),
    user=os.getenv("DB_USER", "postgres"),
    password=os.getenv("DB_PASSWORD", "postgres")
)

cursor = conn.cursor()

print("=== Colonnes Orders ===")
cursor.execute("""
    SELECT column_name 
    FROM information_schema.columns 
    WHERE table_name = 'Orders' 
    ORDER BY ordinal_position
""")
for col in cursor.fetchall():
    print(f"  - {col[0]}")

print("\n=== Colonnes OrderItems ===")
cursor.execute("""
    SELECT column_name 
    FROM information_schema.columns 
    WHERE table_name = 'OrderItems' 
    ORDER BY ordinal_position
""")
for col in cursor.fetchall():
    print(f"  - {col[0]}")

print("\n=== Test de requête similaire au code ===")
try:
    cursor.execute("""
        SELECT 
            DATE_TRUNC('month', o."OrderDate") as month,
            SUM(oi."Quantity") as total_quantity
        FROM "OrderItems" oi
        JOIN "Orders" o ON oi."OrderId" = o."Id"
        WHERE oi."ProductId" = 5
        AND o."OrderDate" >= NOW() - INTERVAL '12 months'
        GROUP BY DATE_TRUNC('month', o."OrderDate")
        ORDER BY month ASC
    """)
    results = cursor.fetchall()
    print(f"✅ Requête réussie ! {len(results)} résultats")
    for r in results:
        print(f"   {r}")
except Exception as e:
    print(f"❌ Erreur: {e}")
    import traceback
    traceback.print_exc()

cursor.close()
conn.close()

