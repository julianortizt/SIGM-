# SIGM Cloud — Arquitectura técnica (inicial)

## Visión
Plataforma SaaS multi-tenant con un **núcleo clínico y administrativo compartido** y tres experiencias web:
- **SIGM Clínicas** (institucional, multi-sede/especialidad/equipo)
- **SIGM Consultorios** (profesional/independiente)
- **Portal de Pacientes**

Ambos modelos de negocio se desarrollan en paralelo sobre el mismo backend y modelo de datos.

## Stack
| Capa | Tecnología |
|---|---|
| Backend | C# / ASP.NET Core 10 LTS (`backend/src/SIGM.Api`) |
| Frontend | React + TypeScript + Vite (`frontend/*`) |
| Base de datos | PostgreSQL 18, esquema `sigm`, RLS, triggers de integridad |
| ORM/Acceso | EF Core (Npgsql) + SQL directo para operaciones de plataforma |
| Caché/sesiones/colas (objetivo) | Redis |
| Auth (objetivo) | OIDC/OAuth2, JWT, MFA para personal |
| API | REST + OpenAPI |
| Infra | Docker, Azure (objetivo), OpenTelemetry |

## Backend modular (monolito)
Solución `backend/SIGM.slnx`:
- `SIGM.Api`: host HTTP, middleware de tenant, auth JWT, controladores.
- `SIGM.Shared`: contratos transversales (contexto de tenant, reloj, abstracciones).
- `SIGM.Infrastructure`: `SigmDbContext` (EF Core) y acceso a datos.
- Módulos de dominio: `SIGM.Identity`, `SIGM.Tenancy`, `SIGM.Patients`, `SIGM.Clinical`, `SIGM.Scheduling`, `SIGM.Billing`, `SIGM.Documents`, `SIGM.Audit` (+ interoperabilidad prevista).

Dependencias: módulos → Shared/Infrastructure; Api → módulos. La extracción a servicios independientes queda condicionada a escala/necesidad.

## Multi-tenancy y seguridad
- **Aislamiento primario:** columna `tenant_id` + FKs compuestas `(tenant_id, id)` + RLS `FORCE` + políticas por tabla (ver `database/docs/security-rls.md`).
- **Contexto:** API resuelve tenant desde claims/JWT (`tenant_id`) y aplica `set_config('app.tenant_id', ...)` / `app.user_id` por conexión transaccional.
- **Roles DB:** `sigm_app` (sujeto a RLS, sin DDL/DCL, catálogos globales solo lectura), `sigm_platform` (operaciones de plataforma controladas).
- **Clínico:** notas `DRAFT→SIGNED→AMENDED`, versiones/adendas inmutables, auditoría append-only.
- **Agenda:** validación en servidor + transacciones + exclusiones GiST anti-solape (profesional/consultorio/recurso).

## Modelo de datos
98 tablas, 1 vista (`v_accounts_receivable`) cubriendo: tenants/suscripciones/configuración, identidad y RBAC, organización/sedes/profesionales/especialidades y formularios, pacientes y consentimientos, catálogos clínicos (CIE-10/CUPS/IUM/labs), registros clínicos y documentos, agenda, laboratorio, facturación/EPS/RIPS, interoperabilidad (FHIR/RDA/logs), auditoría y seguridad. Diccionario y ER en `database/docs/`.

## Primer módulo funcional (implementado/inicial)
1. **Onboarding de organización:** `POST /api/v1/organizations` crea tenant, organización, suscripción (plan), usuario admin, membresía/rol y especialidades habilitadas.
2. **Autenticación:** `POST /api/v1/auth/login` (JWT con `tenant_id`, permisos).
3. **Especialidades:** catálogo global y selección por tenant (`/api/v1/specialties`, `/api/v1/specialties/tenant`).
4. **Usuarios del tenant:** alta y listado (`/api/v1/users`).

## Próximos incrementos
- Flujos completos de profesionales/sedes/consultorios y asignación de especialidades por profesional.
- Módulo Pacientes e Historia Clínica (expediente, encuentros, notas, firma/adendas) con controles de contexto asistencial.
- Motor de agendamiento (disponibilidad, reservas atómicas, estados, recordatorios).
- Facturación electrónica (proveedor DIAN) y adaptadores EPS/RIPS por entidad/proveedor.
- Portal pacientes (citas, documentos publicados, datos permitidos), MFA, gestión de secretos y observabilidad.
- Pruebas xUnit/Playwright, endurecimiento de paquetes y pipeline CI/CD.
