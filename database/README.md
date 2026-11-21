# SIGM Cloud - Base de datos

Modelo de datos PostgreSQL 18 para la plataforma médica multi-tenant **SIGM Cloud**, que soporta dos productos sobre un mismo núcleo:

- **SIGM Clínicas**: instituciones con varias sedes, especialidades, facturación, EPS/RIPS y laboratorio.
- **SIGM Consultorios**: profesionales independientes o consultorios pequeños, con especialidades configurables.

El esquema usa un **monolito modular multi-tenant con base de datos compartida**: cada fila de negocio lleva `tenant_id`, las claves foráneas son compuestas `(tenant_id, id)` y PostgreSQL aplica aislamiento con **Row Level Security (RLS)** forzado.

## Estado de validación

Validado contra PostgreSQL 18.4 (instancia local) y la imagen oficial `postgres:18` (Docker):

| Elemento | Cantidad |
| --- | --- |
| Tablas | 98 |
| Tablas con `tenant_id` | 85 (84 con `FORCE RLS` + `audit_logs`) |
| Vistas | 1 (`sigm.v_accounts_receivable`) |
| Índices | 286 |
| Restricciones | 1.337 |
| Políticas RLS | 96 |
| Triggers | 134 |
| Funciones | 17 |

Pruebas ejecutadas correctamente:

- `tests/001_rls_test.sql`: aislamiento entre dos tenants, intento de lectura, escritura y actualización cruzada, y bloqueo de escalamiento de privilegios.
- `tests/002_smoke_test.sql`: agenda con exclusión de solapamientos, nota clínica firmada inmutable, recálculo de totales de factura y vista de cartera.
- `tests/003_catalog_test.sql`: volúmenes, integridad y códigos de control de los catálogos oficiales.
- `tests/004_rls_coverage_test.sql`: toda tabla con `tenant_id` tiene RLS, `FORCE` (salvo `audit_logs`) y política; las FK entre tablas de tenant son compuestas `(tenant_id, id)` o tienen trigger de validación.

La re-ejecución completa de `run_all.sql` es idempotente y se comprueba en cada
validación. Última validación: 12/11/2026, con `validate.ps1 -Mode LocalServer`
y `-Mode Docker` (migraciones, segunda ejecución y tres pruebas en verde).

### Catálogos oficiales

`run_all.sql` carga los catálogos completos de SISPRO:

| Catálogo | Tabla | Registros | Seed |
| --- | --- | ---: | --- |
| CIE-10 | `diagnosis_catalog` | 12.634 | `006_catalogs_cie10.sql` |
| CUPS | `procedure_catalog` | 10.024 | `007_catalogs_cups.sql` |
| IUM | `medication_catalog` | 34.642 | `008_catalogs_ium.sql` |

El proceso de descarga, mapeo, hashes y reimportación está documentado en
[Catálogos oficiales](docs/catalogs.md).

## Estructura

```text
database/
├── migrations/
│   ├── 001_extensions_and_schemas.sql
│   ├── 002_tenancy_and_subscriptions.sql
│   ├── 003_organization.sql
│   ├── 004_identity_access.sql
│   ├── 005_specialties_professionals.sql
│   ├── 006_patients.sql
│   ├── 007_clinical_catalogs.sql
│   ├── 008_scheduling.sql
│   ├── 009_clinical_records.sql
│   ├── 010_documents.sql
│   ├── 011_laboratory.sql
│   ├── 012_billing_eps.sql
│   ├── 013_interoperability.sql
│   ├── 014_audit.sql
│   ├── 015_integrity_triggers.sql
│   ├── 016_rls_and_grants.sql
│   └── 017_security_hardening.sql
├── seeds/
│   ├── 001_roles_permissions.sql
│   ├── 002_specialties_and_catalogs.sql
│   ├── 003_clinical_catalogs_sample.sql   (respaldo sin conectividad)
│   ├── 003_laboratory_tests_sample.sql
│   ├── 004_plans.sql
│   ├── 005_specialty_forms_sample.sql
│   ├── 006_catalogs_cie10.sql             (generado, 12.634 filas)
│   ├── 007_catalogs_cups.sql              (generado, 10.024 filas)
│   └── 008_catalogs_ium.sql               (generado, 34.642 filas)
├── import/
│   ├── import_catalogs.py
│   └── data/
│       ├── cie10.csv
│       ├── cups.csv
│       ├── ium.csv
│       └── manifest.json
├── tests/
│   ├── 001_rls_test.sql
│   ├── 002_smoke_test.sql
│   ├── 003_catalog_test.sql
│   └── 004_rls_coverage_test.sql
├── docs/
│   ├── catalogs.md
│   ├── data-dictionary.md
│   ├── er-model.md
│   ├── security-rls.md
│   └── deployment.md
├── scripts/
│   └── validate.ps1
└── run_all.sql
```

## Ejecución rápida

### Docker

```bash
docker run -d --name sigm-postgres \
  -e POSTGRES_PASSWORD=sigm_test \
  -e POSTGRES_DB=sigm_cloud \
  -p 55432:5432 \
  -v "$PWD/database:/db" \
  postgres:18

docker exec sigm-postgres psql -U postgres -d sigm_cloud -v ON_ERROR_STOP=1 -f /db/run_all.sql
docker exec sigm-postgres psql -U postgres -d sigm_cloud -v ON_ERROR_STOP=1 -f /db/tests/001_rls_test.sql
docker exec sigm-postgres psql -U postgres -d sigm_cloud -v ON_ERROR_STOP=1 -f /db/tests/002_smoke_test.sql
docker exec sigm-postgres psql -U postgres -d sigm_cloud -v ON_ERROR_STOP=1 -f /db/tests/003_catalog_test.sql
```

