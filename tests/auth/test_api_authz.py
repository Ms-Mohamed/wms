"""
Live authorization checks against a RUNNING C# API: token validation is middleware behaviour,
so it can only be proven by sending real HTTP requests. (The per-action [Authorize] matrix is
enforced separately and without a stack by WMS.Tests/AuthorizationMatrixTests.cs.)

  WMS_API_URL=http://localhost:8080  JWT_SECRET=<same secret the API runs with>  pytest tests/auth

Skipped when WMS_API_URL is not set.
"""
import os
import time

import jwt
import pytest
import requests

API = os.environ.get("WMS_API_URL", "").rstrip("/")
SECRET = os.environ.get("JWT_SECRET", "")
ROLE = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
NAME = "http://schemas.microsoft.com/ws/2008/06/identity/claims/name"

pytestmark = pytest.mark.skipif(not API or not SECRET, reason="set WMS_API_URL and JWT_SECRET to run against a live API")

READ_ENDPOINTS = ["/api/products", "/api/stocks", "/api/orders", "/api/warehouses", "/api/locations",
                  "/api/suppliers", "/api/purchaseorders", "/api/invoices/1", "/api/orders/1"]


def mint(role="User", secret=SECRET, exp_delta=3600, alg="HS256"):
    return jwt.encode({NAME: "tester", ROLE: role, "id": "1", "exp": int(time.time()) + exp_delta}, secret, algorithm=alg)


def get(path, token=None):
    h = {"Authorization": f"Bearer {token}"} if token else {}
    return requests.get(API + path, headers=h, timeout=10)


@pytest.mark.parametrize("path", READ_ENDPOINTS)
def test_no_token_is_401(path):
    assert get(path).status_code == 401


@pytest.mark.parametrize("label,token", [
    ("wrong secret", lambda: mint(secret="another-secret-that-is-also-32-characters!!")),
    ("expired", lambda: mint(exp_delta=-3600)),
    ("garbage", lambda: "not.a.jwt"),
    ("alg none", lambda: jwt.encode({ROLE: "Admin", "exp": int(time.time()) + 3600}, key=None, algorithm="none")),
])
@pytest.mark.parametrize("path", READ_ENDPOINTS)
def test_bad_tokens_are_401(path, label, token):
    assert get(path, token()).status_code == 401, label


def test_a_valid_user_token_is_accepted():
    assert get("/api/products", mint("User")).status_code == 200


def test_register_needs_admin():
    body = {"username": "authz_probe", "password": "Probe-12345!", "email": "probe@example.com"}
    anon = requests.post(API + "/api/auth/register", json=body, timeout=10)
    user = requests.post(API + "/api/auth/register", json=body, headers={"Authorization": f"Bearer {mint('User')}"}, timeout=10)
    assert anon.status_code == 401
    assert user.status_code == 403


def test_login_is_anonymous_and_does_not_enumerate_accounts():
    a = requests.post(API + "/api/auth/login", json={"username": "no_such_user", "password": "x"}, timeout=10)
    b = requests.post(API + "/api/auth/login", json={"username": "admin", "password": "definitely-wrong"}, timeout=10)
    assert a.status_code == b.status_code == 400
    assert a.text == b.text
