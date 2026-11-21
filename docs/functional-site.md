# SIGM Cloud — Sitio funcional (desarrollo/demo)

## Sitio desplegado (producción)

Sitio público: **https://hcmmedical.vercel.app** (un proyecto Vercel + Neon; sin dominio propio).

| Portal | URL |
|---|---|
| Landing | https://hcmmedical.vercel.app/ |
| SIGM Clínicas | https://hcmmedical.vercel.app/app/ |
| SIGM Consultorios | https://hcmmedical.vercel.app/consultorios/ |
| Portal Pacientes | https://hcmmedical.vercel.app/pacientes/ |
| Consola Admin | https://hcmmedical.vercel.app/admin/ |

### Accesos demo (organización `CLINIC-DEMO`)

| Rol | Correo | Contraseña | Permisos |
|---|---|---|---|
| Administrador | `admin@demo.sigm.local` | `Demo12345!` | 60 |
| Médico | `doctor@demo.sigm.local` | `Doctor123!` | 22 |
| Recepción | `recepcion@demo.sigm.local` | `Recep123!` | 11 |
| **Paciente** | `ana.perez@ejemplo.test` | `Demo12345!` | 2 (portal propio) |
| Superadmin plataforma (sin tenant) | `soporte@sigm.cloud` | `Soporte2026!` | 6 |

En el campo **Organización** del login se escribe el código corto `CLINIC-DEMO`. El superadmin inicia sesión sin organización.

### Orden de carga de los datos demo en Neon

```powershell
$url = (Get-Content .env.vercel | Where-Object { $_ -match '^DATABASE_URL_UNPOOLED=' } | Select-Object -First 1) -replace '^DATABASE_URL_UNPOOLED="|"$',''
$psql = 'C:\Program Files\PostgreSQL\18\bin\psql.exe'
& $psql $url -X -v ON_ERROR_STOP=1 -f database/run_all.sql
& $psql $url -X -v ON_ERROR_STOP=1 -f database/scripts/seed_demo_full.sql
& $psql $url -X -v ON_ERROR_STOP=1 -f database/scripts/seed_demo_clinical.sql
& $psql $url -X -v ON_ERROR_STOP=1 -f database/scripts/seed_clinic_showcase.sql
& $psql $url -X -v ON_ERROR_STOP=1 -f database/scripts/seed_demo_clinical_details.sql
```

Los cuatro scripts demo son idempotentes y pueden repetirse. Resultado final: 8 pacientes, 8 atenciones, 6 notas (4 firmadas), 17 signos vitales, 9 antecedentes personales, 8 antecedentes familiares, 8 diagnósticos, 17 alergias, 8 medicamentos, 8 afiliaciones EPS, 12 órdenes de laboratorio con resultados, 8 fórmulas, 8 facturas y 9 citas.

## Requisitos
- PostgreSQL 18 en `127.0.0.1:5432` (servicio `postgresql-x64-18`, usuario `postgres`)
- .NET SDK 10.0 (`C:\Users\Julian\dotnet` en este equipo)
- Node.js 24+ / npm

## 1) Base de datos y datos ficticios
```powershell
$pg='C:\Program Files\PostgreSQL\18\bin'; $env:PGPASSWORD='postgres'
# (si no existe) crear y cargar esquema + catálogos oficiales
& "$pg\createdb.exe" -h 127.0.0.1 -p 5432 -U postgres sigm_cloud_dev
& "$pg\psql.exe" -X -v ON_ERROR_STOP=1 -h 127.0.0.1 -p 5432 -U postgres -d sigm_cloud_dev -f database/run_all.sql
# datos ficticios (tenant CLINIC-DEMO, usuarios, sede, profesional, pacientes, cita, atención, clínica)
& "$pg\psql.exe" -X -v ON_ERROR_STOP=1 -h 127.0.0.1 -p 5432 -U postgres -d sigm_cloud_dev -f database/scripts/seed_demo_full.sql
```

