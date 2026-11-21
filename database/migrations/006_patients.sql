-- ============================================================================
-- SIGM CLOUD - 006 - Pacientes, identificadores, contactos y consentimientos
-- ============================================================================
BEGIN;

-- ----------------------------------------------------------------------------
-- Catalogo de tipos de documento
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.document_types (
    code      varchar(10) PRIMARY KEY,
    name      varchar(80) NOT NULL,
    is_active boolean NOT NULL DEFAULT true
);

-- ----------------------------------------------------------------------------
-- Pacientes
-- ----------------------------------------------------------------------------
CREATE SEQUENCE IF NOT EXISTS sigm.patient_number_seq;

CREATE TABLE IF NOT EXISTS sigm.patients (
    id                uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id         uuid NOT NULL DEFAULT sigm.current_tenant_id()
                      REFERENCES sigm.tenants(id) ON DELETE CASCADE,
    patient_number    varchar(50) NOT NULL DEFAULT ('P' || lpad(nextval('sigm.patient_number_seq')::text, 8, '0')),
    document_type     varchar(10) REFERENCES sigm.document_types(code) ON DELETE SET NULL,
    document_number   varchar(40),
    first_name        varchar(100) NOT NULL,
    middle_name       varchar(100),
    last_name         varchar(100) NOT NULL,
    second_last_name  varchar(100),
    full_name         text GENERATED ALWAYS AS (
                          btrim(
                              coalesce(first_name, '') || ' ' ||
                              coalesce(middle_name, '') || ' ' ||
                              coalesce(last_name, '') || ' ' ||
                              coalesce(second_last_name, '')
                          )
                      ) STORED,
    birth_date        date,
    sex_at_birth      varchar(20) NOT NULL DEFAULT 'UNKNOWN'
                      CHECK (sex_at_birth IN ('M', 'F', 'INTERSEX', 'UNKNOWN')),
    gender_identity   varchar(60),
    marital_status    varchar(30),
    occupation        varchar(120),
    blood_type        varchar(10)
                      CHECK (blood_type IS NULL OR blood_type IN ('A+', 'A-', 'B+', 'B-', 'AB+', 'AB-', 'O+', 'O-', 'UNKNOWN')),
    email             citext,
    phone             varchar(40),
    mobile_phone      varchar(40),
    address_line      text,
    city              varchar(120),
    department        varchar(120),
    country           varchar(80) NOT NULL DEFAULT 'Colombia',
    notes             text,
    is_active         boolean NOT NULL DEFAULT true,
    deceased_at       timestamptz,
    created_by        uuid,
    created_at        timestamptz NOT NULL DEFAULT now(),
    updated_at        timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    UNIQUE (tenant_id, patient_number),
    FOREIGN KEY (tenant_id, created_by)
        REFERENCES sigm.user_tenants (tenant_id, user_id) ON DELETE SET NULL,
    CHECK (birth_date IS NULL OR birth_date <= current_date)
);

COMMENT ON TABLE sigm.patients IS
  'Paciente dentro de un tenant. La identidad de un paciente no se comparte automaticamente entre organizaciones.';

CREATE UNIQUE INDEX IF NOT EXISTS ux_patients_document
    ON sigm.patients (tenant_id, document_type, document_number)
    WHERE document_number IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_patients_full_name_trgm
    ON sigm.patients USING gin (full_name gin_trgm_ops);

CREATE INDEX IF NOT EXISTS ix_patients_tenant_active
    ON sigm.patients (tenant_id, is_active);

CREATE INDEX IF NOT EXISTS ix_patients_birth_date
    ON sigm.patients (tenant_id, birth_date);

-- ----------------------------------------------------------------------------
-- Identificadores adicionales (pasaporte, afiliacion, etc.)
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.patient_identifiers (
    id                uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id         uuid NOT NULL DEFAULT sigm.current_tenant_id(),
    patient_id        uuid NOT NULL,
    identifier_type   varchar(40) NOT NULL,
    identifier_value  varchar(80) NOT NULL,
    issuing_authority varchar(120),
    is_primary        boolean NOT NULL DEFAULT false,
    valid_from        date,
    valid_to          date,
    created_at        timestamptz NOT NULL DEFAULT now(),
    updated_at        timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    UNIQUE (tenant_id, patient_id, identifier_type, identifier_value),
    FOREIGN KEY (tenant_id, patient_id)
        REFERENCES sigm.patients (tenant_id, id) ON DELETE CASCADE,
    CHECK (valid_to IS NULL OR valid_from IS NULL OR valid_to >= valid_from)
);

