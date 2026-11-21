-- ============================================================================
-- SIGM CLOUD - 004 - Identidad, roles, permisos, membresias y sesiones
-- ============================================================================
BEGIN;

-- ----------------------------------------------------------------------------
-- Identidad global. Un usuario puede pertenecer a varios tenants.
-- El acceso a esta tabla debe pasar por el servicio de identidad.
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.users (
    id                     uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    email                  citext NOT NULL UNIQUE,
    password_hash          text,
    full_name              varchar(200) NOT NULL,
    phone                  varchar(40),
    is_active              boolean NOT NULL DEFAULT true,
    email_verified_at      timestamptz,
    mfa_enabled            boolean NOT NULL DEFAULT false,
    mfa_secret_encrypted   text,
    failed_login_attempts  integer NOT NULL DEFAULT 0,
    locked_until           timestamptz,
    last_login_at          timestamptz,
    created_at             timestamptz NOT NULL DEFAULT now(),
    updated_at             timestamptz NOT NULL DEFAULT now()
);

COMMENT ON TABLE sigm.users IS
  'Identidad global del usuario. Los datos clinicos y de negocio siempre se relacionan con un tenant.';

-- ----------------------------------------------------------------------------
-- Roles: globales (tenant_id NULL) o personalizados de un tenant
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.roles (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id   uuid REFERENCES sigm.tenants(id) ON DELETE CASCADE,
    code        varchar(60) NOT NULL,
    name        varchar(120) NOT NULL,
    description text,
    is_system   boolean NOT NULL DEFAULT false,
    created_at  timestamptz NOT NULL DEFAULT now(),
    updated_at  timestamptz NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_roles_global_code
    ON sigm.roles (code)
    WHERE tenant_id IS NULL;

CREATE UNIQUE INDEX IF NOT EXISTS ux_roles_tenant_code
    ON sigm.roles (tenant_id, code)
    WHERE tenant_id IS NOT NULL;

-- ----------------------------------------------------------------------------
-- Catalogo global de permisos
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.permissions (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code        varchar(100) NOT NULL UNIQUE,
    module      varchar(60) NOT NULL,
    name        varchar(150) NOT NULL,
    description text,
    created_at  timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS sigm.role_permissions (
    role_id       uuid NOT NULL REFERENCES sigm.roles(id) ON DELETE CASCADE,
    permission_id uuid NOT NULL REFERENCES sigm.permissions(id) ON DELETE CASCADE,
    PRIMARY KEY (role_id, permission_id)
);

-- ----------------------------------------------------------------------------
-- Membresia del usuario dentro de un tenant
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.user_tenants (
    tenant_id      uuid NOT NULL DEFAULT sigm.current_tenant_id()
                   REFERENCES sigm.tenants(id) ON DELETE CASCADE,
    user_id        uuid NOT NULL REFERENCES sigm.users(id) ON DELETE CASCADE,
    status         varchar(20) NOT NULL DEFAULT 'ACTIVE'
                   CHECK (status IN ('INVITED', 'ACTIVE', 'SUSPENDED', 'REVOKED')),
    default_site_id uuid,
    invited_by     uuid REFERENCES sigm.users(id) ON DELETE SET NULL,
    joined_at      timestamptz,
    created_at     timestamptz NOT NULL DEFAULT now(),
    updated_at     timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (tenant_id, user_id),
    FOREIGN KEY (tenant_id, default_site_id)
        REFERENCES sigm.sites (tenant_id, id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS ix_user_tenants_user
    ON sigm.user_tenants (user_id, status);

-- ----------------------------------------------------------------------------
-- Roles asignados por tenant
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.user_roles (
    tenant_id   uuid NOT NULL DEFAULT sigm.current_tenant_id(),
    user_id     uuid NOT NULL,
    role_id     uuid NOT NULL REFERENCES sigm.roles(id) ON DELETE CASCADE,
    granted_by  uuid REFERENCES sigm.users(id) ON DELETE SET NULL,
    granted_at  timestamptz NOT NULL DEFAULT now(),
    expires_at  timestamptz,
    PRIMARY KEY (tenant_id, user_id, role_id),
    FOREIGN KEY (tenant_id, user_id)
        REFERENCES sigm.user_tenants (tenant_id, user_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_user_roles_role
    ON sigm.user_roles (role_id);

-- ----------------------------------------------------------------------------
-- Sesiones de usuario
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.user_sessions (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           uuid NOT NULL DEFAULT sigm.current_tenant_id(),
    user_id             uuid NOT NULL,
    refresh_token_hash  text NOT NULL UNIQUE,
    ip_address          inet,
    user_agent          text,
    created_at          timestamptz NOT NULL DEFAULT now(),
    last_seen_at        timestamptz,
    expires_at          timestamptz NOT NULL,
    revoked_at          timestamptz,
    revoked_reason      varchar(120),
    UNIQUE (tenant_id, id),
    FOREIGN KEY (tenant_id, user_id)
        REFERENCES sigm.user_tenants (tenant_id, user_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_user_sessions_user
    ON sigm.user_sessions (tenant_id, user_id, expires_at DESC);

-- ----------------------------------------------------------------------------
-- Eventos de autenticacion
-- tenant_id NULL = evento de plataforma previo a la seleccion de tenant.
-- RLS solo expone los eventos del tenant actual.
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.authentication_events (
    id           uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id    uuid REFERENCES sigm.tenants(id) ON DELETE CASCADE,
    user_id      uuid REFERENCES sigm.users(id) ON DELETE SET NULL,
    event_type   varchar(40) NOT NULL,
    success      boolean NOT NULL,
    ip_address   inet,
    user_agent   text,
    detail       jsonb NOT NULL DEFAULT '{}'::jsonb,
    occurred_at  timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_authentication_events_tenant_date
    ON sigm.authentication_events (tenant_id, occurred_at DESC);

CREATE INDEX IF NOT EXISTS ix_authentication_events_user_date
    ON sigm.authentication_events (user_id, occurred_at DESC);

COMMIT;
