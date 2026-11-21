# SIGM Cloud — Estado de avance

Fecha de actualización: 2026-11-12 (construcción paralela de módulos, iteración 2)

## Completado
- **Base de datos (PostgreSQL 18):** 17 migraciones, 98 tablas, 1 vista, RLS/FORCE+políticas, 134 triggers, 17 funciones. Catálogos oficiales SISPRO (CIE-10 12.634, CUPS 10.024, IUM 34.642), manifiesto SHA-256, 4 pruebas SQL + validador. BD `sigm_cloud_dev` cargada y validada.
- **Backend (ASP.NET Core .NET 10, compila 0 errores; tests 1/1 OK):** solución modular `backend/SIGM.slnx`, middleware de tenant, JWT, interceptor `app.tenant_id`/`app.user_id`, `TenantConnectionFactory`, atributo base de permisos, DbContext ampliado.
  - Acceso/config: `/health`, `POST /auth/login`, `POST /organizations`, `GET/PUT /specialties[/tenant]`, `GET/POST /users`, `GET/POST /sites`, `GET/POST /medical-offices`.
  - Profesionales y pacientes: `GET/POST /professionals`, `GET/POST /patients` (crea expediente).
  - Agenda: `GET/POST /schedules`, `GET/POST /schedules/availability-rules`, `GET/POST /schedules/exceptions`, `GET/POST /appointment-types`, `GET/POST /appointments`, `POST /appointments/{id}/status`, `GET/POST /waiting-list`, `GET/POST /reminders`.
  - Clínica: `GET/POST /encounters`, `POST /encounters/{id}/notes`, `GET /notes/{id}`, `POST /notes/{id}/sign`, `POST /notes/{id}/amendments`, `GET /clinical/patients/{id}/summary`, `POST /clinical/vitals|diagnoses|allergies|history`, `GET /clinical/diagnosis-catalog`.
  - Laboratorio y recetas: `GET/POST /laboratory/orders`, `GET /laboratory/tests`, `POST /laboratory/results`, `GET/POST /prescriptions`, `GET /prescriptions/{id}/items`.
  - Facturación/EPS: `GET/POST /billing/services`, `GET/POST /billing/invoices`, `GET /billing/accounts-receivable`, `GET /insurers`, `GET /contracts`, `GET /authorizations`, `GET/POST /payments`.
  - Documentos: `GET/POST /documents` (metadatos).
  - Interoperabilidad y auditoría: `GET /interoperability/events|transmissions|fhir-mappings|rda`, `GET /audit/logs|access`.
  - Organización/soporte: `GET/POST /departments`, `GET/POST /resources`, `GET /availability` (slots por profesional/fecha). Refactor progresivo a `TenantConnectionFactory` (contexto RLS) en controladores clave.
- **Frontend (React + TypeScript + Vite, build OK):** `clinic-portal` y `practice-portal` con onboarding/login y módulos UI para especialidades, usuarios, sedes, consultorios, profesionales, horarios/disponibilidad/excepciones/tipos de cita, pacientes, agenda, lista de espera, recordatorios, atenciones, resumen/registro clínico, laboratorio, recetas, facturación, EPS/pagos/autorizaciones, documentos, interoperabilidad y auditoría; `patient-portal` base.
- **Infra/calidad:** `infrastructure/docker-compose.yml` + Dockerfiles, xUnit tests, `.github/workflows/ci.yml`, `database/scripts/seed_demo.sql`, documentación (`README`, `docs/*`, `database/docs/*`).

## Parcial / en endurecimiento
- **Seguridad:** base de RBAC (`RequirePermissionAttribute`) y `TenantConnectionFactory`/interceptor agregados; falta aplicar autorización fina en todos los endpoints, MFA/OIDC completo, refresh/revocación y sesiones/eventos persistidos, y migrar **todos** los accesos a rol `sigm_app` con GUC por transacción (parte aún usa conexión plataforma en dev). Secretos/TLS/rate-limit y remediación NU1902/NU1903 pendientes.
- **Dominio:** lógica/flujo/UI completos por módulo aún parciales (agenda: slots efectivos y envío de recordatorios; clínica: formularios por especialidad, órdenes/prescripciones completas, firma legal; documentos: almacenamiento S3 real/versionado/firmas/permisos; laboratorio/facturación/EPS: flujos y estados completos).
- **Cobertura:** no todas las 98 tablas tienen reglas/API/UI completas; auditoría de aplicación no instrumentada en todas las operaciones.

## Pendiente
- Integraciones reales: facturación electrónica DIAN (proveedor), adaptadores EPS/autorizaciones/RIPS, HL7 FHIR/IHCE/RDA y transmisión operativa.
- Pruebas de integración (API+BD+RLS), E2E Playwright, concurrencia/carga, seguridad y restauración RPO/RTO; observabilidad OpenTelemetry; CI/CD productivo e IaC/Azure endurecido.
- Revisión normativa/jurídica, datos ficticios completos y definiciones comerciales/operativas definitivas.