### Credenciales demo (CLINIC-DEMO)
| Rol | Correo | Contraseña | Organización |
|---|---|---|---|
| Administrador (`CLINIC_ADMIN`, 60 permisos) | `admin@demo.sigm.local` | `Demo12345!` | `CLINIC-DEMO` |
| Médico (`PHYSICIAN`, 22 permisos) | `doctor@demo.sigm.local` | `Doctor123!` | `CLINIC-DEMO` |
| Recepción (`RECEPTIONIST`, 11 permisos) | `recepcion@demo.sigm.local` | `Recep123!` | `CLINIC-DEMO` |
| Paciente (`PATIENT`, 2 permisos) | `ana.perez@ejemplo.test` | `Demo12345!` | `CLINIC-DEMO` |

> En el campo **Organización** del login se escribe el código corto `CLINIC-DEMO` (no distingue mayúsculas). El UUID interno `11111111-1111-4111-8111-111111111111` es solo la clave primaria en BD; el usuario nunca lo necesita. `Login` lo resuelve con `ResolveTenantAsync` y el front muestra el código en la insignia del tenant.

> Los permisos viajan en el JWT (claims `permission`); tras cambiar roles/permisos en BD hay que re-iniciar sesión.

Datos incluidos: especialidades habilitadas, sede/consultorio/departamento, profesional Dra. Laura Gómez con agenda (lun-vie 08:00-12:00, slots 30 min), 2 pacientes con expediente, cita confirmada, encuentro, nota en borrador, signos vitales, alergia y antecedente.

> Todos los datos son **ficticios**. No usar datos clínicos reales en desarrollo.

## 2) API (backend)
```powershell
$env:PATH="C:\Users\Julian\dotnet;$env:PATH"; $env:DOTNET_ROOT="C:\Users\Julian\dotnet"
dotnet run --project backend/src/SIGM.Api/SIGM.Api.csproj --urls http://localhost:5000
```
- Salud: `GET http://localhost:5000/health`
- OpenAPI (dev): `GET http://localhost:5000/openapi/v1.json`
- Conexión DB: `backend/src/SIGM.Api/appsettings.Development.json` (`sigm_cloud_dev`, usuario postgres en dev)

## 3) Frontends
En terminales separadas (con la API en ejecución):
```powershell
cd frontend/clinic-portal;   npm run dev   # http://localhost:5173
cd frontend/practice-portal; npm run dev   # http://localhost:5174 (ajustar puerto si colisiona)
cd frontend/patient-portal;  npm run dev   # http://localhost:5175
```
`VITE_API_URL` por defecto `http://localhost:5000` (`.env.development` de cada portal). Si la API corre en otro puerto, actualizar `VITE_API_URL`.

### Uso rápido
1. Abrir **SIGM Clínicas** (`clinic-portal`).
2. Iniciar sesión con `admin@demo.sigm.local` / `Demo12345!` y organización `CLINIC-DEMO`.
3. Navegar módulos: especialidades, usuarios, sedes/consultorios, profesionales, horarios (reglas/excepciones, consultar disponibilidad), pacientes, agenda/lista de espera/recordatorios, atenciones/notas, clínico (resumen, vitales, alergias, antecedentes), laboratorio, recetas, facturación/EPS/pagos, documentos, interoperabilidad y auditoría.
4. También puede **crear una nueva organización** desde la pantalla de registro (genera tenant + admin + especialidades).

### Flujo clínico en `/consultorios/` (médico)

1. Abrir **SIGM Consultorios** (`https://hcmmedical.vercel.app/consultorios/`) e iniciar sesión con `doctor@demo.sigm.local` / `Doctor123!`, organización `CLINIC-DEMO`.
2. Entrar en **Clínico** (`clinical.read`). Seleccionar un paciente (p. ej. Ana María Pérez) y pulsar **Cargar historia**.
3. La vista muestra la identificación, las alertas de alergias, las atenciones con sus notas (incluidas firmas y adendas), la línea de tiempo clínica y las tablas de alergias, diagnósticos, signos vitales y antecedentes.
4. Desde la misma pantalla se pueden registrar **notas** (borrador → **Firmar nota**, si se tiene `clinical.notes.sign`), **diagnósticos CIE-10**, **signos vitales**, **alergias** y **antecedentes**, y **firmar** las notas en borrador.
5. **Historia clínica (PDF)** abre el snapshot oficial (JSON/PDF con hash SHA-256 en `X-Document-Sha256`); **Imprimir** genera el mismo contenido en una ventana de impresión.
6. La firma de una nota crea una nueva versión firmada e inmutable y cambia su estado a `SIGNED`, cumpliendo el guard `sigm.fn_clinical_note_sign_guard()`. Las notas firmadas se corrigen con **adendas**, no se sobrescriben.

