import os
import time
import requests
import pytest
from concurrent.futures import ThreadPoolExecutor
import psycopg2

API_URL = os.getenv("WMS_API_URL")
if API_URL:
    API_URL = API_URL.rstrip("/")
    if not API_URL.endswith("/api"):
        API_URL += "/api"

DB_HOST = os.getenv("DB_HOST", "localhost")
DB_PORT = os.getenv("DB_PORT", "5432")
DB_USER = os.getenv("DB_USER", "postgres")
DB_PASSWORD = os.getenv("DB_PASSWORD", "password123")
DB_NAME = os.getenv("DB_NAME", "wms_db")

pytestmark = pytest.mark.skipif(not os.getenv("WMS_API_URL"), reason="WMS_API_URL not set")

@pytest.fixture(scope="module")
def session():
    sess = requests.Session()
    res = sess.post(f"{API_URL}/auth/login", json={"username": "admin", "password": "change-me-admin"})
    res.raise_for_status()
    token = res.json()["token"]
    sess.headers.update({"Authorization": f"Bearer {token}"})
    return sess

@pytest.fixture(scope="module")
def data(session):
    # Need some products to test with.
    prods = session.get(f"{API_URL}/products").json()
    whs = session.get(f"{API_URL}/warehouses").json()
    assert prods, "No products"
    assert whs, "No warehouses"
    # Make sure p1 has some stock
    p1 = prods[0]['id']
    p2 = prods[1]['id']
    w1 = whs[0]['id']
    
    # Check stock
    s1 = session.get(f"{API_URL}/stocks/product/{p1}/warehouse/{w1}").json()
    # Assume there is enough stock for testing, since we seeded 50 and 200.
    
    return {"p1": p1, "p2": p2, "w1": w1, "q1": s1["quantity"], "s1_id": s1["id"]}

def test_order_flow(session, data):
    p1 = data["p1"]
    p2 = data["p2"]
    w1 = data["w1"]
    
    # Initial state
    s1_before = session.get(f"{API_URL}/stocks/product/{p1}/warehouse/{w1}").json()
    s2_before = session.get(f"{API_URL}/stocks/product/{p2}/warehouse/{w1}").json()
    
    # 1. Create an order with 2 lines. POST /api/orders/{id}/reserve, check stock ReservedQuantity went up.
    payload = {
        "customerName": "Test Order Flow",
        "items": [
            {"productId": p1, "warehouseId": w1, "quantity": 10},
            {"productId": p2, "warehouseId": w1, "quantity": 5}
        ]
    }
    res = session.post(f"{API_URL}/orders", json=payload)
    assert res.status_code in (200, 201), res.text
    order = res.json()
    order_id = order["id"]
    
    res = session.post(f"{API_URL}/orders/{order_id}/reserve")
    assert res.status_code == 200, res.text
    
    s1_after_reserve = session.get(f"{API_URL}/stocks/product/{p1}/warehouse/{w1}").json()
    assert s1_after_reserve["reservedQuantity"] == s1_before["reservedQuantity"] + 10
    
    # 2. A second order for the same product must not be able to take the reserved units: expect 409 or refusal.
    # We try to order the remaining free stock + 1.
    free_stock = s1_after_reserve["quantity"] - s1_after_reserve["reservedQuantity"]
    payload2 = {
        "customerName": "Test Overbook",
        "items": [{"productId": p1, "warehouseId": w1, "quantity": free_stock + 1}]
    }
    res2 = session.post(f"{API_URL}/orders", json=payload2)
    order2_id = res2.json()["id"]
    res_reserve2 = session.post(f"{API_URL}/orders/{order2_id}/reserve")
    assert res_reserve2.status_code in (400, 409), "Should refuse reservation for overbooking"
    
    # 3. Ship part of line 1. Order status must be PartiallyShipped and no invoice may exist yet.
    order_updated = session.get(f"{API_URL}/orders/{order_id}").json()
    line1 = next(item for item in order_updated["items"] if item["productId"] == p1)
    line2 = next(item for item in order_updated["items"] if item["productId"] == p2)
    
    ship_payload = {"items": [{"orderItemId": line1["id"], "locationId": 1, "quantity": 4}]}
    res = session.post(f"{API_URL}/orders/{order_id}/ship", json=ship_payload)
    assert res.status_code == 200, res.text
    
    order_after_partial = session.get(f"{API_URL}/orders/{order_id}").json()
    assert order_after_partial["status"] == "PartiallyShipped"
    
    # Check no invoice
    res_inv = session.get(f"{API_URL}/invoices/{order_id}")
    assert res_inv.status_code == 404, "No invoice should exist yet"
    
    # 4. Ship the rest of both lines. Status must be Shipped and exactly one invoice must exist.
    ship_payload2 = {
        "items": [
            {"orderItemId": line1["id"], "locationId": 1, "quantity": 6},
            {"orderItemId": line2["id"], "locationId": 1, "quantity": 5}
        ]
    }
    res = session.post(f"{API_URL}/orders/{order_id}/ship", json=ship_payload2)
    assert res.status_code == 200, res.text
    
    order_after_full = session.get(f"{API_URL}/orders/{order_id}").json()
    assert order_after_full["status"] == "Shipped"
    
    res_inv = session.get(f"{API_URL}/invoices/{order_id}")
    assert res_inv.status_code == 200, "Invoice should exist"
    
    # 5. Over-ship a line: expect 400 with a clear error.
    ship_payload_over = {"items": [{"orderItemId": line1["id"], "locationId": 1, "quantity": 1}]}
    res = session.post(f"{API_URL}/orders/{order_id}/ship", json=ship_payload_over)
    assert res.status_code == 400, "Should refuse over-shipping"
    
    # 6. Cancel an order with nothing shipped: reservations are released. Cancel an order that has shipped: expect 400.
    # order 2 has nothing shipped, but reservation failed. Let's create order 3, reserve, then cancel.
    payload3 = {
        "customerName": "Test Cancel",
        "items": [{"productId": p1, "warehouseId": w1, "quantity": 2}]
    }
    res3 = session.post(f"{API_URL}/orders", json=payload3)
    order3_id = res3.json()["id"]
    session.post(f"{API_URL}/orders/{order3_id}/reserve")
    
    s1_before_cancel = session.get(f"{API_URL}/stocks/product/{p1}/warehouse/{w1}").json()
    res_cancel = session.post(f"{API_URL}/orders/{order3_id}/cancel")
    assert res_cancel.status_code == 200
    
    s1_after_cancel = session.get(f"{API_URL}/stocks/product/{p1}/warehouse/{w1}").json()
    assert s1_after_cancel["reservedQuantity"] == s1_before_cancel["reservedQuantity"] - 2
    
    # Cancel an order that has shipped
    res_cancel_shipped = session.post(f"{API_URL}/orders/{order_id}/cancel")
    assert res_cancel_shipped.status_code == 400, "Cannot cancel a shipped order"

