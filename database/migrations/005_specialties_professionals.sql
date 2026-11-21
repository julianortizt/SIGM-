-- ============================================================================
-- SIGM CLOUD - 005 - Especialidades, profesionales y formularios clinicos
-- ============================================================================
BEGIN;

-- ----------------------------------------------------------------------------
-- Catalogo global de especialidades
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.specialties (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code        varchar(40) NOT NULL UNIQUE,
    name        varchar(150) NOT NULL,
    description text,
    is_active   boolean NOT NULL DEFAULT true,
    created_at  timestamptz NOT NULL DEFAULT now(),
    updated_at  timestamptz NOT NULL DEFAULT now()
);

COMMENT ON TABLE sigm.specialties IS
  'Catalogo maestro de especialidades. Cada tenant decide cuales habilita.';

-- ----------------------------------------------------------------------------
-- Especialidades habilitadas por tenant
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.tenant_specialties (
    tenant_id    uuid NOT NULL DEFAULT sigm.current_tenant_id()
                 REFERENCES sigm.tenants(id) ON DELETE CASCADE,
    specialty_id uuid NOT NULL REFERENCES sigm.specialties(id) ON DELETE RESTRICT,
    custom_name  varchar(150),
    is_active    boolean NOT NULL DEFAULT true,
    created_at   timestamptz NOT NULL DEFAULT now(),
    updated_at   timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (tenant_id, specialty_id)
);

