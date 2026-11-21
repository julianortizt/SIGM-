-- ============================================================================
-- SIGM CLOUD - 007 - Catalogos clinicos globales
-- ============================================================================
BEGIN;

-- ----------------------------------------------------------------------------
-- CIE-10
-- El catalogo oficial completo debe importarse desde la fuente autorizada.
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.diagnosis_catalog (
    id           uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code         varchar(20) NOT NULL UNIQUE,
    description  text NOT NULL,
    chapter      varchar(150),
    chapter_code varchar(10),
    group_code   varchar(20),
    is_active    boolean NOT NULL DEFAULT true,
    created_at   timestamptz NOT NULL DEFAULT now(),
    updated_at   timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_diagnosis_catalog_description_trgm
    ON sigm.diagnosis_catalog USING gin (description gin_trgm_ops);

-- ----------------------------------------------------------------------------
-- CUPS / catalogo de procedimientos
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.procedure_catalog (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code        varchar(30) NOT NULL UNIQUE,
    -- El nombre oficial de algunos procedimientos CUPS supera los 250
    -- caracteres; se usa text para no truncar el catalogo de SISPRO.
    name        text NOT NULL,
    chapter     varchar(150),
    is_active   boolean NOT NULL DEFAULT true,
    created_at  timestamptz NOT NULL DEFAULT now(),
    updated_at  timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_procedure_catalog_name_trgm
    ON sigm.procedure_catalog USING gin (name gin_trgm_ops);

-- ----------------------------------------------------------------------------
-- Medicamentos
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.medication_catalog (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code                varchar(30) NOT NULL UNIQUE,
    name                varchar(250) NOT NULL,
    presentation        varchar(150),
    concentration       varchar(80),
    pharmaceutical_form varchar(80),
    route               varchar(80),
    is_active           boolean NOT NULL DEFAULT true,
    created_at          timestamptz NOT NULL DEFAULT now(),
    updated_at          timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_medication_catalog_name_trgm
    ON sigm.medication_catalog USING gin (name gin_trgm_ops);

-- ----------------------------------------------------------------------------
-- Examenes de laboratorio
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.laboratory_tests (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code            varchar(30) NOT NULL UNIQUE,
    name            varchar(250) NOT NULL,
    sample_type     varchar(80),
    unit            varchar(40),
    reference_range varchar(150),
    is_active       boolean NOT NULL DEFAULT true,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now()
);

-- ----------------------------------------------------------------------------
-- Categorias documentales (globales o propias de un tenant)
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.document_categories (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id   uuid REFERENCES sigm.tenants(id) ON DELETE CASCADE,
    code        varchar(40) NOT NULL,
    name        varchar(120) NOT NULL,
    description text,
    is_system   boolean NOT NULL DEFAULT false,
    is_active   boolean NOT NULL DEFAULT true,
    created_at  timestamptz NOT NULL DEFAULT now(),
    updated_at  timestamptz NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_document_categories_global
    ON sigm.document_categories (code)
    WHERE tenant_id IS NULL;

CREATE UNIQUE INDEX IF NOT EXISTS ux_document_categories_tenant
    ON sigm.document_categories (tenant_id, code)
    WHERE tenant_id IS NOT NULL;

-- ----------------------------------------------------------------------------
-- Tipos de nota clinica
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.clinical_note_types (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code        varchar(40) NOT NULL UNIQUE,
    name        varchar(120) NOT NULL,
    description text,
    is_active   boolean NOT NULL DEFAULT true,
    created_at  timestamptz NOT NULL DEFAULT now()
);

-- ----------------------------------------------------------------------------
-- EPS, ARL, aseguradoras y planes complementarios
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.insurers (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code        varchar(30) NOT NULL UNIQUE,
    name        varchar(200) NOT NULL,
    nit         varchar(30),
    insurer_type varchar(30) NOT NULL DEFAULT 'EPS'
                CHECK (insurer_type IN ('EPS', 'ARL', 'PREPAID', 'SUBSIDIZED', 'COMPLEMENTARY', 'OTHER')),
    is_active   boolean NOT NULL DEFAULT true,
    created_at  timestamptz NOT NULL DEFAULT now(),
    updated_at  timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_insurers_name_trgm
    ON sigm.insurers USING gin (name gin_trgm_ops);

COMMIT;