CREATE INDEX IF NOT EXISTS ix_patient_identifiers_value
    ON sigm.patient_identifiers (tenant_id, identifier_type, identifier_value);

-- ----------------------------------------------------------------------------
-- Contactos del paciente
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.patient_contacts (
    id           uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id    uuid NOT NULL DEFAULT sigm.current_tenant_id(),
    patient_id   uuid NOT NULL,
    contact_type varchar(30) NOT NULL DEFAULT 'FAMILY'
                 CHECK (contact_type IN ('EMERGENCY', 'FAMILY', 'CAREGIVER', 'LEGAL_GUARDIAN', 'OTHER')),
    name         varchar(200) NOT NULL,
    relationship varchar(80),
    phone        varchar(40),
    email        citext,
    is_primary   boolean NOT NULL DEFAULT false,
    is_emergency boolean NOT NULL DEFAULT false,
    notes        text,
    created_at   timestamptz NOT NULL DEFAULT now(),
    updated_at   timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    FOREIGN KEY (tenant_id, patient_id)
        REFERENCES sigm.patients (tenant_id, id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_patient_contacts_patient
    ON sigm.patient_contacts (tenant_id, patient_id);

-- ----------------------------------------------------------------------------
-- Direcciones
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.patient_addresses (
    id           uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id    uuid NOT NULL DEFAULT sigm.current_tenant_id(),
    patient_id   uuid NOT NULL,
    address_type varchar(30) NOT NULL DEFAULT 'HOME'
                 CHECK (address_type IN ('HOME', 'WORK', 'MAILING', 'OTHER')),
    line1        varchar(200) NOT NULL,
    line2        varchar(200),
    city         varchar(120),
    department   varchar(120),
    country      varchar(80) NOT NULL DEFAULT 'Colombia',
    postal_code  varchar(20),
    is_primary   boolean NOT NULL DEFAULT false,
    created_at   timestamptz NOT NULL DEFAULT now(),
    updated_at   timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    FOREIGN KEY (tenant_id, patient_id)
        REFERENCES sigm.patients (tenant_id, id) ON DELETE CASCADE
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_patient_addresses_primary
    ON sigm.patient_addresses (tenant_id, patient_id)
    WHERE is_primary;

-- ----------------------------------------------------------------------------
-- Familiares y responsables
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.patient_relatives (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           uuid NOT NULL DEFAULT sigm.current_tenant_id(),
    patient_id          uuid NOT NULL,
    relative_patient_id uuid,
    name                varchar(200) NOT NULL,
    relationship        varchar(80),
    document_type       varchar(10) REFERENCES sigm.document_types(code) ON DELETE SET NULL,
    document_number     varchar(40),
    phone               varchar(40),
    is_guardian         boolean NOT NULL DEFAULT false,
    created_at          timestamptz NOT NULL DEFAULT now(),
    updated_at          timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    FOREIGN KEY (tenant_id, patient_id)
        REFERENCES sigm.patients (tenant_id, id) ON DELETE CASCADE,
    FOREIGN KEY (tenant_id, relative_patient_id)
        REFERENCES sigm.patients (tenant_id, id) ON DELETE SET NULL,
    CHECK (relative_patient_id IS NULL OR relative_patient_id <> patient_id)
);

-- ----------------------------------------------------------------------------
-- Alertas clinicas y administrativas
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.patient_alerts (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id   uuid NOT NULL DEFAULT sigm.current_tenant_id(),
    patient_id  uuid NOT NULL,
    alert_type  varchar(30) NOT NULL DEFAULT 'MEDICAL'
                CHECK (alert_type IN ('ALLERGY', 'MEDICAL', 'ADMINISTRATIVE', 'BILLING', 'OTHER')),
    severity    varchar(20) NOT NULL DEFAULT 'MEDIUM'
                CHECK (severity IN ('LOW', 'MEDIUM', 'HIGH', 'CRITICAL')),
    description text NOT NULL,
    is_active   boolean NOT NULL DEFAULT true,
    created_by  uuid,
    created_at  timestamptz NOT NULL DEFAULT now(),
    updated_at  timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    FOREIGN KEY (tenant_id, patient_id)
        REFERENCES sigm.patients (tenant_id, id) ON DELETE CASCADE,
    FOREIGN KEY (tenant_id, created_by)
        REFERENCES sigm.user_tenants (tenant_id, user_id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS ix_patient_alerts_active
    ON sigm.patient_alerts (tenant_id, patient_id, is_active);

-- ----------------------------------------------------------------------------
-- Consentimientos informados
-- document_id se relaciona con sigm.documents en la migracion 010.
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.patient_consents (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       uuid NOT NULL DEFAULT sigm.current_tenant_id(),
    patient_id      uuid NOT NULL,
    consent_type    varchar(60) NOT NULL,
    consent_version varchar(20) NOT NULL DEFAULT '1',
    granted         boolean NOT NULL,
    granted_at      timestamptz,
    revoked_at      timestamptz,
    document_id     uuid,
    recorded_by     uuid,
    notes           text,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    UNIQUE (tenant_id, patient_id, consent_type, consent_version),
    FOREIGN KEY (tenant_id, patient_id)
        REFERENCES sigm.patients (tenant_id, id) ON DELETE CASCADE,
    FOREIGN KEY (tenant_id, recorded_by)
        REFERENCES sigm.user_tenants (tenant_id, user_id) ON DELETE SET NULL,
    CHECK (NOT granted OR granted_at IS NOT NULL),
    CHECK (revoked_at IS NULL OR granted_at IS NULL OR revoked_at >= granted_at)
);

-- ----------------------------------------------------------------------------
-- Solicitudes sobre datos personales (Ley 1581 de 2012)
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.personal_data_requests (
    id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id     uuid NOT NULL DEFAULT sigm.current_tenant_id(),
    patient_id    uuid NOT NULL,
    request_type  varchar(30) NOT NULL
                  CHECK (request_type IN ('ACCESS', 'RECTIFICATION', 'DELETION', 'OPPOSITION', 'PORTABILITY', 'OTHER')),
    description   text NOT NULL,
    status        varchar(20) NOT NULL DEFAULT 'RECEIVED'
                  CHECK (status IN ('RECEIVED', 'IN_REVIEW', 'RESOLVED', 'REJECTED', 'CANCELLED')),
    requested_at  timestamptz NOT NULL DEFAULT now(),
    resolved_at   timestamptz,
    resolution    text,
    handled_by    uuid,
    created_at    timestamptz NOT NULL DEFAULT now(),
    updated_at    timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    FOREIGN KEY (tenant_id, patient_id)
        REFERENCES sigm.patients (tenant_id, id) ON DELETE CASCADE,
    FOREIGN KEY (tenant_id, handled_by)
        REFERENCES sigm.user_tenants (tenant_id, user_id) ON DELETE SET NULL
);

-- ----------------------------------------------------------------------------
-- Trazabilidad de fusiones de pacientes duplicados
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.patient_merge_log (
    id                uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id         uuid NOT NULL DEFAULT sigm.current_tenant_id(),
    source_patient_id uuid NOT NULL,
    target_patient_id uuid NOT NULL,
    merged_by         uuid,
    merged_at         timestamptz NOT NULL DEFAULT now(),
    reason            text,
    metadata          jsonb NOT NULL DEFAULT '{}'::jsonb,
    UNIQUE (tenant_id, id),
    FOREIGN KEY (tenant_id, source_patient_id)
        REFERENCES sigm.patients (tenant_id, id) ON DELETE RESTRICT,
    FOREIGN KEY (tenant_id, target_patient_id)
        REFERENCES sigm.patients (tenant_id, id) ON DELETE RESTRICT,
    FOREIGN KEY (tenant_id, merged_by)
        REFERENCES sigm.user_tenants (tenant_id, user_id) ON DELETE SET NULL,
    CHECK (source_patient_id <> target_patient_id)
);

COMMIT;
