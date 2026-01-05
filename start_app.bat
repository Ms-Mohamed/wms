@echo off
set PATH=%PATH%;C:\Program Files\Docker\Docker\resources\bin
echo ==============================================
echo      Starting WMS Application (Docker)
echo ==============================================
echo.
echo Stopping any running containers...
docker compose down

echo.
echo Building and Starting Services...
echo - Frontend: http://localhost
echo - C# Backend: http://localhost:8080
echo - Python Backend: http://localhost:8000
echo.
docker compose up --build

pause
