# SIGM Cloud

Plataforma SaaS multi-tenant para gestión médica (Colombia). Núcleo compartido para dos productos:
- **SIGM Clínicas** (`frontend/clinic-portal`)
- **SIGM Consultorios** (`frontend/practice-portal`)
- **Portal Pacientes** (`frontend/patient-portal`)
- **SIGM Welcome** (`frontend/sigm-welcome`) — sitio público / landing
- **SIGM Admin** (`frontend/sigm-admin`) — panel de plataforma (superadmin)

## Accesos en producción
Sitio funcionando en **https://hcmmedical.vercel.app** (un solo proyecto Vercel + Neon; sin dominio propio ni subdominios):

| Portal | URL |
|---|---|
| Landing / bienvenida | https://hcmmedical.vercel.app/ |
| SIGM Clínicas | https://hcmmedical.vercel.app/app/ |
| SIGM Consultorios | https://hcmmedical.vercel.app/consultorios/ |
| Portal Pacientes | https://hcmmedical.vercel.app/pacientes/ |
| Consola Admin | https://hcmmedical.vercel.app/admin/ |
| Salud / API | https://hcmmedical.vercel.app/health/ready · https://hcmmedical.vercel.app/api/v1/public/welcome |

Si se compra un dominio propio, se pueden añadir subdominios (`app.`, `consultorios.`, etc.) en `vercel.json` + DNS; mientras tanto, las rutas anteriores son la forma oficial de acceso. Detalles en `infrastructure/VERCEL-DEPLOY.md`.

### Accesos demo
Todos usan la organización `CLINIC-DEMO` en el login (excepto el superadmin, que entra sin organización):

| Rol | Correo | Contraseña |
|---|---|---|
| Administrador | `admin@demo.sigm.local` | `Demo12345!` |
| Médico | `doctor@demo.sigm.local` | `Doctor123!` |
| Recepción | `recepcion@demo.sigm.local` | `Recep123!` |
| Paciente | `ana.perez@ejemplo.test` | `Demo12345!` |
| Superadmin | `soporte@sigm.cloud` | `Soporte2026!` |

Las 8 historias clínicas demo (con vitales, antecedentes, diagnósticos, laboratorio, fórmulas y facturación) se cargan con los scripts de `database/scripts/` en este orden: `run_all.sql` → `seed_demo_full.sql` → `seed_demo_clinical.sql` → `seed_clinic_showcase.sql` → `seed_demo_clinical_details.sql`. Ver `docs/functional-site.md`.

## Estado
- **Base de datos (PostgreSQL 18):** completa y validada en `database/` (22 migraciones, seeds con catálogos CIE-10/CUPS/IUM desde SISPRO, RLS, pruebas y validador). Ver `database/README.md`.
- **Backend (.NET 10 / ASP.NET Core):** la API en `backend/src/SIGM.Api` concentra la lógica real: JWT/MFA, multi-tenant + RLS, permisos, agenda, clínico, facturación, documentos y auditoría (~190 rutas). Los otros proyectos de la solución son cáscaras vacías. ¿Para qué sirve? Ver `infrastructure/VERCEL-DEPLOY.md` §0.
- **Frontend (React + TypeScript, Vite):** portales en `frontend/` (clínica, consultorio, pacientes, welcome, admin) + `shared-components`.
- **Infraestructura:** `infrastructure/docker-compose.yml` (Postgres, Redis, API, portales).

## Requisitos
- PostgreSQL 18 (local en `C:\Program Files\PostgreSQL\18\bin` o Docker)
- .NET SDK 10.0 (`C:\Users\Julian\dotnet` en este equipo)
- Node.js 24+ y npm

