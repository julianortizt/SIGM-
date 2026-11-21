-- ============================================================================
-- SIGM CLOUD - 018 - Superadministrador de plataforma y preferencias de usuario
-- ============================================================================
-- 1. Bandera is_platform_admin en usuarios: identidad de soporte SaaS que no
--    pertenece a ningun tenant y accede al panel oculto de plataforma.
-- 2. Tabla sigm.user_preferences: preferencias por usuario dentro de un tenant
--    (p. ej. dashboard personalizable de clinica/consultorio).
-- 3. Seed del superadmin de plataforma (dev/demo):
--    soporte@sigm.cloud / Soporte2026!
--    El hash es PBKDF2-SHA256 v1 (formato de SIGM.Api PasswordService).
-- ============================================================================
BEGIN;

-- ----------------------------------------------------------------------------
-- 1. Superadministrador de plataforma
-- ----------------------------------------------------------------------------
ALTER TABLE sigm.users ADD COLUMN IF NOT EXISTS is_platform_admin boolean NOT NULL DEFAULT false;

COMMENT ON COLUMN sigm.users.is_platform_admin IS
  'true = superadministrador de plataforma (soporte SaaS). No requiere membresia de tenant; gestiona tenants, modulos y auditoria global.';

-- ----------------------------------------------------------------------------
-- 2. Preferencias por usuario (dashboard personalizable)
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.user_preferences (
    tenant_id  uuid NOT NULL DEFAULT sigm.current_tenant_id(),
    user_id    uuid NOT NULL REFERENCES sigm.users(id) ON DELETE CASCADE,
    pref_key   varchar(80) NOT NULL,
    value      jsonb NOT NULL DEFAULT '{}'::jsonb,
    updated_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (tenant_id, user_id, pref_key)
);

COMMENT ON TABLE sigm.user_preferences IS
  'Preferencias personales por usuario y tenant. pref_key ''dashboard'' guarda la configuracion del panel de inicio (paneles visibles).';

-- ----------------------------------------------------------------------------
-- 3. Seed superadmin de plataforma (solo dev/demo; en prod crear via
--    aprovisionamiento con sigm_platform)
-- ----------------------------------------------------------------------------
INSERT INTO sigm.users(id, email, password_hash, full_name, is_active, is_platform_admin)
VALUES (
    gen_random_uuid(),
    'soporte@sigm.cloud',
    'v1.100000.vwdWfiFUuAGzPkbA5GmvCA==.nDne0dnMs7mfP1TIX/o82VbUZyjew4xVqrx0M117xeI=',
    'Soporte SIGM Cloud',
    true,
    true
)
ON CONFLICT (email) DO UPDATE
    SET is_platform_admin = true,
        is_active = true,
        password_hash = EXCLUDED.password_hash;

COMMIT;
