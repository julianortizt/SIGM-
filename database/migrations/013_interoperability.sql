-- ============================================================================
-- SIGM CLOUD - 013 - Interoperabilidad: FHIR, IHCE y RDA
-- ============================================================================
BEGIN;

CREATE TABLE IF NOT EXISTS sigm.interoperability_messages (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           uuid NOT NULL DEFAULT sigm.current_tenant_id()
                        REFERENCES sigm.tenants(id) ON DELETE CASCADE,
    message_type        varchar(60) NOT NULL,
    direction           varchar(10) NOT NULL CHECK (direction IN ('OUTBOUND', 'INBOUND')),
    fhir_resource_type  varchar(60),
    resource_id         uuid,
    external_id         varchar(120),
    payload             jsonb NOT NULL DEFAULT '{}'::jsonb,
    status              varchar(20) NOT NULL DEFAULT 'PENDING'
                        CHECK (status IN ('PENDING', 'SENT', 'RECEIVED', 'PROCESSED', 'FAILED', 'CANCELLED')),
    sent_at             timestamptz,
    response_payload    jsonb NOT NULL DEFAULT '{}'::jsonb,
    error_message       text,
    created_at          timestamptz NOT NULL DEFAULT now(),
    updated_at          timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id)
);

CREATE INDEX IF NOT EXISTS ix_interoperability_messages_status
    ON sigm.interoperability_messages (tenant_id, status, created_at DESC);

CREATE TABLE IF NOT EXISTS sigm.interoperability_events (
    id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id     uuid NOT NULL DEFAULT sigm.current_tenant_id(),
    event_type    varchar(80) NOT NULL,
    entity_type   varchar(80) NOT NULL,
    entity_id     uuid,
    status        varchar(20) NOT NULL DEFAULT 'PENDING'
                  CHECK (status IN ('PENDING', 'SUCCESS', 'FAILED', 'SKIPPED')),
    detail        jsonb NOT NULL DEFAULT '{}'::jsonb,
    occurred_at   timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id)
);

-- tenant_id NULL = mapeo global; tenant_id NOT NULL = mapeo del cliente
CREATE TABLE IF NOT EXISTS sigm.fhir_mappings (
    id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id     uuid REFERENCES sigm.tenants(id) ON DELETE CASCADE,
    resource_type varchar(60) NOT NULL,
    local_table   varchar(80) NOT NULL,
    local_field   varchar(80),
    fhir_path     varchar(200) NOT NULL,
    mapping       jsonb NOT NULL DEFAULT '{}'::jsonb,
    is_active     boolean NOT NULL DEFAULT true,
    created_at    timestamptz NOT NULL DEFAULT now(),
    updated_at    timestamptz NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_fhir_mappings_global
    ON sigm.fhir_mappings (resource_type, local_table, local_field, fhir_path)
    WHERE tenant_id IS NULL;

CREATE UNIQUE INDEX IF NOT EXISTS ux_fhir_mappings_tenant
    ON sigm.fhir_mappings (tenant_id, resource_type, local_table, local_field, fhir_path)
    WHERE tenant_id IS NOT NULL;

CREATE TABLE IF NOT EXISTS sigm.transmission_logs (
    id               uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id        uuid NOT NULL DEFAULT sigm.current_tenant_id(),
    message_id       uuid,
    destination      varchar(250) NOT NULL,
    http_status      integer,
    request_payload  jsonb NOT NULL DEFAULT '{}'::jsonb,
    response_payload jsonb NOT NULL DEFAULT '{}'::jsonb,
    attempted_at     timestamptz NOT NULL DEFAULT now(),
    success          boolean NOT NULL,
    error_message    text,
    UNIQUE (tenant_id, id),
    FOREIGN KEY (tenant_id, message_id)
        REFERENCES sigm.interoperability_messages (tenant_id, id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS ix_transmission_logs_message
    ON sigm.transmission_logs (tenant_id, message_id, attempted_at DESC);

CREATE TABLE IF NOT EXISTS sigm.rda_documents (
    id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id      uuid NOT NULL DEFAULT sigm.current_tenant_id()
                   REFERENCES sigm.tenants(id) ON DELETE CASCADE,
    encounter_id   uuid NOT NULL,
    patient_id     uuid NOT NULL,
    rda_uuid       varchar(120) NOT NULL,
    status         varchar(20) NOT NULL DEFAULT 'DRAFT'
                   CHECK (status IN ('DRAFT', 'GENERATED', 'TRANSMITTED', 'ACCEPTED', 'REJECTED')),
    generated_at   timestamptz,
    transmitted_at timestamptz,
    payload        jsonb NOT NULL DEFAULT '{}'::jsonb,
    document_id    uuid,
    created_at     timestamptz NOT NULL DEFAULT now(),
    updated_at     timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    UNIQUE (tenant_id, rda_uuid),
    FOREIGN KEY (tenant_id, encounter_id, patient_id)
        REFERENCES sigm.encounters (tenant_id, id, patient_id) ON DELETE CASCADE,
    FOREIGN KEY (tenant_id, document_id)
        REFERENCES sigm.documents (tenant_id, id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS ix_rda_documents_patient
    ON sigm.rda_documents (tenant_id, patient_id, created_at DESC);

COMMIT;
