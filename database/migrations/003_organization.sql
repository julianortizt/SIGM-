-- ============================================================================
-- SIGM CLOUD - 003 - Organizaciones, sedes, departamentos y recursos
-- ============================================================================
BEGIN;

-- ----------------------------------------------------------------------------
-- Organizaciones legales dentro de un tenant
-- Un tenant puede agrupar una o varias organizaciones (ej. grupo empresarial).
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.organizations (
    id                uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id         uuid NOT NULL DEFAULT sigm.current_tenant_id()
                      REFERENCES sigm.tenants(id) ON DELETE CASCADE,
    legal_name        varchar(200) NOT NULL,
    commercial_name   varchar(200) NOT NULL,
    tax_id            varchar(30),
    organization_type varchar(20) NOT NULL DEFAULT 'CLINIC'
                      CHECK (organization_type IN ('CLINIC', 'PRACTICE', 'LABORATORY', 'PHARMACY', 'GROUP', 'OTHER')),
    is_primary        boolean NOT NULL DEFAULT false,
    status            varchar(20) NOT NULL DEFAULT 'ACTIVE'
                      CHECK (status IN ('ACTIVE', 'INACTIVE', 'SUSPENDED')),
    address_line      text,
    city              varchar(120),
    department        varchar(120),
    country           varchar(80) NOT NULL DEFAULT 'Colombia',
    phone             varchar(40),
    email             citext,
    created_at        timestamptz NOT NULL DEFAULT now(),
    updated_at        timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id)
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_organizations_primary
    ON sigm.organizations (tenant_id)
    WHERE is_primary;

CREATE UNIQUE INDEX IF NOT EXISTS ux_organizations_tax_id
    ON sigm.organizations (tenant_id, tax_id)
    WHERE tax_id IS NOT NULL;

-- ----------------------------------------------------------------------------
-- Sedes
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.sites (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       uuid NOT NULL DEFAULT sigm.current_tenant_id()
                    REFERENCES sigm.tenants(id) ON DELETE CASCADE,
    organization_id uuid NOT NULL,
    code            varchar(40) NOT NULL,
    name            varchar(150) NOT NULL,
    address_line    text,
    city            varchar(120),
    department      varchar(120),
    country         varchar(80) NOT NULL DEFAULT 'Colombia',
    phone           varchar(40),
    email           citext,
    timezone        varchar(60) NOT NULL DEFAULT 'America/Bogota',
    is_active       boolean NOT NULL DEFAULT true,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    UNIQUE (tenant_id, organization_id, code),
    FOREIGN KEY (tenant_id, organization_id)
        REFERENCES sigm.organizations (tenant_id, id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_sites_organization
    ON sigm.sites (tenant_id, organization_id);

-- ----------------------------------------------------------------------------
-- Departamentos / areas internas
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.departments (
    id           uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id    uuid NOT NULL DEFAULT sigm.current_tenant_id()
                 REFERENCES sigm.tenants(id) ON DELETE CASCADE,
    site_id      uuid,
    code         varchar(40) NOT NULL,
    name         varchar(150) NOT NULL,
    description  text,
    is_active    boolean NOT NULL DEFAULT true,
    created_at   timestamptz NOT NULL DEFAULT now(),
    updated_at   timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    UNIQUE (tenant_id, code),
    FOREIGN KEY (tenant_id, site_id)
        REFERENCES sigm.sites (tenant_id, id) ON DELETE CASCADE
);

-- ----------------------------------------------------------------------------
-- Consultorios fisicos
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.medical_offices (
    id         uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id  uuid NOT NULL DEFAULT sigm.current_tenant_id()
               REFERENCES sigm.tenants(id) ON DELETE CASCADE,
    site_id    uuid NOT NULL,
    code       varchar(40) NOT NULL,
    name       varchar(120) NOT NULL,
    floor      varchar(40),
    capacity   integer CHECK (capacity IS NULL OR capacity > 0),
    is_active  boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    UNIQUE (tenant_id, site_id, code),
    FOREIGN KEY (tenant_id, site_id)
        REFERENCES sigm.sites (tenant_id, id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_medical_offices_site
    ON sigm.medical_offices (tenant_id, site_id);

-- ----------------------------------------------------------------------------
-- Recursos fisicos o equipos reservables
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.resources (
    id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id     uuid NOT NULL DEFAULT sigm.current_tenant_id()
                  REFERENCES sigm.tenants(id) ON DELETE CASCADE,
    site_id       uuid,
    code          varchar(40) NOT NULL,
    name          varchar(150) NOT NULL,
    resource_type varchar(30) NOT NULL DEFAULT 'EQUIPMENT'
                  CHECK (resource_type IN ('ROOM', 'EQUIPMENT', 'DEVICE', 'VEHICLE', 'OTHER')),
    description   text,
    is_active     boolean NOT NULL DEFAULT true,
    created_at    timestamptz NOT NULL DEFAULT now(),
    updated_at    timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    UNIQUE (tenant_id, code),
    FOREIGN KEY (tenant_id, site_id)
        REFERENCES sigm.sites (tenant_id, id) ON DELETE SET NULL
);

COMMIT;
