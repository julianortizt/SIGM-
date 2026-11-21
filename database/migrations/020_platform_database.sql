-- ============================================================================
-- SIGM CLOUD - 020 - Consola de base de datos de plataforma
-- ============================================================================
-- Permite al superadministrador de plataforma cargar scripts SQL, validarlos
-- en seco (dentro de una transaccion que siempre se revierte) y aplicarlos de
-- forma controlada: respaldo previo, bloqueo asesor, transaccion unica y
-- registro inmutable de cada ejecucion.
--
-- No hay SQL libre sin control: todo script queda versionado con hash, se
-- rechazan sentencias catastroficas y las destructivas exigen confirmacion.
-- ============================================================================
BEGIN;

-- ----------------------------------------------------------------------------
-- 1. Permiso de plataforma
-- ----------------------------------------------------------------------------
INSERT INTO sigm.permissions (code, module, name, description) VALUES
('platform.database.manage', 'platform', 'Administrar base de datos',
 'Cargar, validar y aplicar scripts SQL controlados sobre la base de datos.')
ON CONFLICT (code) DO UPDATE
SET module = EXCLUDED.module,
    name = EXCLUDED.name,
    description = EXCLUDED.description;

INSERT INTO sigm.role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM sigm.roles r
JOIN sigm.permissions p ON p.code = 'platform.database.manage'
WHERE r.code = 'PLATFORM_ADMIN' AND r.tenant_id IS NULL
ON CONFLICT DO NOTHING;

-- ----------------------------------------------------------------------------
-- 2. Scripts cargados
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.db_scripts (
    id           uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    name         varchar(160) NOT NULL,
    description  text,
    sql_text     text NOT NULL,
    checksum     varchar(64) NOT NULL,
    status       varchar(20) NOT NULL DEFAULT 'PENDING'
                 CHECK (status IN ('PENDING', 'VALIDATED', 'APPLIED', 'FAILED')),
    destructive  boolean NOT NULL DEFAULT false,
    statements   integer NOT NULL DEFAULT 0,
    created_by   uuid,
    created_at   timestamptz NOT NULL DEFAULT now(),
    validated_at timestamptz,
    applied_at   timestamptz,
    last_error   text,
    UNIQUE (checksum)
);

CREATE INDEX IF NOT EXISTS ix_db_scripts_created ON sigm.db_scripts (created_at DESC);

COMMENT ON TABLE sigm.db_scripts IS
  'Scripts SQL cargados desde el panel de plataforma. El checksum evita duplicados y da trazabilidad del contenido aplicado.';

-- ----------------------------------------------------------------------------
-- 3. Historial de validaciones y aplicaciones
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.db_script_runs (
    id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    script_id     uuid NOT NULL REFERENCES sigm.db_scripts(id) ON DELETE CASCADE,
    mode          varchar(12) NOT NULL CHECK (mode IN ('VALIDATE', 'APPLY')),
    success       boolean NOT NULL,
    message       text,
    statements    integer NOT NULL DEFAULT 0,
    duration_ms   integer NOT NULL DEFAULT 0,
    executed_by   uuid,
    client_ip     inet,
    executed_at   timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_db_script_runs_script ON sigm.db_script_runs (script_id, executed_at DESC);

COMMENT ON TABLE sigm.db_script_runs IS
  'Bitacora de cada validacion (se revierte) y aplicacion real de un script SQL.';

-- ----------------------------------------------------------------------------
-- 4. Respaldos
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.database_backups (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    file_name   varchar(240) NOT NULL,
    size_bytes  bigint NOT NULL,
    checksum    varchar(64),
    note        text,
    created_by  uuid,
    created_at  timestamptz NOT NULL DEFAULT now()
);

COMMENT ON TABLE sigm.database_backups IS
  'Respaldos logicos (pg_dump -Fc) generados antes de aplicar scripts. El archivo vive en el almacenamiento del servidor.';

-- ----------------------------------------------------------------------------
-- 5. Privilegios (si existen los roles de aplicacion)
-- ----------------------------------------------------------------------------
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'sigm_platform') THEN
        GRANT USAGE ON SCHEMA sigm TO sigm_platform;
        GRANT SELECT, INSERT, UPDATE, DELETE ON sigm.db_scripts TO sigm_platform;
        GRANT SELECT, INSERT ON sigm.db_script_runs TO sigm_platform;
        GRANT SELECT, INSERT, DELETE ON sigm.database_backups TO sigm_platform;
    END IF;
END $$;

COMMIT;