### PostgreSQL local

```bash
createdb -h localhost -p 5432 -U postgres sigm_cloud
psql -h localhost -p 5432 -U postgres -d sigm_cloud -v ON_ERROR_STOP=1 -f database/run_all.sql
psql -h localhost -p 5432 -U postgres -d sigm_cloud -v ON_ERROR_STOP=1 -f database/tests/001_rls_test.sql
psql -h localhost -p 5432 -U postgres -d sigm_cloud -v ON_ERROR_STOP=1 -f database/tests/002_smoke_test.sql
psql -h localhost -p 5432 -U postgres -d sigm_cloud -v ON_ERROR_STOP=1 -f database/tests/003_catalog_test.sql
```

### Validación automatizada en Windows

```powershell
# Contra una instancia PostgreSQL ya en ejecución (recomendado, ~30 s)
.\database\scripts\validate.ps1 -Mode LocalServer -ServerPassword postgres

# Clúster efímero con initdb/pg_ctl (sin Docker)
.\database\scripts\validate.ps1 -Mode LocalCluster

# Contenedor temporal
.\database\scripts\validate.ps1 -Mode Docker
```

Con `-Mode Auto` (por defecto) el script usa la instancia local si hay
credenciales (`-ServerPassword` o `PGPASSWORD`), luego un clúster efímero y, por
último, Docker. Crea una base `sigm_cloud_validate`, ejecuta `run_all.sql` dos
veces para comprobar idempotencia, corre las tres pruebas y elimina la base al
terminar. Los roles `sigm_app`/`sigm_platform` solo se eliminan si el script los
creó; si ya existían, se conservan.

## Orden de ejecución

`run_all.sql` ejecuta deliberadamente los seeds **antes** de la migración `016`:

1. Migraciones `001` a `015`: extensiones, tablas, índices, restricciones y triggers.
2. Seeds: roles, permisos, especialidades, catálogos oficiales completos, planes y formularios.
3. Migración `016`: habilita RLS, crea roles de base de datos y concede privilegios.
4. Migración `017`: endurece privilegios, protege catálogos globales y evita escalamiento de roles.

Los catálogos globales usan `tenant_id NULL` y se siembran antes de `FORCE ROW LEVEL SECURITY`. En producción, cualquier recarga de catálogos debe hacerse con el rol `sigm_platform` o durante una ventana de mantenimiento. Los seeds `006`–`008` son idempotentes (`ON CONFLICT DO UPDATE`).

## Contexto de sesión

Toda transacción del backend debe establecer el tenant y el usuario autenticados antes de consultar datos:

```sql
BEGIN;
SET LOCAL app.tenant_id = '11111111-1111-1111-1111-111111111111';
SET LOCAL app.user_id   = '22222222-2222-2222-2222-222222222222';

-- consultas y escrituras de la aplicación

COMMIT;
```

`sigm.current_tenant_id()` y `sigm.current_user_id()` leen esas variables. Sin contexto, las políticas RLS no devuelven filas.

## Roles de base de datos

| Rol | Uso | RLS |
| --- | --- | --- |
| `sigm_app` | Backend en tiempo de ejecución | Sujeto a RLS |
| `sigm_platform` | Aprovisionamiento, autenticación, tareas globales, recarga de catálogos | `BYPASSRLS` |
| Propietario del esquema | Migraciones | Omite RLS por ser owner |

El backend **nunca** debe conectarse como propietario ni como `sigm_platform` para atender tráfico de usuarios.

## Convenciones del modelo

- Claves primarias `uuid` con `gen_random_uuid()`.
- Claves foráneas compuestas `(tenant_id, id)` para impedir referencias cruzadas entre tenants.
- Columnas de auditoría `created_at`, `updated_at`, `created_by` cuando aplica.
- Estados y tipos cerrados con `CHECK` en lugar de enums nativos, para facilitar migraciones.
- Catálogos globales con `tenant_id NULL`; personalizaciones con `tenant_id` propio.
- Fechas y horas en `timestamptz`; zona horaria por defecto `America/Bogota`.
- Dinero en `numeric(14,2)`; moneda por defecto `COP`.
- Contenido clínico estructurado en `jsonb` versionado e inmutable.
- Archivos fuera de la base: solo se guardan metadatos y `storage_key`.

## Documentación

- [Catálogos oficiales](docs/catalogs.md): fuentes SISPRO, importación, hashes y reimportación.
- [Diccionario de datos](docs/data-dictionary.md): columnas, tipos y defaults de las 98 tablas.
- [Modelo entidad-relación](docs/er-model.md): módulos, relaciones y diagramas.
- [Seguridad y RLS](docs/security-rls.md): aislamiento, roles, auditoría e inmutabilidad.
- [Despliegue](docs/deployment.md): entornos, migraciones, respaldo, retención y operación.

## Advertencias

- Los catálogos CIE-10, CUPS e IUM se cargan completos desde SISPRO. Los exámenes de laboratorio siguen siendo un catálogo operativo de ejemplo que cada institución debe personalizar.
- Los precios de los planes son parámetros de ejemplo, no una decisión comercial.
- La facturación electrónica, RIPS, FHIR/IHCE y RDA requieren integraciones externas que no forman parte de este esquema.
- Las pruebas se ejecutan dentro de transacciones con `ROLLBACK`; no dejan datos.
