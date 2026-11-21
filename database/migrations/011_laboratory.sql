-- ============================================================================
-- SIGM CLOUD - 011 - Laboratorio y reportes diagnosticos
-- ============================================================================
BEGIN;

CREATE SEQUENCE IF NOT EXISTS sigm.lab_order_number_seq;

CREATE TABLE IF NOT EXISTS sigm.laboratory_orders (
    id           uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id    uuid NOT NULL DEFAULT sigm.current_tenant_id()
                 REFERENCES sigm.tenants(id) ON DELETE CASCADE,
    encounter_id uuid NOT NULL,
    patient_id   uuid NOT NULL,
    order_number varchar(50) NOT NULL DEFAULT ('L' || lpad(nextval('sigm.lab_order_number_seq')::text, 10, '0')),
    ordered_by   uuid,
    status       varchar(20) NOT NULL DEFAULT 'REQUESTED'
                 CHECK (status IN ('REQUESTED', 'COLLECTED', 'IN_PROCESS', 'RESULTED', 'VALIDATED', 'CANCELLED')),
    priority     varchar(20) NOT NULL DEFAULT 'ROUTINE'
                 CHECK (priority IN ('ROUTINE', 'URGENT', 'STAT')),
    ordered_at   timestamptz NOT NULL DEFAULT now(),
    notes        text,
    created_at   timestamptz NOT NULL DEFAULT now(),
    updated_at   timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    UNIQUE (tenant_id, order_number),
    FOREIGN KEY (tenant_id, encounter_id, patient_id)
        REFERENCES sigm.encounters (tenant_id, id, patient_id) ON DELETE CASCADE,
    FOREIGN KEY (tenant_id, ordered_by)
        REFERENCES sigm.user_tenants (tenant_id, user_id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS ix_laboratory_orders_patient
    ON sigm.laboratory_orders (tenant_id, patient_id, ordered_at DESC);

CREATE TABLE IF NOT EXISTS sigm.laboratory_order_items (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           uuid NOT NULL DEFAULT sigm.current_tenant_id(),
    laboratory_order_id uuid NOT NULL,
    test_id             uuid NOT NULL REFERENCES sigm.laboratory_tests(id) ON DELETE RESTRICT,
    status              varchar(20) NOT NULL DEFAULT 'REQUESTED'
                        CHECK (status IN ('REQUESTED', 'COLLECTED', 'IN_PROCESS', 'RESULTED', 'VALIDATED', 'CANCELLED')),
    notes               text,
    created_at          timestamptz NOT NULL DEFAULT now(),
    updated_at          timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    FOREIGN KEY (tenant_id, laboratory_order_id)
        REFERENCES sigm.laboratory_orders (tenant_id, id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS sigm.laboratory_results (
    id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id     uuid NOT NULL DEFAULT sigm.current_tenant_id(),
    order_item_id uuid NOT NULL,
    result_value  text NOT NULL,
    unit          varchar(40),
    reference_range varchar(150),
    result_flag   varchar(20) NOT NULL DEFAULT 'NORMAL'
                  CHECK (result_flag IN ('NORMAL', 'HIGH', 'LOW', 'CRITICAL', 'ABNORMAL')),
    resulted_at   timestamptz NOT NULL DEFAULT now(),
    validated_by  uuid,
    validated_at  timestamptz,
    created_at    timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    FOREIGN KEY (tenant_id, order_item_id)
        REFERENCES sigm.laboratory_order_items (tenant_id, id) ON DELETE CASCADE,
    FOREIGN KEY (tenant_id, validated_by)
        REFERENCES sigm.user_tenants (tenant_id, user_id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS ix_laboratory_results_item
    ON sigm.laboratory_results (tenant_id, order_item_id, resulted_at DESC);

CREATE TABLE IF NOT EXISTS sigm.diagnostic_reports (
    id           uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id    uuid NOT NULL DEFAULT sigm.current_tenant_id()
                 REFERENCES sigm.tenants(id) ON DELETE CASCADE,
    patient_id   uuid NOT NULL,
    encounter_id uuid,
    report_type  varchar(30) NOT NULL DEFAULT 'LABORATORY'
                 CHECK (report_type IN ('LABORATORY', 'IMAGING', 'PATHOLOGY', 'CARDIOLOGY', 'OTHER')),
    title        varchar(250) NOT NULL,
    content      text,
    document_id  uuid,
    status       varchar(20) NOT NULL DEFAULT 'DRAFT'
                 CHECK (status IN ('DRAFT', 'FINAL', 'DELIVERED', 'CANCELLED')),
    reported_by  uuid,
    reported_at  timestamptz,
    created_at   timestamptz NOT NULL DEFAULT now(),
    updated_at   timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    FOREIGN KEY (tenant_id, patient_id)
        REFERENCES sigm.patients (tenant_id, id) ON DELETE CASCADE,
    FOREIGN KEY (tenant_id, encounter_id, patient_id)
        REFERENCES sigm.encounters (tenant_id, id, patient_id) ON DELETE SET NULL,
    FOREIGN KEY (tenant_id, reported_by)
        REFERENCES sigm.user_tenants (tenant_id, user_id) ON DELETE SET NULL
);

ALTER TABLE sigm.diagnostic_reports
    DROP CONSTRAINT IF EXISTS fk_diagnostic_reports_document;
ALTER TABLE sigm.diagnostic_reports
    ADD CONSTRAINT fk_diagnostic_reports_document
    FOREIGN KEY (tenant_id, document_id)
    REFERENCES sigm.documents (tenant_id, id) ON DELETE SET NULL;

CREATE INDEX IF NOT EXISTS ix_diagnostic_reports_patient
    ON sigm.diagnostic_reports (tenant_id, patient_id, created_at DESC);

COMMIT;
