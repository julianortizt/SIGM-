# SIGM Cloud — Pendientes

Actualizado: 2026-11-15, tras la implementación de endurecimiento y cumplimiento. Complementa a `docs/status.md`.

## 0. Cerrado y verificado

- **Datos demo del portal:** paciente `eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee2` con 1 atención, 2 citas
  (una `ATTENDED`, una `CONFIRMED`), 1 documento público, 1 fórmula con ítem y 1 orden de laboratorio con
  resultado. Los endpoints `/portal/me/*` responden con datos.
- **MFA:** enrolamiento, desactivación y **códigos de recuperación de un solo uso** (10, hash en BD,
  regenerables con contraseña). Login acepta TOTP o código de recuperación.
- **Bloqueo del segundo factor:** `users.failed_mfa_attempts` y `mfa_locked_until`; a los 5 códigos fallidos
  el MFA se bloquea 15 minutos aunque la contraseña sea correcta (probado: 5.º intento bloquea, siguiente
  login con código válido responde 423).
- **Revocación de sesiones:** `users.token_version` + claim `tv`; cambiar/restablecer contraseña o desactivar
  MFA invalida los JWT (probado: token viejo → 401).
- **Recuperación y cambio de contraseña:** tokens hasheados de un solo uso (30 min) y cambio con verificación
  de la actual; formularios en admin, clínica y paciente. Se envía **aviso por correo** al cambiar,
  restablecer o desactivar MFA (si hay SMTP configurado).
- **Auditoría:** accesos del portal del paciente (`READ`/`UPDATE`), consentimientos y exportaciones de
  historia clínica. `AuditService` corregido para usar `metadata` e IP `inet`.
- **Consentimientos informados en el portal:** catálogo versionado (tratamiento de datos, uso de historia
  clínica e interoperabilidad), aceptación/revocación con evidencia de fecha, versión, IP y agente de usuario.
  Probado: aceptar crea la fila y queda auditado.
- **Respaldos:** ruta fuera de `bin`, retención configurable, verificación SHA-256 + `pg_restore --list` al
  crear y desde el panel, y **respaldo automático programado** (`Database:AutoBackupEnabled`,
  `AutoBackupIntervalHours`). Probado: respaldo automático a las 24 h de la última ejecución y respaldo
  previo a un script con 1 108 objetos verificados.
- **Consola SQL:** **doble aprobación** de scripts destructivos. La aprobación exige correo y contraseña de
  otro `PLATFORM_ADMIN`, distinto del autor y de quien aplica; sin aprobación el `apply` responde 400
  `requiresApproval`. Probado el flujo completo (rechazo del autor, aprobación por un segundo administrador,
  aplicación con respaldo verificado).
- **Configuración segura:** `appsettings.json` ya no contiene clave JWT ni credenciales; en Production la API
  rechaza cadenas de conexión locales o `postgres/postgres` y claves JWT ausentes/débiles. Se añadió
  `backend/.env.example` con todas las variables.
- **Operación:** `/health/ready` comprueba la base de datos (503 si no responde); HSTS y redirección HTTPS en
  Production; CORS por entorno.
- **Pruebas:** 43 pruebas en verde (`dotnet test`), typecheck de admin, clínica y paciente correcto.
- **Migraciones:** `021_auth_hardening.sql` y `022_mfa_lockout_script_approval.sql` aplicadas y registradas
  en `database/run_all.sql`. Restauración manual documentada en `database/docs/deployment.md`.

## 1. Consola de base de datos (pendiente)

- Restauración desde el panel: se mantiene fuera por riesgo; si se retoma, debe crear una base nueva y exigir
  ventana + confirmación doble, nunca reemplazar la base en uso.
- `skipBackup` sigue permitido en scripts no destructivos; evaluar exigir respaldo siempre.
- No se puede asociar un “script de reversión” a una aplicación.
- La aprobación no caduca por tiempo ni se invalida al cambiar el contenido (los scripts aplicados ya no se
  pueden editar, pero conviene revisar el flujo para futuras versiones).

## 2. Autenticación y sesiones (pendiente)

- Sin refresh token: el JWT dura 8 h y se revoca por `token_version`, pero no se renueva sin volver a
  autenticarse.
- Sin verificación de correo al crear cuentas ni bloqueo de la cuenta completa por intentos de contraseña
  (solo rate limit por IP y bloqueo del segundo factor).
- Sin notificación al titular cuando se crea una cuenta o se cambia el correo.

## 3. Producto (pendiente)

- **Portal del paciente:** faltan solicitud/reprogramación de citas, notificaciones y descarga de su propia
  historia clínica (si el negocio lo aprueba). Consentimientos, cambio de contraseña y MFA ya están.
- **Recordatorios:** `Reminders:DeliveryEnabled=false` y SMTP sin configurar en local; faltan plantillas por
  tipo, reintentos con backoff y tratamiento de rebotes.
- **Duplicación de portales:** `clinic-portal` y `practice-portal` siguen siendo copias casi idénticas;
  unificarlas en un shell compartido es un refactor pendiente.
- **Búsqueda de pacientes:** falta “cargar más” explícito en todos los listados paginados.
- **Flujos de dominio:** agenda con slots efectivos, formularios por especialidad, firma legal de notas,
  almacenamiento S3/versionado/firmas, estados completos de laboratorio, facturación y EPS.

## 4. Seguridad y operación (bloqueantes para producción)

- **TLS:** la API ya aplica HSTS/redirección en Production, pero faltan certificados y terminación TLS del
  proxy/ingress en el entorno real.
- **Respaldos fuera del host:** hoy quedan en el servidor de aplicación; falta copia cifrada a almacenamiento
  externo, política RPO/RTO y pruebas periódicas de restauración/PITR.
- **Observabilidad:** sin OpenTelemetry ni métricas/alertas de errores 5xx, latencia y colas; `/health/ready`
  es solo una comprobación básica.
- **CI/CD e IaC:** revisar que `.github/workflows/ci.yml` cubra los endpoints y portales nuevos; endurecer
  `infrastructure/azure`.
- **Secretos:** ya no están en el repositorio, pero falta integrarlos con un gestor (Key Vault, Vault, etc.) y
  definir rotación.

## 5. Calidad y pruebas

- Faltan pruebas de `PasswordService`/TOTP, bloqueo MFA, consentimientos y rate limiting.
- Sin pruebas de integración API+BD+RLS ni E2E (Playwright) de login con MFA, exportación de historia
  clínica, consola SQL y portal.
- Sin pruebas de concurrencia/carga ni de seguridad (OWASP básico).

## 6. Depende de terceros o del negocio

- Facturación electrónica DIAN (proveedor autorizado y resolución de numeración).
- Adaptadores EPS/autorizaciones/RIPS y transmisión operativa.
- HL7 FHIR / IHCE / RDA con endpoint real y homologación.
- Proveedor SMTP/WhatsApp y remitente verificado.
- Revisión normativa y jurídica (Ley 1581, Res. 1995, historia clínica electrónica).
- Definiciones comerciales: planes, límites por tenant, precios y retención de datos.

## 7. Orden sugerido

1. Certificados TLS, respaldos externos cifrados y observabilidad antes de un despliegue real.
2. Refresh tokens/rotación y verificación de correo.
3. Unificar `clinic-portal`/`practice-portal`.
4. E2E de los flujos críticos y pruebas de integración con RLS.
5. Integraciones de negocio (DIAN, EPS, FHIR/RDA) según prioridad comercial.
