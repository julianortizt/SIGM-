-- ============================================================================
-- SIGM CLOUD - 002 - Tenants, planes, suscripciones y configuracion
-- ============================================================================
BEGIN;

-- ----------------------------------------------------------------------------
-- Tenants: frontera de aislamiento de toda la plataforma
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.tenants (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_code     varchar(40)  NOT NULL UNIQUE,
    legal_name      varchar(200) NOT NULL,
    commercial_name varchar(200) NOT NULL,
    tax_id          varchar(30),
    product_type    varchar(20)  NOT NULL CHECK (product_type IN ('CLINIC', 'PRACTICE')),
    status          varchar(20)  NOT NULL DEFAULT 'ACTIVE'
                    CHECK (status IN ('TRIAL', 'ACTIVE', 'SUSPENDED', 'CANCELLED')),
    timezone        varchar(60)  NOT NULL DEFAULT 'America/Bogota',
    locale          varchar(10)  NOT NULL DEFAULT 'es-CO',
    currency        char(3)      NOT NULL DEFAULT 'COP',
    created_at      timestamptz  NOT NULL DEFAULT now(),
    updated_at      timestamptz  NOT NULL DEFAULT now()
);

COMMENT ON TABLE sigm.tenants IS
  'Cuenta comercial SaaS. Es la frontera de aislamiento logico de todos los datos clinicos y administrativos.';
COMMENT ON COLUMN sigm.tenants.product_type IS
  'CLINIC = SIGM Clinicas; PRACTICE = SIGM Consultorios.';

CREATE UNIQUE INDEX IF NOT EXISTS ux_tenants_tax_id
    ON sigm.tenants (tax_id)
    WHERE tax_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_tenants_status
    ON sigm.tenants (status);

-- ----------------------------------------------------------------------------
-- Planes comerciales (catalogo global)
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.plans (
    id                 uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code               varchar(40)  NOT NULL UNIQUE,
    name               varchar(120) NOT NULL,
    product_type       varchar(20)  NOT NULL DEFAULT 'BOTH'
                       CHECK (product_type IN ('CLINIC', 'PRACTICE', 'BOTH')),
    description        text,
    price              numeric(14,2) NOT NULL DEFAULT 0 CHECK (price >= 0),
    currency           char(3)      NOT NULL DEFAULT 'COP',
    billing_period     varchar(20)  NOT NULL DEFAULT 'MONTHLY'
                       CHECK (billing_period IN ('MONTHLY', 'YEARLY', 'CUSTOM')),
    max_users          integer      CHECK (max_users IS NULL OR max_users > 0),
    max_sites          integer      CHECK (max_sites IS NULL OR max_sites > 0),
    storage_limit_mb   integer      CHECK (storage_limit_mb IS NULL OR storage_limit_mb > 0),
    features           jsonb        NOT NULL DEFAULT '{}'::jsonb,
    is_active          boolean      NOT NULL DEFAULT true,
    created_at         timestamptz  NOT NULL DEFAULT now(),
    updated_at         timestamptz  NOT NULL DEFAULT now()
);

COMMENT ON TABLE sigm.plans IS
  'Catalogo global de planes comerciales. Los precios son parametricos y no representan una decision comercial definitiva.';

