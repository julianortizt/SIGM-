# Despliegue y operación - SIGM Cloud

Guía para llevar el esquema `sigm` desde un entorno local hasta producción en Azure, con controles de migración, respaldo, rendimiento y cumplimiento.

## 1. Entornos

| Entorno | Base de datos | Datos | Objetivo |
| --- | --- | --- | --- |
| Local | PostgreSQL 18 instalado localmente o en Docker | Sintéticos | Desarrollo y pruebas |
| Integración | PostgreSQL 18 administrado | Sintéticos | Validar migraciones y RLS |
| Staging | Réplica de producción a escala reducida | Seudonimizados | Pruebas de aceptación y carga |
| Producción | PostgreSQL 18 administrado en Azure | Reales | Operación |

Recomendación: **Azure Database for PostgreSQL Flexible Server** con:
- Zona de disponibilidad redundante y réplica de lectura.
- Private Endpoint y acceso solo desde la red virtual.
- TLS obligatorio.
- Cifrado en reposo con llave administrada por el cliente (Key Vault) cuando el cliente lo exija.
- Copias automáticas con retención y restauración a un punto en el tiempo.

Verifique que las extensiones estén permitidas en el motor administrado:

```sql
CREATE EXTENSION IF NOT EXISTS pgcrypto;
CREATE EXTENSION IF NOT EXISTS btree_gist;
CREATE EXTENSION IF NOT EXISTS pg_trgm;
CREATE EXTENSION IF NOT EXISTS citext;
CREATE EXTENSION IF NOT EXISTS unaccent;
```

`btree_gist` es indispensable: sin ella no se pueden crear las restricciones de exclusión de agenda.

## 2. Prerrequisitos

- PostgreSQL 18.
- Rol propietario del esquema con permisos para `CREATE EXTENSION` y `CREATE ROLE`.
- `psql` 18 en el ejecutor de migraciones.
- Secretos en Key Vault o variables de entorno; nunca en el repositorio.
- Ventana de mantenimiento y respaldo verificado antes de migrar.

En un motor administrado, si el proveedor no permite `CREATE ROLE`, cree previamente:

```sql
CREATE ROLE sigm_app NOLOGIN;
CREATE ROLE sigm_platform NOLOGIN BYPASSRLS;
```

La migración `016` detecta la ausencia de privilegios y emite un `NOTICE`, pero no falla. El despliegue debe verificar que ambos roles existan antes de abrir tráfico.

## 3. Estrategia de migraciones

1. Las migraciones son **transaccionales** (`BEGIN` / `COMMIT` por archivo).
2. Se ejecutan en orden numérico estricto con `ON_ERROR_STOP=1`.
3. El ejecutor debe ser un **job único**; use un lock de aviso para evitar ejecuciones simultáneas:

```sql
SELECT pg_advisory_lock(hashtext('sigm_cloud_migrations'));
-- ejecutar migraciones
SELECT pg_advisory_unlock(hashtext('sigm_cloud_migrations'));
```

4. No se modifican migraciones ya aplicadas en producción. Todo cambio nuevo va en una migración con el siguiente número.
5. Cambios destructivos siguen el patrón **expandir → migrar → contraer**:
   - Agregar columnas y tablas nuevas.
   - Desplegar código compatible con ambas formas.
   - Rellenar datos por lotes.
   - Eliminar lo obsoleto en una migración posterior.
6. Los seeds son idempotentes (`ON CONFLICT`), pero en producción los catálogos globales se recargan con `sigm_platform` siguiendo [catalogs.md](catalogs.md).
7. Antes de migrar: respaldo verificado, extensión de ventana y plan de reversa.

### Comando de despliegue

```bash
export PGHOST=<host>
export PGPORT=5432
export PGDATABASE=sigm_cloud
export PGUSER=<owner>
export PGSSLMODE=require

psql -v ON_ERROR_STOP=1 -f database/run_all.sql
```

En producción no se ejecuta `run_all.sql` completo si ya hay datos: se ejecutan solo las migraciones pendientes. Lleve una tabla de control (`sigm.schema_migrations`) o use una herramienta como Flyway, Liquibase o EF Core Migrations con el mismo orden.

### Orden

