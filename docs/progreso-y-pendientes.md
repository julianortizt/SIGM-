# SIGM Cloud — Progreso y pendientes

**Fecha de actualización:** 2026-11-21  
**Estado:** sitio demo desplegado, funcional y verificado en producción (Vercel + Neon).  
**URL pública:** https://hcmmedical.vercel.app · **API:** `https://hcmmedical.vercel.app/api/v1`

---

## 1. Resumen ejecutivo

| Concepto | Estado |
|---|---|
| Proyecto Vercel | `hcmmedical` · org `team_3X0SaoXsW1KggMhjctu8kCSn` · `prj_vnWsH2ij3MEAwh2K5Dk2Acw7jzgt` |
| Último deploy | `dpl_maZfdrnFikimmAcpwSLiuZDC6rD6` · commit `7a9f524` · **Ready** · alias activo |
| Portales | `/` (landing), `/app/` (clínicas), `/consultorios/`, `/pacientes/`, `/admin/` |
| Base de datos | Neon/PostgreSQL: 106 tablas, 98 policies RLS, migraciones `001`–`024` + seeds base/demo |
| Almacenamiento | `Storage__Provider=Database` → `sigm.file_blobs` (persistente en Neon) |
| Verificación E2E | **49/49 comprobaciones efectivas** en el sitio en vivo |
| Árbol Git | Limpio; todos los commits pusheados a `origin/main` |

---

## 2. Cambios guardados en Git

| Commit | Alcance |
|---|---|
| `b441aa7` | Firma de notas clínicas y permisos de especialidades en portales |
| `db87657` | Histórico clínico global y enlace «← Volver al inicio» |
| `4305d9f` | Auditoría exhaustiva: seguridad, contratos API, RLS y storage durable |
| `57d93ec` | Permisos del dashboard, corrección de seeds y documentación de auditoría |
| `7a9f524` | Conversión real snake_case→camelCase del resumen clínico y campos de formularios globales |

Archivos de mayor impacto:
- `backend/src/SIGM.Api/Controllers/ClinicalDataController.cs` — `ToCamel` real.
- `backend/src/SIGM.Api/Controllers/UsersController.cs`, `AuthController.cs`, `TokenService.cs` — bloqueo de escalada de privilegios y sesión *fail-closed*.
- `backend/src/SIGM.Api/Middleware/PostgresExceptionMiddleware.cs`, `TenantResolutionMiddleware.cs` — errores PG→4xx y tenant por JWT firmado.
- `backend/src/SIGM.Infrastructure/Persistence/TenantConnectionFactory.cs` — `set_config(..., false)` y limpieza del pool.
- `database/migrations/023_hardening_and_file_blobs.sql` — RLS/GRANTs/REVOKEs, `file_blobs`, índice de adendas.
- `database/migrations/024_form_field_tenant_trigger.sql` — trigger de campos de formularios globales.
- `database/run_all.sql` — incorpora 023 y 024.
- `frontend/*/src/**` — expiración de sesión, 401, `JSON.parse` seguro, slots ocupados, avisos parciales, `BASE_URL`.
- `docs/functional-site.md` — detalle completo de la auditoría.

---

## 3. Completado y verificado

### Infraestructura
- Un solo proyecto Vercel con la API .NET como servicio container y 5 apps Vite por subruta.
- Neon desde Vercel Marketplace; migraciones ejecutadas manualmente con `psycopg2` (no en arranque).
- Variables de producción configuradas: `ConnectionStrings__Default`, `ConnectionStrings__Platform`, `Jwt__*`, `CORS__AllowedOrigins__0`, `App__PublicUrl`, `Storage__Provider=Database`, etc.

### Seguridad
- Login con código corto `CLINIC-DEMO`; UUID interno `11111111-1111-4111-8111-111111111111`.
- `CLINIC_ADMIN` **no** puede crear `PLATFORM_ADMIN` (400 verificado); roles inválidos rechazados.
- `platform.*` solo para cuentas con claim firmado `is_platform_admin`; `/platform/modules` devuelve 403 a admin de tenant y 200 a plataforma.
- Rate limit de login/MFA por IP real; CORS restringido en producción; `ForwardLimit=1`.
- Sesiones revocables por `token_version`; validación fail-closed si la DB no responde.
- Especialidades: ocultar/deshabilitar UI según permisos, sin conceder permisos admin a médicos.

### API y contratos
- `/clinical/patients/{id}/summary` devuelve `allergenType`, `createdAt`, `measuredAt`, etc. (camelCase real).
- `GET /encounters/{id}/notes` incluye `content`; snapshot clínico sin truncamiento y con adendas filtradas por paciente.
- `POST /laboratory/results` usa las columnas reales de Neon; `POST /specialty-forms/{id}/fields` funciona con plantillas globales.
- Errores de integridad PostgreSQL traducidos a `4xx` con `traceId`.
- Export PDF con `X-Document-Sha256`; FHIR mappings autenticados (401 sin token).
- Historial clínico global accesible desde todos los módulos de `/consultorios/` y `/app/`.

