# Seguridad, RLS y trazabilidad - SIGM Cloud

Este documento describe cómo el modelo de datos impide el acceso cruzado entre tenants, cómo se protege la evidencia clínica y qué controles debe implementar la aplicación para completar la seguridad.

## 1. Principios

1. **Aislamiento por fila**: toda tabla de negocio lleva `tenant_id` y RLS forzado.
2. **Integridad referencial por tenant**: las claves foráneas son compuestas `(tenant_id, id)`.
3. **Mínimo privilegio**: la aplicación usa `sigm_app`, sin `BYPASSRLS` y sin ser propietaria del esquema.
4. **Evidencia inmutable**: versiones de nota, adendas, firmas y logs no admiten `UPDATE` ni `DELETE`.
5. **Defensa en profundidad**: RLS no reemplaza la autorización de la aplicación ni las revisiones de permisos.
6. **Trazabilidad**: toda operación sensible debe poder atribuirse a un usuario, un tenant y un momento.

## 2. Roles de base de datos

| Rol | Atributos | Uso | RLS |
| --- | --- | --- | --- |
| Propietario del esquema | Superusuario o rol de migraciones | Crear y migrar objetos | Omite RLS |
| `sigm_app` | `NOLOGIN` | Backend en tiempo de ejecución | Sujeto a RLS |
| `sigm_platform` | `NOLOGIN BYPASSRLS` | Aprovisionamiento, autenticación, tareas globales | Omite RLS |

En Azure Database for PostgreSQL flexible server los roles `NOLOGIN` se activan con `SET ROLE` desde un rol de servicio autenticado. La contraseña de `sigm_app` debe gestionarse fuera del esquema (Key Vault / variables de entorno) y rotarse periódicamente.

### Privilegios de `sigm_app`

- `USAGE` sobre el esquema `sigm`.
- `SELECT, INSERT, UPDATE, DELETE` sobre tablas de negocio.
- `USAGE, SELECT` sobre secuencias.
- `EXECUTE` sobre funciones de aplicación.

Excepciones deliberadas:

| Tabla | Restricción |
| --- | --- |
| `audit_logs` | Sin `UPDATE` ni `DELETE` |
| `access_logs` | Sin `UPDATE` ni `DELETE` |
| `authentication_events` | Sin `UPDATE` ni `DELETE` |
| `transmission_logs` | Sin `UPDATE` ni `DELETE` |
| `clinical_note_versions` | Sin `UPDATE` ni `DELETE` |
| `clinical_amendments` | Sin `UPDATE` ni `DELETE` |
| `document_signatures` | Sin `UPDATE` ni `DELETE` |
| `clinical_notes` | Sin `DELETE` |
| `encounters` | Sin `DELETE` |
| `patients` | Sin `DELETE` (se inactiva con `is_active`) |
| `invoices` | Sin `DELETE` (se anula con `status = 'VOID'`) |
| `payments` | Sin `DELETE` (se reversa con nota o ajuste) |

Los catálogos globales son **solo lectura** para `sigm_app`:

`plans`, `specialties`, `permissions`, `document_types`, `diagnosis_catalog`, `procedure_catalog`, `medication_catalog`, `laboratory_tests`, `clinical_note_types`, `insurers`.

Solo `sigm_platform` o el propietario pueden modificarlos (migración `017`).

## 3. Contexto de sesión

Las políticas leen dos variables de sesión:

```sql
SELECT set_config('app.tenant_id', '<uuid del tenant>', true);
SELECT set_config('app.user_id',   '<uuid del usuario>', true);
```

O, en su forma habitual:

```sql
BEGIN;
SET LOCAL app.tenant_id = '11111111-1111-1111-1111-111111111111';
SET LOCAL app.user_id   = '22222222-2222-2222-2222-222222222222';
-- trabajo de la transacción
COMMIT;
```

Reglas obligatorias del backend:

1. El `tenant_id` **nunca** se toma de un encabezado, query string o body sin validar la membresía del usuario.
2. El contexto se establece **después** de autenticar y **dentro** de la misma transacción que ejecuta las consultas.
3. Se usa `SET LOCAL` / `set_config(..., true)`, nunca `SET SESSION`, para no filtrar contexto entre conexiones reutilizadas.
4. El pool de conexiones debe operar en modo transacción; si se usa PgBouncer en modo sesión, cada préstamo de conexión debe reiniciar el contexto.
5. Un tenant `SUSPENDED` o `CANCELLED` debe rechazarse antes de abrir el contexto.
6. Nunca se concatena SQL con el `tenant_id`; siempre se parametriza.

Sin `app.tenant_id`, `sigm.current_tenant_id()` devuelve `NULL` y las políticas no devuelven filas. Esto es un fallo cerrado.

## 4. Inventario de políticas

