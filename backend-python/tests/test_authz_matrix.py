"""
Authorization matrix for the analytics service, enforced by tests instead of by hand.

The routes are read from the app itself, so a new endpoint added without
`Depends(require_user)` fails here without anyone remembering to update a list.
"""
import time

import jwt
import pytest

from conftest import SECRET

PUBLIC = {"/", "/health"}   # banner + liveness: the only routes allowed to answer without a token


def _routes(app):
    out = []
    for r in app.routes:
        methods = getattr(r, "methods", None)
        path = getattr(r, "path", "")
        if not methods or path in {"/openapi.json", "/docs", "/docs/oauth2-redirect", "/redoc"}:
            continue
        for m in methods - {"HEAD", "OPTIONS"}:
            out.append((m, path))
    return sorted(out)


def _concrete(path: str) -> str:
    return path.replace("{product_id}", "1")


def _mint(secret=SECRET, alg="HS256", exp_delta=3600, **claims):
    payload = {"sub": "tester", "exp": int(time.time()) + exp_delta, **claims}
    return jwt.encode(payload, secret, algorithm=alg)


@pytest.fixture(scope="module")
def routes(app_module):
    r = _routes(app_module.app)
    assert r, "no routes discovered - the matrix would pass vacuously"
    return r


def test_every_non_public_route_rejects_a_missing_token(client, routes):
    for method, path in routes:
        if path in PUBLIC:
            continue
        resp = client.request(method, _concrete(path))
        assert resp.status_code == 401, f"{method} {path} answered {resp.status_code} without a token"


@pytest.mark.parametrize("label,token", [
    ("signed with another secret", _mint(secret="another-secret-that-is-also-32-characters!!")),
    ("expired", _mint(exp_delta=-10)),
    ("garbage", "not.a.jwt"),
    ("alg none", jwt.encode({"sub": "x", "exp": int(time.time()) + 3600}, key=None, algorithm="none")),
])
def test_every_non_public_route_rejects_a_bad_token(client, routes, label, token):
    for method, path in routes:
        if path in PUBLIC:
            continue
        resp = client.request(method, _concrete(path), headers={"Authorization": f"Bearer {token}"})
        assert resp.status_code == 401, f"{method} {path} accepted a token that is {label} ({resp.status_code})"


def test_a_valid_token_is_let_through_on_every_route(client, routes):
    good = {"Authorization": f"Bearer {_mint()}"}
    for method, path in routes:
        resp = client.request(method, _concrete(path), headers=good)
        assert resp.status_code not in (401, 403), f"{method} {path} refused a valid token"


def test_public_routes_are_exactly_the_allow_list(client, routes):
    open_routes = {path for method, path in routes if client.request(method, _concrete(path)).status_code != 401}
    assert open_routes == PUBLIC


def test_errors_do_not_leak_tracebacks(client):
    resp = client.get("/predict/1", headers={"Authorization": "Bearer nope"})
    assert "Traceback" not in resp.text and "File \"" not in resp.text
