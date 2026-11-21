-- ============================================================================
-- SIGM CLOUD - 021 - Endurecimiento de autenticación
-- ============================================================================
-- - token_version: permite revocar todos los JWT de un usuario al desactivar
--   MFA, cambiar o restablecer la contraseña.
-- - mfa_recovery_codes: códigos de un solo uso (hash SHA-256) para entrar si se
--   pierde el dispositivo autenticador.
-- - password_reset_tokens: tokens de restablecimiento de un solo uso, con
--   caducidad y hash en reposo (nunca se guarda el token en claro).
-- ============================================================================
BEGIN;

ALTER TABLE sigm.users
    ADD COLUMN IF NOT EXISTS token_version integer NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS mfa_recovery_codes jsonb;

CREATE TABLE IF NOT EXISTS sigm.password_reset_tokens (
    id           uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id      uuid NOT NULL REFERENCES sigm.users(id) ON DELETE CASCADE,
    token_hash   varchar(64) NOT NULL UNIQUE,
    expires_at   timestamptz NOT NULL,
    used_at      timestamptz,
    requested_ip inet,
    created_at   timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_password_reset_tokens_user
    ON sigm.password_reset_tokens (user_id, created_at DESC);

CREATE INDEX IF NOT EXISTS ix_password_reset_tokens_active
    ON sigm.password_reset_tokens (expires_at)
    WHERE used_at IS NULL;

COMMENT ON COLUMN sigm.users.token_version IS
    'Versión del token JWT; incrementarla invalida todas las sesiones emitidas.';
COMMENT ON COLUMN sigm.users.mfa_recovery_codes IS
    'Arreglo JSON de hashes SHA-256 de códigos de recuperación MFA (un solo uso).';

COMMIT;
