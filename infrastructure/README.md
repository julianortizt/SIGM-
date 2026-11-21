# Infraestructura SIGM Cloud

## Docker Compose
`docker-compose.yml` levanta:
- `postgres` (postgres:18) — puerto 5432, volumen `pgdata`
- `redis` (redis:7-alpine) — puerto 6379
- `api` — ASP.NET Core (`backend/src/SIGM.Api`) puerto 8080
- `clinic-portal` (3001), `practice-portal` (3002), `patient-portal` (3003)

### Uso
```powershell
docker compose up --build
```

### Base de datos
El esquema y catálogos viven en `../database` (fuera del initdb automático por tamaño/orden). Para inicializar el contenedor:
```powershell
docker exec -i sigm-postgres psql -U postgres -d sigm_cloud_dev -v ON_ERROR_STOP=1 -f /docker-entrypoint-initdb.d-src/run_all.sql
```
o usar el validador: `powershell -ExecutionPolicy Bypass -File ../database/scripts/validate.ps1 -Mode Docker`.

> Las credenciales del compose son de **desarrollo**. En producción: secretos administrados, TLS, `sigm_app` con contraseña propia, backups administrados, red privada y mínimos privilegios.

## Variables relevantes
- `ConnectionStrings__Default` / `ConnectionStrings__Platform`
- `Jwt__Issuer`, `Jwt__Audience`, `Jwt__Key`
- `Redis__Connection`
- `VITE_API_URL`, `VITE_PRODUCT` (build de frontends)

## Objetivos operativos (a validar)
Disponibilidad 99.9% mensual, RPO 15 min, RTO 4 h, ambientes separados (dev/test/prod), observabilidad (OpenTelemetry) y pruebas de restauración periódicas.