> **Especialidades:** `GET/PUT /api/v1/specialties/tenant` requiere `tenant.specialties.read` / `tenant.specialties.manage`. `PHYSICIAN` no posee esos permisos, por lo que en `/consultorios/` el menú «Especialidades» no aparece para el médico y la acción de guardado queda deshabilitada. Para habilitar especialidades se debe ingresar con `admin@demo.sigm.local` (rol `CLINIC_ADMIN`, con ambos permisos) en `/app/`. Los roles válidos en el formulario de usuarios son los del catálogo (`PHYSICIAN`, `NURSE`, `RECEPTIONIST`, `BILLER`, `MEDICAL_DIRECTOR`, `LAB_STAFF`, `CLINIC_ADMIN`, `PRACTICE_OWNER`); los antiguos `DOCTOR`, `BILLING` y `TENANT_ADMIN` no existen y hacen fallar el alta.

### Panel superadmin oculto (soporte SaaS)
- El usuario `soporte@sigm.cloud` (o cualquier cuenta con rol global `PLATFORM_ADMIN`) inicia sesión **sin necesidad de seleccionar un tenant**. El panel permite:
  - **Ver todos los tenants** activos/suspendidos con counts de pacientes, citas y planes.
  - **Activar/desactivar módulos** por cliente (appointments, laboratory, reminders, prescriptions, billing, insurers, documents, interoperability, audit, waiting-list). Los módulos desactivados quedan ocultos automáticamente en el menú y panel de inicio de cada portal.
  - **Estadísticas globales**: total de tenants, usuarios, pacientes, citas en todo el sistema.
  - **Auditoría global**: lista de eventos de login éxito/fallo con tenant y usuario.
- Los endpoints son: `GET /api/v1/platform` / `GET /api/v1/platform/stats` / `GET /api/v1/platform/tenants` / `PUT /api/v1/platform/tenants/{id}/features/{code}` / `GET /api/v1/platform/audit`.

### Dashboard personalizable por usuario
- Cada usuario autenticado puede ahorrar sus preferencias de panel de inicio (`GET /api/v1/tenant-config/dashboard` / `PUT /api/v1/tenant-config/dashboard`).
- El valor JSON guarda una lista de claves de módulos **ocultos** (p. ej. `{ "hidden": ["billing","lab"] }`). Los módulos en esa lista desaparecen del menú lateral y del panel de inicio "Inicio".
- Esto permite que cada clínica/consultorio configure qué KPIs o tiles muestra al entrar, según su flujo de trabajo.
- El panel oculta/muestra automáticamente los items del menú `nav` que correspondan al módulo: si `laboratory` está oculto, los ítems `lab`, `laboratory`, `clinical-extended`/`clinical` son removidos del menú principal.

## 4) Docker (alternativa)
```powershell
docker compose -f infrastructure/docker-compose.yml up --build
# inicializar esquema dentro del contenedor:
docker exec -i sigm-postgres psql -U postgres -d sigm_cloud_dev -v ON_ERROR_STOP=1 -f /docker-entrypoint-initdb.d-src/run_all.sql
docker exec -i sigm-postgres psql -U postgres -d sigm_cloud_dev -v ON_ERROR_STOP=1 -f /docker-entrypoint-initdb.d-src/scripts/seed_demo_full.sql
```
Puertos: API 8080, clínica 3001, consultorio 3002, paciente 3003. Ajustar `VITE_API_URL`/conexiones según corresponda.

## Notas de seguridad (dev vs prod)
- En desarrollo se usa conexión `postgres` (superusuario) para facilitar el arranque; en producción debe usarse `sigm_app` (sujeto a RLS) con `set_config('app.tenant_id'/'app.user_id')` por transacción, secretos en vault, TLS, MFA/OIDC, rate-limit y remediación de vulnerabilidades.
- Catálogos oficiales ya cargados (CIE-10/CUPS/IUM). Validación DB: `powershell -ExecutionPolicy Bypass -File database/scripts/validate.ps1 -Mode LocalServer -ServerPassword postgres`.