```text
001 extensiones y funciones
002 tenancy y suscripciones
003 organizacion
004 identidad y acceso
005 especialidades y profesionales
006 pacientes
007 catalogos clinicos
008 agendamiento
009 historia clinica
010 documentos
011 laboratorio
012 facturacion y EPS
013 interoperabilidad
014 auditoria
015 triggers de integridad
seeds 001-002 (roles, permisos, especialidades, catalogos base)
seeds 003 (laboratorio) y 006-008 (CIE-10, CUPS e IUM oficiales)
seeds 004-005 (planes y formularios)
016 RLS y privilegios
017 endurecimiento de privilegios
```

Los seeds van antes de `016` porque insertan filas globales con `tenant_id NULL`. Si necesita recargarlos después, hágalo como `sigm_platform` o en una ventana sin RLS forzado. Los catálogos oficiales se generan con `import/import_catalogs.py`; no los edite a mano.

## 4. Verificación posterior

```sql
-- Objetos esperados
SELECT count(*) AS tablas
FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE n.nspname = 'sigm' AND c.relkind = 'r';          -- 98

SELECT count(*) AS politicas FROM pg_policies WHERE schemaname = 'sigm'; -- 96

-- RLS forzado en tablas de negocio
SELECT c.relname
FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE n.nspname = 'sigm' AND c.relkind = 'r'
  AND c.relrowsecurity AND NOT c.relforcerowsecurity;   -- solo audit_logs

-- Roles
SELECT rolname, rolbypassrls, rolcanlogin FROM pg_roles
WHERE rolname IN ('sigm_app', 'sigm_platform');
```

Pruebas obligatorias:

```bash
psql -v ON_ERROR_STOP=1 -f database/tests/001_rls_test.sql
psql -v ON_ERROR_STOP=1 -f database/tests/002_smoke_test.sql
psql -v ON_ERROR_STOP=1 -f database/tests/003_catalog_test.sql
psql -v ON_ERROR_STOP=1 -f database/tests/004_rls_coverage_test.sql
```

Las pruebas deben terminar con `ROLLBACK` sin errores. Los `NOTICE` de éxito son esperados. En Windows puede ejecutar todo en un solo paso con `database/scripts/validate.ps1 -Mode LocalServer -ServerPassword <clave>`; el script crea una base temporal, comprueba idempotencia, corre las pruebas y la elimina.

## 5. Conexión de la aplicación

Cadena de conexión de ejemplo:

```text
Host=<host>;Port=5432;Database=sigm_cloud;Username=sigm_app;Password=<secreto>;SSL Mode=Require;Trust Server Certificate=false;Pooling=true;Maximum Pool Size=50;
```

Requisitos:

- `sigm_app` sin `BYPASSRLS`.
- `SET LOCAL app.tenant_id` y `app.user_id` en cada transacción.
- Pool en modo transacción; reinicio de contexto al tomar conexión si se usa modo sesión.
- Timeouts de comando y de conexión.
- Reintentos solo para errores transitorios, nunca para errores de integridad.
- `search_path` explícito: `SET search_path = sigm, pg_catalog`.

## 6. Respaldo y recuperación

| Control | Recomendación |
| --- | --- |
| Respaldo automático | Diario con retención según política institucional |
| PITR | Habilitado; ventana alineada con RPO |
| Réplica | Réplica de lectura en otra zona |
| Geo-redundancia | Según criticidad y residencia de datos |
| Pruebas de restauración | Trimestrales, con verificación de RLS y conteos |
| Respaldo lógico | `pg_dump --schema=sigm` antes de cambios mayores |
| Cifrado | Reposo y tránsito |

Objetivos sugeridos (ajustar con el negocio):

- **RPO**: 15 minutos o menos.
- **RTO**: 4 horas para producción.

La historia clínica y los documentos tienen requisitos de conservación de largo plazo. Defina una estrategia de archivo (almacenamiento inmutable, exportación periódica, verificación de integridad por hash) conforme a la normativa colombiana aplicable y a la política de la institución.

## 7. Rendimiento

### Índices incluidos

- Búsqueda de pacientes por nombre con trigram (`ix_patients_full_name_trgm`).
- Citas por tenant, fecha, paciente, profesional y estado.
- Encuentros por paciente y profesional.
- Notas por encuentro y paciente.
- Facturas por paciente, estado y estado electrónico.
- Colas de recordatorios pendientes y mensajes de interoperabilidad.