-- ----------------------------------------------------------------------------
-- Profesionales de la salud
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.professionals (
    id                 uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id          uuid NOT NULL DEFAULT sigm.current_tenant_id()
                       REFERENCES sigm.tenants(id) ON DELETE CASCADE,
    user_id            uuid NOT NULL,
    organization_id    uuid,
    primary_site_id    uuid,
    professional_type  varchar(40) NOT NULL DEFAULT 'PHYSICIAN'
                       CHECK (professional_type IN (
                           'PHYSICIAN', 'SPECIALIST', 'NURSE', 'NURSING_ASSISTANT',
                           'PSYCHOLOGIST', 'NUTRITIONIST', 'PHYSIOTHERAPIST',
                           'ODONTOLOGIST', 'LABORATORY_STAFF', 'OTHER'
                       )),
    professional_license varchar(80),
    display_name       varchar(200) NOT NULL,
    email              citext,
    phone              varchar(40),
    is_active          boolean NOT NULL DEFAULT true,
    created_at         timestamptz NOT NULL DEFAULT now(),
    updated_at         timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    FOREIGN KEY (tenant_id, user_id)
        REFERENCES sigm.user_tenants (tenant_id, user_id) ON DELETE CASCADE,
    FOREIGN KEY (tenant_id, organization_id)
        REFERENCES sigm.organizations (tenant_id, id) ON DELETE SET NULL,
    FOREIGN KEY (tenant_id, primary_site_id)
        REFERENCES sigm.sites (tenant_id, id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS ix_professionals_user
    ON sigm.professionals (tenant_id, user_id);

CREATE INDEX IF NOT EXISTS ix_professionals_active
    ON sigm.professionals (tenant_id, is_active);

-- ----------------------------------------------------------------------------
-- Asignacion de un profesional a varias sedes
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.professional_sites (
    tenant_id       uuid NOT NULL DEFAULT sigm.current_tenant_id(),
    professional_id uuid NOT NULL,
    site_id         uuid NOT NULL,
    is_primary      boolean NOT NULL DEFAULT false,
    valid_from      date,
    valid_to        date,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (tenant_id, professional_id, site_id),
    FOREIGN KEY (tenant_id, professional_id)
        REFERENCES sigm.professionals (tenant_id, id) ON DELETE CASCADE,
    FOREIGN KEY (tenant_id, site_id)
        REFERENCES sigm.sites (tenant_id, id) ON DELETE CASCADE,
    CHECK (valid_to IS NULL OR valid_from IS NULL OR valid_to >= valid_from)
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_professional_sites_primary
    ON sigm.professional_sites (tenant_id, professional_id)
    WHERE is_primary;

-- ----------------------------------------------------------------------------
-- Relacion muchos-a-muchos profesional <-> especialidad
-- La especialidad debe estar habilitada en el tenant y el profesional debe
-- tenerla acreditada para poder generar encuentros y citas.
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.professional_specialties (
    tenant_id           uuid NOT NULL DEFAULT sigm.current_tenant_id(),
    professional_id     uuid NOT NULL,
    specialty_id        uuid NOT NULL,
    site_id             uuid,
    professional_license varchar(80),
    verification_status varchar(20) NOT NULL DEFAULT 'PENDING'
                        CHECK (verification_status IN ('PENDING', 'VERIFIED', 'REJECTED', 'EXPIRED')),
    verified_at         timestamptz,
    verified_by         uuid REFERENCES sigm.users(id) ON DELETE SET NULL,
    valid_from          date,
    valid_to            date,
    is_active           boolean NOT NULL DEFAULT true,
    created_at          timestamptz NOT NULL DEFAULT now(),
    updated_at          timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (tenant_id, professional_id, specialty_id),
    FOREIGN KEY (tenant_id, professional_id)
        REFERENCES sigm.professionals (tenant_id, id) ON DELETE CASCADE,
    FOREIGN KEY (tenant_id, specialty_id)
        REFERENCES sigm.tenant_specialties (tenant_id, specialty_id) ON DELETE RESTRICT,
    FOREIGN KEY (tenant_id, site_id)
        REFERENCES sigm.sites (tenant_id, id) ON DELETE SET NULL,
    CHECK (valid_to IS NULL OR valid_from IS NULL OR valid_to >= valid_from)
);

CREATE INDEX IF NOT EXISTS ix_professional_specialties_specialty
    ON sigm.professional_specialties (tenant_id, specialty_id, is_active);

-- ----------------------------------------------------------------------------
-- Formularios configurables por especialidad.
-- tenant_id NULL = plantilla global del producto.
-- tenant_id NOT NULL = personalizacion del cliente.
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.specialty_forms (
    id           uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id    uuid REFERENCES sigm.tenants(id) ON DELETE CASCADE,
    specialty_id uuid NOT NULL REFERENCES sigm.specialties(id) ON DELETE CASCADE,
    name         varchar(150) NOT NULL,
    version      integer NOT NULL DEFAULT 1 CHECK (version > 0),
    description  text,
    schema       jsonb NOT NULL DEFAULT '{}'::jsonb,
    is_active    boolean NOT NULL DEFAULT true,
    created_at   timestamptz NOT NULL DEFAULT now(),
    updated_at   timestamptz NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_specialty_forms_global
    ON sigm.specialty_forms (specialty_id, name, version)
    WHERE tenant_id IS NULL;

CREATE UNIQUE INDEX IF NOT EXISTS ux_specialty_forms_tenant
    ON sigm.specialty_forms (tenant_id, specialty_id, name, version)
    WHERE tenant_id IS NOT NULL;

CREATE TABLE IF NOT EXISTS sigm.specialty_form_fields (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id   uuid REFERENCES sigm.tenants(id) ON DELETE CASCADE,
    form_id     uuid NOT NULL REFERENCES sigm.specialty_forms(id) ON DELETE CASCADE,
    field_key   varchar(80) NOT NULL,
    label       varchar(200) NOT NULL,
    field_type  varchar(40) NOT NULL DEFAULT 'TEXT'
                CHECK (field_type IN (
                    'TEXT', 'TEXTAREA', 'NUMBER', 'DATE', 'DATETIME', 'BOOLEAN',
                    'SELECT', 'MULTISELECT', 'RADIO', 'CHECKBOX', 'SIGNATURE',
                    'FILE', 'TABLE', 'CALCULATED'
                )),
    required    boolean NOT NULL DEFAULT false,
    options     jsonb NOT NULL DEFAULT '[]'::jsonb,
    config      jsonb NOT NULL DEFAULT '{}'::jsonb,
    sort_order  integer NOT NULL DEFAULT 0,
    created_at  timestamptz NOT NULL DEFAULT now(),
    updated_at  timestamptz NOT NULL DEFAULT now(),
    UNIQUE (form_id, field_key)
);

COMMIT;
