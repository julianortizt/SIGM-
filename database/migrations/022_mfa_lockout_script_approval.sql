-- ============================================================================
-- SIGM CLOUD - 022 - Bloqueo MFA y doble aprobación de scripts destructivos
-- ============================================================================
-- - failed_mfa_attempts / mfa_locked_until: bloquea el segundo factor tras
--   varios intentos fallidos, incluso si la contraseña es correcta.
-- - db_scripts.approved_by / approved_at / approval_note: un script destructivo
--   solo puede aplicarse después de que otro administrador de plataforma lo
--   apruebe con sus propias credenciales.
-- ============================================================================
BEGIN;

ALTER TABLE sigm.users
    ADD COLUMN IF NOT EXISTS failed_mfa_attempts integer NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS mfa_locked_until timestamptz;

COMMENT ON COLUMN sigm.users.failed_mfa_attempts IS
    'Intentos fallidos consecutivos de código MFA; se reinicia al acertar.';
COMMENT ON COLUMN sigm.users.mfa_locked_until IS
    'Si está en el futuro, el segundo factor queda bloqueado hasta esa hora.';

ALTER TABLE sigm.db_scripts
    ADD COLUMN IF NOT EXISTS approved_by uuid REFERENCES sigm.users(id) ON DELETE SET NULL,
    ADD COLUMN IF NOT EXISTS approved_at timestamptz,
    ADD COLUMN IF NOT EXISTS approval_note text;

COMMENT ON COLUMN sigm.db_scripts.approved_by IS
    'Administrador de plataforma distinto del solicitante que aprobó el script destructivo.';
COMMENT ON COLUMN sigm.db_scripts.approved_at IS
    'Fecha de la aprobación; se invalida si cambia el contenido del script.';

CREATE INDEX IF NOT EXISTS ix_users_mfa_locked_until
    ON sigm.users (mfa_locked_until) WHERE mfa_locked_until IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_db_scripts_approved_by
    ON sigm.db_scripts (approved_by) WHERE approved_by IS NOT NULL;

COMMIT;