### Base de datos
- `sigm_app` sin `BYPASSRLS`; `sigm_platform` con `BYPASSRLS` solo para operaciones de plataforma.
- `ENABLE RLS` + `NO FORCE` en `user_preferences`/`file_blobs` (con `FORCE` el rol propietario de Neon daba `42501`).
- GRANTs explícitos a `sigm_platform` sobre preferencias; REVOKE de DML a `sigm_app` en tablas exclusivas de plataforma.
- Seeds corregidos: doble codificación UTF-8 (`cutáneo`, `Hipertensión`) y `seed_demo.sql` redirigido a `seed_demo_full.sql`.
- Sin datos de prueba residuales: 0 usuarios E2E, 0 campos E2E, 0 resultados E2E.

### Frontends
- Los 5 portales compilan (`npm run build`) y sus bundles responden 200.
- Sesión expirada, JSON no-HTML y 401 manejados en los 4 portales autenticados.
- Slots ocupados visibles, etiqueta del paciente controlado y avisos de carga parcial.
- Enlaces de retorno al inicio respetan `BASE_URL`; eliminado código plantilla muerto.

### Resultado del E2E final
49 comprobaciones efectivas: rutas y bundles, 5 logins, permisos de especialidades, contratos clínicos, escalada bloqueada, dashboard, formularios, FHIR, PDF, laboratorio y limpieza.  
Nota: el script local informó 48/49 por un prefijo duplicado en la URL de `/api/v1/public/welcome`; la petición directa devolvió `200`, por lo que no es un fallo de la aplicación.

---

## 4. Pendientes

### Operación e infraestructura (prioridad alta)
- **Cron/workers:** configurar ejecución programada para recordatorios (`ReminderWorker`) y copias (`ScheduledBackupWorker`) en Vercel; los `IHostedService` no garantizan continuidad en serverless.
- **Backups/PITR:** confirmar retención PITR de Neon, definir RPO/RTO y realizar una prueba de restauración.
- **Variables de Preview:** las variables sensibles están configuradas solo en Production; copiarlas a Preview si se van a usar despliegues de revisión.
- **Observabilidad:** alertas sobre errores 5xx, `42501`, timeouts de DB y tasa de 429; panel de logs/auditoría.

### Seguridad (prioridad media)
- **Antes de uso real:** desactivar/eliminar cuentas demo y rotar `Jwt__Key` y contraseñas; la migración 018 ya no restaura la contraseña demo al repetir `run_all.sql`.
- Completar MFA/OIDC y refresh/revocación de tokens para usuarios finales.
- Revisar periódicamente que ningún endpoint nuevo use la conexión de plataforma por defecto sin justificación (decisión actual: **no** invertir el default `platform=true` por riesgo de romper endpoints; usar `set_config(..., false)` y limpieza explícita).
- Ampliar instrumentación de auditoría a todas las operaciones sensibles.

### Calidad y cobertura (prioridad media/baja)
- Instalar SDK .NET local para poder ejecutar `dotnet build`/tests sin depender del build de Vercel (actualmente no hay SDK en este entorno).
- Automatizar E2E con Playwright, incluyendo pruebas de concurrencia/carga y restauración.
- Ampliar cobertura de API/UI para el resto de las 106 tablas.
- Integraciones reales pendientes: facturación electrónica DIAN, adaptadores EPS/autorizaciones, RDA/HL7 FHIR y transmisión operativa.

### Demo / documentación
- Dominio propio: no proporcionado; no se crearon subdominios/DNS reales. Los portales funcionan por ruta en `hcmmedical.vercel.app`.
- Secuencia correcta de seeds: `run_all.sql` → `seed_demo_full.sql` → `seed_demo_clinical.sql` → `seed_clinic_showcase.sql` → `seed_demo_clinical_details.sql`.

---

## 5. Comandos útiles

```powershell
# Estado y deploy
git status --short
git log -3 --oneline
vercel ls
vercel inspect <url-de-deploy> --wait

# Variables (los secretos aparecen redactados con "" en el archivo descargado)
vercel env pull .env.vercel --environment=production --yes

# Builds de portales
cd frontend/practice-portal; npm run build
cd frontend/clinic-portal;  npm run build
cd frontend/patient-portal; npm run build
cd frontend/sigm-admin;     npm run build
cd frontend/sigm-welcome;   npm run build

# Base de datos (psql no está instalado en este entorno; se usa Python + psycopg2)
# Migraciones 023 y 024 ya están aplicadas en Neon.
```

---

## 6. Decisiones registradas

- Un proyecto Vercel, API .NET + 5 portales Vite, sin Laravel/Turso/R2.
- Neon desde Marketplace; migraciones manuales, nunca en arranque.
- Firma de notas: insertar nueva versión firmada e inmutable en transacción antes de `status='SIGNED'`.
- Histórico clínico global en `/consultorios/` y `/app/`; `/pacientes/` y `/admin/` solo requieren el enlace de retorno.
- Módulos bloqueados por plataforma vía `config->>'platformManaged'='true'` en `sigm.tenant_features`, sin columna nueva.
- Almacenamiento durable en Neon porque el filesystem de una función de Vercel es efímero.