def test_drift(session):
    # 7. After all of that, SELECT count(*) FROM wms_stock_drift and wms_reservation_drift must both be 0.
    import subprocess
    def get_count(table):
        cmd = f'docker exec wms-db-1 psql -U postgres -d wms_db -t -c "SELECT count(*) FROM {table}"'
        res = subprocess.run(cmd, shell=True, capture_output=True, text=True)
        return int(res.stdout.strip())
        
    stock_drift = get_count("wms_stock_drift")
    res_drift = get_count("wms_reservation_drift")
    
    assert stock_drift == 0, f"wms_stock_drift is {stock_drift}"
    assert res_drift == 0, f"wms_reservation_drift is {res_drift}"

def test_idempotency(session, data):
    # 9. Re-run the idempotency check against the live API: 20 parallel POST /api/orders with the same Idempotency-Key
    # must create exactly 1 order, and the same key with a different body must return 422.
    idem_key = "test-idem-" + str(time.time())
    payload = {
        "customerName": "Test Idem",
        "items": [{"productId": data["p1"], "warehouseId": data["w1"], "quantity": 1}]
    }
    
    def fire():
        return session.post(f"{API_URL}/orders", json=payload, headers={"Idempotency-Key": idem_key})
        
    with ThreadPoolExecutor(max_workers=20) as executor:
        results = list(executor.map(lambda _: fire(), range(20)))
        
    status_codes = [r.status_code for r in results]
    assert status_codes.count(200) + status_codes.count(201) == 20
    
    # All should return the same order id
    order_ids = set([r.json()["id"] for r in results])
    assert len(order_ids) == 1
    
    # Same key, different body -> 422
    payload_diff = {
        "customerName": "Test Idem Diff",
        "items": [{"productId": data["p1"], "warehouseId": data["w1"], "quantity": 1}]
    }
    res_diff = session.post(f"{API_URL}/orders", json=payload_diff, headers={"Idempotency-Key": idem_key})
    assert res_diff.status_code == 422