## Base de datos
```powershell
# Crear y cargar base de desarrollo
$pg='C:\Program Files\PostgreSQL\18\bin'; $env:PGPASSWORD='postgres'
& "$pg\createdb.exe" -h 127.0.0.1 -p 5432 -U postgres sigm_cloud_dev
& "$pg\psql.exe" -X -v ON_ERROR_STOP=1 -h 127.0.0.1 -p 5432 -U postgres -d sigm_cloud_dev -f database/run_all.sql

# Validación completa (crea/elimina base temporal)
powershell -ExecutionPolicy Bypass -File database/scripts/validate.ps1 -Mode LocalServer -ServerPassword postgres
```

## Backend
```powershell
$env:PATH="C:\Users\Julian\dotnet;$env:PATH"; $env:DOTNET_ROOT="C:\Users\Julian\dotnet"
dotnet build backend/SIGM.slnx
dotnet run --project backend/src/SIGM.Api/SIGM.Api.csproj
```
API: `http://localhost:5000` (perfil `http` de `launchSettings`: `http://localhost:5067`), OpenAPI en `/openapi/v1.json` (dev), salud en `/health`.
Configurar conexión en `backend/src/SIGM.Api/appsettings.Development.json`.

## Frontend
```powershell
cd frontend/clinic-portal; npm run dev    # :5173
cd frontend/practice-portal; npm run dev  # :5174
cd frontend/patient-portal; npm run dev   # :5175
cd frontend/sigm-welcome; npm run dev     # :3001
cd frontend/sigm-admin; npm run dev       # :3002
```
Variable `VITE_API_URL` (por defecto `http://localhost:5000`). Cada portal puede fijarla en su `.env.development` cuando la API corre en otro puerto o host.

## Docker
```powershell
docker compose -f infrastructure/docker-compose.yml up --build
```
> Nota: la carga inicial del esquema/catálogos en el contenedor Postgres debe ejecutarse apuntando `database/run_all.sql` a la base (ver `database/docs/deployment.md`).

## Módulos/API implementados (en progreso)
- **Onboarding y acceso:** `POST /api/v1/organizations`, `POST /api/v1/auth/login`
- **Configuración:** `GET/PUT /api/v1/specialties[/tenant]`, `GET/POST /api/v1/users`, `GET/POST /api/v1/sites`
- **Profesionales:** `GET/POST /api/v1/professionals` (vínculo a usuario/sede/especialidades habilitadas)
- **Pacientes:** `GET/POST /api/v1/patients` (crea expediente clínico base)
- **Agenda:** `GET/POST /api/v1/appointments`, `POST /api/v1/appointments/{id}/status` (respeta exclusiones GiST anti-solape)
- **Atención clínica:** `GET/POST /api/v1/encounters`, `POST /api/v1/encounters/{id}/notes`, `GET /api/v1/notes/{id}`, `POST /api/v1/notes/{id}/sign`, `POST /api/v1/notes/{id}/amendments`

Frontends: flujos de registro/login y gestión de especialidades, usuarios, sedes, profesionales, pacientes, agenda y atenciones en `clinic-portal`/`practice-portal`; portal paciente base. Ver `docs/status.md`.

## Seguridad
- Multi-tenant con `tenant_id`, RLS en PostgreSQL y contexto de tenant en API (`X-Tenant-Id`/claims).
- No usar datos clínicos reales en desarrollo. Cambiar secretos JWT y credenciales en producción.
- Ver `database/docs/security-rls.md` y `docs/`.

## Calidad y automatización
- Pruebas SQL: `database/tests/001`–`004` (RLS, smoke, catálogos, cobertura RLS) y validador `database/scripts/validate.ps1`.
- Backend: `backend/tests/SIGM.Api.Tests` (xUnit).
- CI: `.github/workflows/ci.yml` (validación BD, build/test backend, build frontends).
- Datos demo (ficticios): `database/scripts/seed_demo.sql`.

## Documentación
- `docs/architecture.md`, `docs/requirements.md`, `docs/status.md`
- `database/docs/` — modelo, diccionario, seguridad/RLS, catálogos, despliegue
- `infrastructure/VERCEL-DEPLOY.md` — despliegue del monorepo en Vercel + Neon
- `sigm medical.docx` — documento fuente de requisitos y arquitectura
