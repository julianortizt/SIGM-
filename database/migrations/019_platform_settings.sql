-- ============================================================================
-- SIGM CLOUD - 019 - Configuracion global de plataforma
-- ============================================================================
-- Tabla clave/valor para ajustes de toda la plataforma (no por tenant).
-- Uso inicial: contenido de la pagina publica de bienvenida, administrado
-- desde el panel de superadmin.
-- ============================================================================
BEGIN;

CREATE TABLE IF NOT EXISTS sigm.platform_settings (
    setting_key varchar(120) PRIMARY KEY,
    value       jsonb NOT NULL DEFAULT '{}'::jsonb,
    updated_by  uuid,
    updated_at  timestamptz NOT NULL DEFAULT now()
);

COMMENT ON TABLE sigm.platform_settings IS
  'Ajustes globales de la plataforma (no ligados a un tenant). Ej: setting_key ''welcome'' con {title,message}.';

-- Contenido por defecto de la pagina de bienvenida (idempotente).
INSERT INTO sigm.platform_settings(setting_key, value)
VALUES (
    'welcome',
    jsonb_build_object(
        'title', 'Bienvenido a SIGM Cloud',
        'message', 'Plataforma integral de gestion medica para clinicas, consultorios y pacientes.'
    )
)
ON CONFLICT (setting_key) DO NOTHING;

-- Privilegios: solo el rol de plataforma puede escribir; la app puede leer.
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'sigm_platform') THEN
        GRANT USAGE ON SCHEMA sigm TO sigm_platform;
        GRANT ALL PRIVILEGES ON sigm.platform_settings TO sigm_platform;
    END IF;
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'sigm_app') THEN
        GRANT USAGE ON SCHEMA sigm TO sigm_app;
        GRANT SELECT ON sigm.platform_settings TO sigm_app;
    END IF;
END $$;

COMMIT;
