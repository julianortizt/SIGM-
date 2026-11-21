-- ============================================================================
-- SIGM CLOUD - 010 - Documentos, versiones, permisos y firmas
-- ============================================================================
BEGIN;

-- ----------------------------------------------------------------------------
-- Documentos clinicos y administrativos
-- El archivo fisico vive en almacenamiento privado compatible con S3.
-- La base de datos solo conserva metadatos y la llave de almacenamiento.
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.documents (
    id                   uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id            uuid NOT NULL DEFAULT sigm.current_tenant_id()
                         REFERENCES sigm.tenants(id) ON DELETE CASCADE,
    patient_id           uuid,
    encounter_id         uuid,
    category_id          uuid REFERENCES sigm.document_categories(id) ON DELETE SET NULL,
    title                varchar(250) NOT NULL,
    description          text,
    storage_key          text NOT NULL,
    mime_type            varchar(120) NOT NULL,
    size_bytes           bigint NOT NULL CHECK (size_bytes >= 0),
    checksum_sha256      varchar(64),
    version              integer NOT NULL DEFAULT 1 CHECK (version > 0),
    status               varchar(20) NOT NULL DEFAULT 'ACTIVE'
                         CHECK (status IN ('ACTIVE', 'ARCHIVED', 'DELETED')),
    is_public_to_patient boolean NOT NULL DEFAULT false,
    uploaded_by          uuid,
    created_at           timestamptz NOT NULL DEFAULT now(),
    updated_at           timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    FOREIGN KEY (tenant_id, patient_id)
        REFERENCES sigm.patients (tenant_id, id) ON DELETE CASCADE,
    FOREIGN KEY (tenant_id, encounter_id, patient_id)
        REFERENCES sigm.encounters (tenant_id, id, patient_id) ON DELETE SET NULL,
    FOREIGN KEY (tenant_id, uploaded_by)
        REFERENCES sigm.user_tenants (tenant_id, user_id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS ix_documents_patient
    ON sigm.documents (tenant_id, patient_id, created_at DESC);

CREATE INDEX IF NOT EXISTS ix_documents_encounter
    ON sigm.documents (tenant_id, encounter_id);

-- ----------------------------------------------------------------------------
-- Versiones del documento
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.document_versions (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       uuid NOT NULL DEFAULT sigm.current_tenant_id(),
    document_id     uuid NOT NULL,
    version_number  integer NOT NULL CHECK (version_number > 0),
    storage_key     text NOT NULL,
    mime_type       varchar(120) NOT NULL,
    size_bytes      bigint NOT NULL CHECK (size_bytes >= 0),
    checksum_sha256 varchar(64),
    uploaded_by     uuid,
    created_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    UNIQUE (tenant_id, document_id, version_number),
    FOREIGN KEY (tenant_id, document_id)
        REFERENCES sigm.documents (tenant_id, id) ON DELETE CASCADE,
    FOREIGN KEY (tenant_id, uploaded_by)
        REFERENCES sigm.user_tenants (tenant_id, user_id) ON DELETE SET NULL
);

-- ----------------------------------------------------------------------------
-- Permisos granulares por usuario o rol
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.document_permissions (
    id               uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id        uuid NOT NULL DEFAULT sigm.current_tenant_id(),
    document_id      uuid NOT NULL,
    user_id          uuid,
    role_id          uuid REFERENCES sigm.roles(id) ON DELETE CASCADE,
    permission_level varchar(20) NOT NULL DEFAULT 'READ'
                     CHECK (permission_level IN ('READ', 'WRITE', 'DELETE', 'MANAGE')),
    granted_by       uuid,
    granted_at       timestamptz NOT NULL DEFAULT now(),
    expires_at       timestamptz,
    UNIQUE (tenant_id, id),
    FOREIGN KEY (tenant_id, document_id)
        REFERENCES sigm.documents (tenant_id, id) ON DELETE CASCADE,
    FOREIGN KEY (tenant_id, user_id)
        REFERENCES sigm.user_tenants (tenant_id, user_id) ON DELETE CASCADE,
    FOREIGN KEY (tenant_id, granted_by)
        REFERENCES sigm.user_tenants (tenant_id, user_id) ON DELETE SET NULL,
    CHECK (user_id IS NOT NULL OR role_id IS NOT NULL)
);

CREATE INDEX IF NOT EXISTS ix_document_permissions_document
    ON sigm.document_permissions (tenant_id, document_id);

-- ----------------------------------------------------------------------------
-- Firmas de documentos
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.document_signatures (
    id                 uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id          uuid NOT NULL DEFAULT sigm.current_tenant_id(),
    document_id        uuid NOT NULL,
    signer_user_id     uuid NOT NULL,
    signature_type     varchar(20) NOT NULL DEFAULT 'ELECTRONIC'
                       CHECK (signature_type IN ('ELECTRONIC', 'DIGITAL', 'HANDWRITTEN')),
    signature_hash     varchar(128) NOT NULL,
    certificate_serial varchar(120),
    signed_at          timestamptz NOT NULL DEFAULT now(),
    metadata           jsonb NOT NULL DEFAULT '{}'::jsonb,
    UNIQUE (tenant_id, id),
    FOREIGN KEY (tenant_id, document_id)
        REFERENCES sigm.documents (tenant_id, id) ON DELETE CASCADE,
    FOREIGN KEY (tenant_id, signer_user_id)
        REFERENCES sigm.user_tenants (tenant_id, user_id) ON DELETE RESTRICT
);

-- ----------------------------------------------------------------------------
-- Relaciones con documentos creadas en migraciones anteriores
-- ----------------------------------------------------------------------------
ALTER TABLE sigm.patient_consents
    DROP CONSTRAINT IF EXISTS fk_patient_consents_document;
ALTER TABLE sigm.patient_consents
    ADD CONSTRAINT fk_patient_consents_document
    FOREIGN KEY (tenant_id, document_id)
    REFERENCES sigm.documents (tenant_id, id) ON DELETE SET NULL;

ALTER TABLE sigm.prescriptions
    DROP CONSTRAINT IF EXISTS fk_prescriptions_document;
ALTER TABLE sigm.prescriptions
    ADD CONSTRAINT fk_prescriptions_document
    FOREIGN KEY (tenant_id, document_id)
    REFERENCES sigm.documents (tenant_id, id) ON DELETE SET NULL;

COMMIT;