-- ----------------------------------------------------------------------------
-- Suscripciones por tenant
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.tenant_subscriptions (
    id                   uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id            uuid NOT NULL DEFAULT sigm.current_tenant_id()
                         REFERENCES sigm.tenants(id) ON DELETE CASCADE,
    plan_id              uuid NOT NULL REFERENCES sigm.plans(id),
    status               varchar(20) NOT NULL DEFAULT 'TRIAL'
                         CHECK (status IN ('TRIAL', 'ACTIVE', 'PAST_DUE', 'SUSPENDED', 'CANCELLED', 'EXPIRED')),
    started_at           timestamptz NOT NULL DEFAULT now(),
    current_period_start timestamptz,
    current_period_end   timestamptz,
    trial_ends_at        timestamptz,
    cancelled_at         timestamptz,
    external_reference   varchar(120),
    created_at           timestamptz NOT NULL DEFAULT now(),
    updated_at           timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    CHECK (current_period_end IS NULL OR current_period_start IS NULL OR current_period_end > current_period_start)
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_tenant_subscriptions_current
    ON sigm.tenant_subscriptions (tenant_id)
    WHERE status IN ('TRIAL', 'ACTIVE', 'PAST_DUE');

CREATE INDEX IF NOT EXISTS ix_tenant_subscriptions_plan
    ON sigm.tenant_subscriptions (plan_id, status);

-- ----------------------------------------------------------------------------
-- Features habilitadas por tenant
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.tenant_features (
    tenant_id     uuid NOT NULL DEFAULT sigm.current_tenant_id()
                  REFERENCES sigm.tenants(id) ON DELETE CASCADE,
    feature_code  varchar(80) NOT NULL,
    enabled       boolean NOT NULL DEFAULT true,
    limit_value   integer,
    config        jsonb NOT NULL DEFAULT '{}'::jsonb,
    updated_at    timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (tenant_id, feature_code)
);

COMMENT ON TABLE sigm.tenant_features IS
  'Permite habilitar modulos y limites por cliente sin duplicar el codigo base.';

-- ----------------------------------------------------------------------------
-- Configuracion clave/valor por tenant
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.tenant_settings (
    tenant_id    uuid NOT NULL DEFAULT sigm.current_tenant_id()
                 REFERENCES sigm.tenants(id) ON DELETE CASCADE,
    setting_key  varchar(120) NOT NULL,
    value        jsonb NOT NULL,
    updated_by   uuid,
    updated_at   timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (tenant_id, setting_key)
);

-- ----------------------------------------------------------------------------
-- Dominios propios de cada tenant
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.tenant_domains (
    id           uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id    uuid NOT NULL DEFAULT sigm.current_tenant_id()
                 REFERENCES sigm.tenants(id) ON DELETE CASCADE,
    domain       citext NOT NULL UNIQUE,
    is_primary   boolean NOT NULL DEFAULT false,
    verified_at  timestamptz,
    created_at   timestamptz NOT NULL DEFAULT now(),
    updated_at   timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id)
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_tenant_domains_primary
    ON sigm.tenant_domains (tenant_id)
    WHERE is_primary;

-- ----------------------------------------------------------------------------
-- Identidad visual por tenant
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.tenant_branding (
    tenant_id           uuid PRIMARY KEY DEFAULT sigm.current_tenant_id()
                        REFERENCES sigm.tenants(id) ON DELETE CASCADE,
    logo_key            text,
    favicon_key         text,
    primary_color       varchar(20),
    secondary_color     varchar(20),
    accent_color        varchar(20),
    email_from_name     varchar(120),
    email_from_address  citext,
    custom_css          text,
    updated_at          timestamptz NOT NULL DEFAULT now()
);

-- ----------------------------------------------------------------------------
-- Pagos de suscripcion
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.subscription_payments (
    id               uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id        uuid NOT NULL DEFAULT sigm.current_tenant_id()
                     REFERENCES sigm.tenants(id) ON DELETE CASCADE,
    subscription_id  uuid NOT NULL,
    amount           numeric(14,2) NOT NULL CHECK (amount >= 0),
    currency         char(3) NOT NULL DEFAULT 'COP',
    period_start     date,
    period_end       date,
    status           varchar(20) NOT NULL DEFAULT 'PENDING'
                     CHECK (status IN ('PENDING', 'PAID', 'FAILED', 'REFUNDED', 'VOID')),
    paid_at          timestamptz,
    reference        varchar(120),
    created_at       timestamptz NOT NULL DEFAULT now(),
    updated_at       timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    FOREIGN KEY (tenant_id, subscription_id)
        REFERENCES sigm.tenant_subscriptions (tenant_id, id) ON DELETE CASCADE,
    CHECK (period_end IS NULL OR period_start IS NULL OR period_end >= period_start)
);

CREATE INDEX IF NOT EXISTS ix_subscription_payments_tenant_date
    ON sigm.subscription_payments (tenant_id, created_at DESC);

COMMIT;