| Política | Tablas | Comando | Regla |
| --- | --- | --- | --- |
| `tenant_isolation` | 80 | `ALL` | `tenant_id = current_tenant_id()` en `USING` y `WITH CHECK` |
| `global_or_tenant_select` | 5 | `SELECT` | `tenant_id IS NULL OR tenant_id = current_tenant_id()` |
| `tenant_write` | 5 | `ALL` | Solo filas del tenant actual |
| `tenant_self_select` | 1 | `SELECT` | `tenants.id = current_tenant_id()` |
| `tenant_self_update` | 1 | `UPDATE` | El tenant actual solo actualiza su propia fila |
| `users_tenant_read` | 1 | `SELECT` | El propio usuario o usuarios miembros del tenant actual |
| `users_self_update` | 1 | `UPDATE` | El usuario solo se actualiza a sí mismo |
| `role_permissions_read` | 1 | `SELECT` | Roles globales o del tenant actual |
| `role_permissions_write` | 1 | `ALL` | Solo roles propios del tenant actual |

Total: **96 políticas** sobre 88 tablas con RLS. Las 10 tablas restantes son catálogos globales sin `tenant_id`, protegidos por privilegios de solo lectura.

### Tablas con filas globales y filas del tenant

`roles`, `document_categories`, `specialty_forms`, `specialty_form_fields` y `fhir_mappings` usan dos políticas:

- Lectura: filas globales (`tenant_id NULL`) y filas del tenant.
- Escritura: solo filas del tenant; la aplicación no puede alterar plantillas globales.

### Casos especiales

- **`tenants`**: el cliente solo ve y actualiza su propia fila. Crear o suspender tenants es una operación de plataforma.
- **`users`**: la identidad es global. La aplicación ve al usuario autenticado y a los miembros del tenant actual. La creación de usuarios y el login inicial deben pasar por el servicio de identidad con `sigm_platform`.
- **`role_permissions`**: sin `tenant_id`; la política resuelve el alcance consultando `roles`. Un rol de tenant no puede recibir permisos del módulo `platform`.
- **`audit_logs`**: `NO FORCE ROW LEVEL SECURITY` para que el trigger `SECURITY DEFINER` pueda escribir aunque no haya contexto. `sigm_app` sigue sujeto a la política al leer y no puede ejecutar la función de auditoría.

## 5. Integridad y privilegios

La migración `017_security_hardening.sql` cierra tres brechas:

1. **Catálogos globales**: se revocan `INSERT`, `UPDATE` y `DELETE` a `sigm_app`.
2. **`role_permissions`**: se habilita RLS y se valida que un rol de tenant solo reciba permisos no globales.
3. **`PLATFORM_ADMIN`**: solo `sigm_platform` puede asignarlo; un administrador de tenant no puede elevarse a plataforma.

Además, `015_integrity_triggers.sql` valida:

- Un rol asignado debe ser global o del mismo tenant.
- Una categoría documental debe ser global o del mismo tenant.
- Un campo de formulario debe pertenecer al tenant del formulario.

## 6. Evidencia clínica

### Notas clínicas

Estados: `DRAFT → SIGNED → AMENDED`, con `VOID` para descarte.

Controles:

| Control | Implementación |
| --- | --- |
| Versiones inmutables | Trigger `prevent_mutation` sobre `clinical_note_versions` |
| Adendas inmutables | Trigger `prevent_mutation` sobre `clinical_amendments` |
| No versionar después de firmar | `fn_clinical_note_version_after_insert` |
| Firma exige versión firmada | `fn_clinical_note_sign_guard` |
| No borrar notas firmadas | `fn_prevent_signed_note_delete` |
| Autoría obligatoria | FK a `user_tenants (tenant_id, user_id)` |
| Hash de firma | `signature_hash` en versión |
| Adendas con motivo | `reason NOT NULL` |

El contenido se guarda en `jsonb`; el backend debe validar el esquema del formulario antes de persistir y calcular el hash de la versión firmada.

### Documentos

- El contenido vive en almacenamiento privado; la base solo guarda `storage_key`.
- `document_versions` conserva versiones; `document_signatures` es inmutable.
- Los permisos granulares usan `document_permissions` con expiración.
- Las URLs de descarga deben ser firmadas y de vida corta; nunca públicas.

### Auditoría

| Tabla | Contenido | Mutabilidad |
| --- | --- | --- |
| `audit_logs` | Altas, cambios y bajas de entidades sensibles, con campos modificados | Append-only |
| `access_logs` | Vistas, descargas, impresiones, exportes y comparticiones | Append-only |
| `authentication_events` | Login, logout, MFA, fallos | Append-only por privilegios |
| `transmission_logs` | Envíos a FHIR/IHCE/RDA y respuesta | Append-only por privilegios |
| `security_incidents` | Incidentes y su gestión | Editable con auditoría |

El trigger de auditoría registra `tenant_id`, `user_id`, `action`, `entity_type`, `entity_id` y la lista de campos cambiados. No registra el valor anterior ni el nuevo: si se requiere evidencia de valores, debe agregarse una tabla de auditoría detallada con acceso restringido.

