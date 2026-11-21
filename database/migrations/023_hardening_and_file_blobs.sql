-- ============================================================================
-- SIGM CLOUD - 023 - Endurecimiento de permisos, integridad y almacenamiento
-- ============================================================================
-- 1. user_preferences queda bajo RLS (el test 004_rls_coverage_test.sql lo exigía).
-- 2. Se revoca DML a sigm_app sobre tablas exclusivas de plataforma: los
--    privilege por defecto de 016 concedían INSERT/UPDATE/DELETE a todas las
--    tablas nuevas, incluidas platform_settings, db_scripts y backups.
-- 3. Índice para las adendas de historia clínica (JOIN por tenant+nota).
-- 4. sigm.file_blobs: almacenamiento durable de documentos en Vercel, donde el
--    filesystem de la función es efímero y LocalFileStorageService perdía archivos.
-- Idempotente: se puede reaplicar sin error.
-- ============================================================================
BEGIN;

-- ----------------------------------------------------------------------------
-- 1. Aislamiento de preferencias de usuario
-- ----------------------------------------------------------------------------
ALTER TABLE sigm.user_preferences ENABLE ROW LEVEL SECURITY;
ALTER TABLE sigm.user_preferences FORCE  ROW LEVEL SECURITY;

DROP POLICY IF EXISTS user_preferences_tenant_isolation ON sigm.user_preferences;
CREATE POLICY user_preferences_tenant_isolation ON sigm.user_preferences
    FOR ALL TO sigm_app
    USING      (tenant_id = sigm.current_tenant_id())
    WITH CHECK (tenant_id = sigm.current_tenant_id());

GRANT SELECT, INSERT, UPDATE, DELETE ON sigm.user_preferences TO sigm_app;

-- ----------------------------------------------------------------------------
-- 2. Tablas de plataforma: sigm_app solo puede leer lo que necesita
-- ----------------------------------------------------------------------------
REVOKE INSERT, UPDATE, DELETE ON sigm.platform_settings   FROM sigm_app;
REVOKE INSERT, UPDATE, DELETE ON sigm.db_scripts          FROM sigm_app;
REVOKE INSERT, UPDATE, DELETE ON sigm.db_script_runs      FROM sigm_app;
REVOKE INSERT, UPDATE, DELETE ON sigm.database_backups    FROM sigm_app;
REVOKE INSERT, UPDATE, DELETE ON sigm.password_reset_tokens FROM sigm_app;

-- ----------------------------------------------------------------------------
-- 3. Rendimiento del snapshot clínico
-- ----------------------------------------------------------------------------
CREATE INDEX IF NOT EXISTS ix_clinical_amendments_note
    ON sigm.clinical_amendments (tenant_id, note_id, amendment_number);

-- ----------------------------------------------------------------------------
-- 4. Blobs de documentos (almacenamiento durable)
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.file_blobs (
    storage_key  text PRIMARY KEY,
    tenant_id    uuid NOT NULL,
    file_name    text NOT NULL,
    content_type text NOT NULL,
    content      bytea NOT NULL,
    created_at   timestamptz NOT NULL DEFAULT now()
);

COMMENT ON TABLE sigm.file_blobs IS
  'Contenido de documentos subidos por los portales. Persiste en Neon aunque la función de Vercel se reinicie.';

CREATE INDEX IF NOT EXISTS ix_file_blobs_tenant
    ON sigm.file_blobs (tenant_id, created_at DESC);

ALTER TABLE sigm.file_blobs ENABLE ROW LEVEL SECURITY;
ALTER TABLE sigm.file_blobs FORCE  ROW LEVEL SECURITY;

DROP POLICY IF EXISTS file_blobs_tenant_isolation ON sigm.file_blobs;
CREATE POLICY file_blobs_tenant_isolation ON sigm.file_blobs
    FOR ALL TO sigm_app
    USING      (tenant_id = sigm.current_tenant_id())
    WITH CHECK (tenant_id = sigm.current_tenant_id());

GRANT SELECT, INSERT, UPDATE, DELETE ON sigm.file_blobs TO sigm_app;
GRANT ALL ON sigm.file_blobs TO sigm_platform;

COMMIT;
