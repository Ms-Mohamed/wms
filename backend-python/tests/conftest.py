import os
import sys
import uuid
from pathlib import Path

import psycopg2
import pytest

ADMIN = dict(host=os.environ.get("WMS_TEST_HOST", "/tmp"), port=os.environ.get("WMS_TEST_PORT", "5433"),
             user=os.environ.get("WMS_TEST_USER", "postgres"), password=os.environ.get("WMS_TEST_PASSWORD", ""))
SCHEMA = Path(__file__).with_name("schema_analytics.sql")
SECRET = "test-secret-that-is-at-least-32-characters-long!"
os.environ.setdefault("JWT_SECRET", SECRET)   # lets pure-maths tests import main without a database
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))


@pytest.fixture(scope="session")
def dbname():
    name = "wms_an_" + uuid.uuid4().hex[:8]
    admin = psycopg2.connect(dbname="postgres", **ADMIN)
    admin.autocommit = True
    admin.cursor().execute(f'CREATE DATABASE "{name}"')
    c = psycopg2.connect(dbname=name, **ADMIN)
    c.autocommit = True
    c.cursor().execute(SCHEMA.read_text())
    c.close()
    yield name
    admin.cursor().execute(f'DROP DATABASE "{name}" WITH (FORCE)')
    admin.close()


@pytest.fixture(scope="session")
def conn(dbname):
    c = psycopg2.connect(dbname=dbname, **ADMIN)
    c.autocommit = True
    yield c
    c.close()


@pytest.fixture(scope="session")
def app_module(dbname):
    os.environ.update(JWT_SECRET=SECRET, DB_HOST=ADMIN["host"], DB_PORT=str(ADMIN["port"]), DB_NAME=dbname,
                      DB_USER=ADMIN["user"], DB_PASSWORD=ADMIN["password"], CACHE_TTL_SECONDS="0")
    os.environ.pop("AUTH_DISABLED", None)
    import importlib, sys
    sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
    import main
    importlib.reload(main)
    return main


@pytest.fixture(scope="session")
def client(app_module):
    from fastapi.testclient import TestClient
    return TestClient(app_module.app, raise_server_exceptions=False)


@pytest.fixture(scope="session")
def token():
    import jwt, time
    return jwt.encode({"unique_name": "admin", "exp": int(time.time()) + 3600}, SECRET, algorithm="HS256")


@pytest.fixture(scope="session")
def auth(token):
    return {"Authorization": f"Bearer {token}"}