**Limitación importante**: RLS y triggers no registran lecturas. El backend debe escribir `access_logs` en cada consulta, descarga o exportación de información clínica, incluyendo justificación cuando el acceso sea excepcional.

## 7. Protección de datos personales

Controles disponibles para la Ley 1581 de 2012 y el Decreto 1377 de 2013:

- `patient_consents`: consentimiento por tipo y versión, con otorgamiento y revocación.
- `personal_data_requests`: acceso, rectificación, supresión, oposición y portabilidad, con estado y resolución.
- `patient_merge_log`: fusiones trazables sin pérdida de historia.
- `patient_alerts`: alertas clínicas y administrativas.
- `access_logs`: evidencia de acceso a información clínica.
- `security_incidents`: gestión de incidentes.
- `audit_logs`: trazabilidad de cambios.

Recomendaciones operativas:

1. Cifrado en tránsito con TLS obligatorio y cifrado en reposo en Azure PostgreSQL.
2. Cifrado adicional a nivel de aplicación para `users.mfa_secret_encrypted` y cualquier dato especialmente sensible.
3. Minimización: no almacenar datos que no sean necesarios para la atención.
4. Retención conforme a la Resolución 839 de 2017 y a la política institucional; la historia clínica no se elimina por solicitud de supresión cuando existe deber legal de conservación.
5. Seudonimización en ambientes de desarrollo y pruebas.
6. Prohibición de exportar datos reales a estaciones de trabajo sin control.

## 8. Pruebas de seguridad

### `tests/001_rls_test.sql`

Verifica con `SET ROLE sigm_app`:

- Un tenant solo ve su propia fila en `tenants` y sus propios pacientes.
- No ve pacientes ni historias de otro tenant.
- No puede insertar filas de otro tenant.
- Un `UPDATE` cross-tenant afecta 0 filas y no modifica el dato.
- No ve auditoría de otro tenant.
- No puede insertar en catálogos globales.
- No puede asignar permisos de plataforma a un rol de tenant.
- No puede asignarse `PLATFORM_ADMIN`.

### `tests/002_smoke_test.sql`

Verifica:

- Exclusión GiST de citas solapadas por profesional y consultorio.
- Inmutabilidad de versiones firmadas.
- Recálculo automático de totales de factura.
- Saldo y estado correctos en `v_accounts_receivable`.

### `tests/003_catalog_test.sql`

Verifica volúmenes, integridad y códigos de control de CIE-10, CUPS e IUM.

### `tests/004_rls_coverage_test.sql`

Verifica la cobertura estructural del aislamiento:

- Las 85 tablas con `tenant_id` tienen RLS habilitado.
- 84 tienen `FORCE ROW LEVEL SECURITY`; `audit_logs` es la única excepción.
- Todas tienen al menos una política que referencia `tenant_id`.
- Las FK entre tablas de tenant son compuestas `(tenant_id, id)`.
- Las cuatro excepciones (roles, formularios, categorías documentales) tienen su trigger de validación de tenant.

Las cuatro pruebas terminan en `ROLLBACK`.

## 9. Lista de verificación para producción

- [ ] El backend se conecta como `sigm_app`, nunca como propietario ni `sigm_platform`.
- [ ] `app.tenant_id` y `app.user_id` se establecen con `SET LOCAL` en cada transacción.
- [ ] La membresía y el estado del tenant se validan antes de abrir el contexto.
- [ ] SSL/TLS obligatorio y certificados válidos.
- [ ] Contraseñas y secretos en Key Vault, no en el repositorio.
- [ ] Copias de seguridad cifradas y restauración probada.
- [ ] `log_statement` y auditoría de conexiones configurados sin registrar datos clínicos.
- [ ] Monitoreo de intentos de acceso denegados y de cambios en roles.
- [ ] Revisión periódica de `role_permissions` y de usuarios con roles administrativos.
- [ ] Pruebas RLS y funcionales ejecutadas en cada despliegue.
- [ ] Retención y borrado seguro documentados.
- [ ] Procedimiento de respuesta a incidentes y notificación a la SIC.

## 10. Límites conocidos

- RLS no protege contra un superusuario, el propietario de las tablas ni `sigm_platform`.
- La autorización funcional (quién puede firmar, facturar o exportar) se implementa en la aplicación con `permissions` y `user_roles`; RLS solo delimita el tenant.
- Las políticas no filtran por sede, especialidad ni relación clínica. Si se requiere ese nivel, debe agregarse autorización en la capa de aplicación o políticas adicionales.
- `audit_logs` no almacena valores anteriores y nuevos; para auditoría de valores se requiere una tabla especializada.
- Los objetos de almacenamiento externo requieren políticas de IAM independientes.
- La recarga de catálogos globales debe hacerse con `sigm_platform` y queda fuera del tráfico normal.