### Recomendaciones

1. Ejecutar `ANALYZE` después de la carga inicial de catálogos.
2. Monitorear consultas lentas con `pg_stat_statements`.
3. Particionar por rango cuando el volumen lo justifique:
   - `audit_logs` y `access_logs` por mes.
   - `appointments` y `encounters` por año.
   - `interoperability_messages` y `transmission_logs` por mes.
4. Configurar autovacuum agresivo en tablas de alto movimiento (`audit_logs`, `appointments`, `invoices`).
5. Evitar `SELECT *` en tablas con `jsonb` grandes.
6. Usar paginación por cursor en auditoría y cartera.
7. Revisar bloat y reconstruir índices solo con `REINDEX CONCURRENTLY`.
8. Mantener las estadísticas al día tras migraciones masivas.

### Capacidad inicial sugerida

| Recurso | Inicial | Escala |
| --- | --- | --- |
| vCores | 4 | 8–16 según concurrencia |
| RAM | 16 GB | 32–64 GB |
| Almacenamiento | 256 GB | Crecimiento por documentos y auditoría |
| Conexiones | 100–200 | Con pooler delante |
| Réplicas de lectura | 1 | Para reportes y RIPS |

## 8. Observabilidad

Métricas mínimas:

- Conexiones activas, en espera y errores de autenticación.
- Latencia p50/p95/p99 por consulta.
- Bloqueos y transacciones largas.
- Crecimiento de tablas e índices.
- Réplicas: retraso y estado.
- Fallos de jobs de interoperabilidad, RIPS y facturación electrónica.
- Intentos de acceso denegados por RLS o permisos.

Con OpenTelemetry, propague `trace_id` y `tenant_id` en logs de aplicación, sin incluir datos clínicos identificables. Los logs de base de datos no deben capturar parámetros con información personal.

Alertas sugeridas:

- Uso de disco > 80 %.
- Conexiones > 85 % del máximo.
- Transacciones > 5 minutos.
- Réplica con retraso > 60 segundos.
- Errores 42501 (permiso denegado) por encima del umbral.
- Fallos repetidos de transmisión RDA/RIPS.

## 9. Interoperabilidad y procesos externos

| Proceso | Tabla de control | Recomendación |
| --- | --- | --- |
| FHIR/IHCE | `interoperability_messages`, `transmission_logs` | Patrón outbox con reintentos e idempotencia por `external_id` |
| RDA | `rda_documents` | Generar tras el cierre del encuentro; conservar `rda_uuid` y payload |
| RIPS | `rips_reports`, `rips_report_details` | Validar detalles antes de transmitir; conservar respuesta |
| Facturación electrónica | `invoices`, `credit_debit_notes` | Guardar CUFE, XML y respuesta DIAN |
| Recordatorios | `reminders` | Worker dedicado; reintentos con backoff |

Los workers deben ejecutarse con `sigm_platform` solo si necesitan cruzar tenants. Para procesar un tenant, usan `sigm_app` con contexto.

## 10. Respaldos y restauración manual

La consola de plataforma escribe los respaldos en `Database:BackupPath` (por defecto
`backend/backups`, fuera de `bin`) y conserva los últimos `Database:BackupRetentionCount`
(30 por defecto). Cada respaldo se registra en `sigm.database_backups` con su SHA-256 y
puede verificarse desde el panel con `pg_restore --list` sin tocar la base en uso.

Además, la API programa respaldos automáticos (`Database:AutoBackupEnabled=true`,
`Database:AutoBackupIntervalHours=24`). Cada ejecución crea el archivo, lo registra, lo
verifica y aplica la retención; si falla, deja el error en el log y reintenta en 30 minutos.
El estado se consulta en `GET /api/v1/platform/database/status` (`lastAutomaticBackupAt`).

Los scripts destructivos requieren **doble aprobación**: otro `PLATFORM_ADMIN` distinto del
autor y de quien aplica debe aprobarlos con su correo y contraseña (`POST .../scripts/{id}/approve`).
Sin esa aprobación, `apply` responde 400 con `requiresApproval=true`. El respaldo previo sigue
siendo obligatorio y se cancela la aplicación si no supera la verificación.

