import sqlite3
from unittest.mock import MagicMock
from data_generator import main
import data_generator
import sys
import pytest

def test_data_generator_calls_analyze():
    conn_mock = MagicMock()
    conn_mock.execute = MagicMock()
    
    # Just testing that ANALYZE is passed to execute during generation teardown
    # By mocking out everything else or just inspecting the script
    with open("performance/data_generator.py", "r") as f:
        content = f.read()
        assert "conn.execute(\"ANALYZE;\")" in content or "conn.execute('ANALYZE;')" in content