La configuración de producción debe venir por variables de entorno o gestor de secretos
(`backend/.env.example`): cadena de conexión, `Jwt__Key` (mínimo 32 caracteres), CORS, SMTP y
ruta de respaldos. En Production la API rechaza credenciales locales o `postgres/postgres`.

Restauración manual (nunca desde el panel, para evitar reemplazar la base productiva por error):

```bash
# 1. Detener la API y confirmar el archivo elegido.
pg_restore --list /ruta/backups/sigm_YYYYMMDD_HHMMSS_<hash>.dump | head

# 2. Restaurar en una base nueva para validar antes de cualquier cambio.
createdb -h 127.0.0.1 -U postgres sigm_restore_check
pg_restore -h 127.0.0.1 -U postgres -d sigm_restore_check --no-owner --no-privileges \
  /ruta/backups/sigm_YYYYMMDD_HHMMSS_<hash>.dump

# 3. Validar conteos y RLS en la copia.
psql -h 127.0.0.1 -U postgres -d sigm_restore_check -c "SELECT count(*) FROM sigm.patients;"
psql -h 127.0.0.1 -U postgres -d sigm_restore_check -c "SELECT count(*) FROM sigm.audit_logs;"

# 4. Solo con ventana aprobada: renombrar la base actual y restaurar sobre el nombre real.
```

Tras una restauración real hay que reiniciar la API, probar login (incluido MFA) y revisar
`sigm.audit_logs` para confirmar que la bitácora quedó disponible.

## 11. Runbook de despliegue

1. Congelar cambios de esquema y anunciar ventana.
2. Verificar respaldo y restauración reciente.
3. Ejecutar pruebas en staging con una copia seudonimizada.
4. Aplicar migraciones pendientes con `ON_ERROR_STOP=1`.
5. Ejecutar pruebas RLS y funcionales.
6. Verificar conteos, políticas y roles.
7. Desplegar la aplicación compatible y comprobar `/health/ready`.
8. Habilitar tráfico gradualmente.
9. Verificar que el respaldo automático se ejecutó (`lastAutomaticBackupAt`) y que un script
destructivo exige aprobación de otra persona.
10. Monitorear errores, latencia y bloqueos durante 24 horas.
11. Registrar la versión aplicada y actualizar el inventario.

### Reversa

- Las migraciones transaccionales se revierten con el rollback de la transacción si fallan antes del `COMMIT`.
- Si el fallo ocurre después del despliegue, aplique una migración correctiva; no edite la migración original.
- Para cambios destructivos, mantenga la columna o tabla antigua hasta que el código nuevo esté estable.
- Restaure PITR solo como último recurso, con aislamiento del entorno y validación posterior.

## 12. Cumplimiento normativo

El modelo ofrece soporte técnico para:

- **Resolución 1995 de 1999**: historia clínica, notas, autoría y conservación.
- **Resolución 839 de 2017**: condiciones de conservación y disponibilidad de la historia clínica.
- **Ley 1581 de 2012 y Decreto 1377 de 2013**: consentimiento, derechos del titular y incidentes.
- **Ley 2015 de 2020**: interoperabilidad de la historia clínica.
- **Resolución 866 de 2021**: IHCE y FHIR.
- **Resolución 1888 de 2025**: Resumen Digital de Atención (RDA).

El cumplimiento no se logra solo con el esquema. Requiere políticas, contratos, capacitación, auditoría, evaluación de impacto y governance de datos.

## 13. Lista de verificación final

- [ ] Extensiones instaladas y verificadas.
- [ ] Roles `sigm_app` y `sigm_platform` creados con los atributos correctos.
- [ ] Migraciones aplicadas en orden y registradas.
- [ ] Seeds cargados; catálogos oficiales completos.
- [ ] RLS y pruebas funcionales en verde.
- [ ] Copias de seguridad y PITR probados.
- [ ] TLS, Private Endpoint y firewall configurados.
- [ ] Secretos en Key Vault y rotación definida.
- [ ] Monitoreo, alertas y tableros activos.
- [ ] Retención y archivo de historia clínica documentados.
- [ ] Procedimiento de incidentes y notificación a la SIC.
- [ ] Integraciones FHIR/RDA/RIPS/DIAN con reintentos e idempotencia.
