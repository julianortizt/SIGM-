-- ============================================================================
-- SIGM CLOUD - 012 - Facturacion electronica, EPS, RIPS y cartera
-- ============================================================================
BEGIN;

-- ----------------------------------------------------------------------------
-- Afiliacion del paciente a EPS / aseguradora
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.patient_insurances (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       uuid NOT NULL DEFAULT sigm.current_tenant_id()
                    REFERENCES sigm.tenants(id) ON DELETE CASCADE,
    patient_id      uuid NOT NULL,
    insurer_id      uuid NOT NULL REFERENCES sigm.insurers(id) ON DELETE RESTRICT,
    affiliate_number varchar(60),
    plan_name       varchar(120),
    is_primary      boolean NOT NULL DEFAULT false,
    valid_from      date,
    valid_to        date,
    status          varchar(20) NOT NULL DEFAULT 'ACTIVE'
                    CHECK (status IN ('ACTIVE', 'INACTIVE', 'SUSPENDED')),
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    FOREIGN KEY (tenant_id, patient_id)
        REFERENCES sigm.patients (tenant_id, id) ON DELETE CASCADE,
    CHECK (valid_to IS NULL OR valid_from IS NULL OR valid_to >= valid_from)
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_patient_insurances_primary
    ON sigm.patient_insurances (tenant_id, patient_id)
    WHERE is_primary;

-- ----------------------------------------------------------------------------
-- Contratos con EPS / aseguradoras
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.contracts (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       uuid NOT NULL DEFAULT sigm.current_tenant_id()
                    REFERENCES sigm.tenants(id) ON DELETE CASCADE,
    insurer_id      uuid NOT NULL REFERENCES sigm.insurers(id) ON DELETE RESTRICT,
    contract_number varchar(80) NOT NULL,
    name            varchar(200) NOT NULL,
    billing_type    varchar(30) NOT NULL DEFAULT 'FEE_FOR_SERVICE'
                    CHECK (billing_type IN ('FEE_FOR_SERVICE', 'CAPITATION', 'PACKAGE', 'MIXED', 'OTHER')),
    valid_from      date,
    valid_to        date,
    status          varchar(20) NOT NULL DEFAULT 'ACTIVE'
                    CHECK (status IN ('DRAFT', 'ACTIVE', 'SUSPENDED', 'EXPIRED', 'TERMINATED')),
    notes           text,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    UNIQUE (tenant_id, contract_number),
    CHECK (valid_to IS NULL OR valid_from IS NULL OR valid_to >= valid_from)
);

-- ----------------------------------------------------------------------------
-- Catalogo de servicios facturables
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.service_catalog (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id   uuid NOT NULL DEFAULT sigm.current_tenant_id()
                REFERENCES sigm.tenants(id) ON DELETE CASCADE,
    code        varchar(40) NOT NULL,
    cups_code   varchar(30),
    name        varchar(250) NOT NULL,
    description text,
    category    varchar(80),
    unit        varchar(40) DEFAULT 'UNIDAD',
    base_price  numeric(14,2) NOT NULL DEFAULT 0 CHECK (base_price >= 0),
    tax_rate    numeric(5,2) NOT NULL DEFAULT 0 CHECK (tax_rate BETWEEN 0 AND 100),
    is_active   boolean NOT NULL DEFAULT true,
    created_at  timestamptz NOT NULL DEFAULT now(),
    updated_at  timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    UNIQUE (tenant_id, code)
);

-- ----------------------------------------------------------------------------
-- Tarifarios
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.price_lists (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id   uuid NOT NULL DEFAULT sigm.current_tenant_id()
                REFERENCES sigm.tenants(id) ON DELETE CASCADE,
    name        varchar(150) NOT NULL,
    insurer_id  uuid REFERENCES sigm.insurers(id) ON DELETE SET NULL,
    contract_id uuid,
    valid_from  date,
    valid_to    date,
    is_active   boolean NOT NULL DEFAULT true,
    created_at  timestamptz NOT NULL DEFAULT now(),
    updated_at  timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    FOREIGN KEY (tenant_id, contract_id)
        REFERENCES sigm.contracts (tenant_id, id) ON DELETE SET NULL,
    CHECK (valid_to IS NULL OR valid_from IS NULL OR valid_to >= valid_from)
);

CREATE TABLE IF NOT EXISTS sigm.price_list_items (
    id               uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id        uuid NOT NULL DEFAULT sigm.current_tenant_id(),
    price_list_id    uuid NOT NULL,
    service_id       uuid NOT NULL,
    price            numeric(14,2) NOT NULL CHECK (price >= 0),
    discount_percent numeric(5,2) NOT NULL DEFAULT 0 CHECK (discount_percent BETWEEN 0 AND 100),
    created_at       timestamptz NOT NULL DEFAULT now(),
    updated_at       timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    UNIQUE (tenant_id, price_list_id, service_id),
    FOREIGN KEY (tenant_id, price_list_id)
        REFERENCES sigm.price_lists (tenant_id, id) ON DELETE CASCADE,
    FOREIGN KEY (tenant_id, service_id)
        REFERENCES sigm.service_catalog (tenant_id, id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS sigm.contract_services (
    id                    uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id             uuid NOT NULL DEFAULT sigm.current_tenant_id(),
    contract_id           uuid NOT NULL,
    service_id            uuid NOT NULL,
    agreed_price          numeric(14,2) NOT NULL CHECK (agreed_price >= 0),
    copay_percent         numeric(5,2) NOT NULL DEFAULT 0 CHECK (copay_percent BETWEEN 0 AND 100),
    requires_authorization boolean NOT NULL DEFAULT false,
    created_at            timestamptz NOT NULL DEFAULT now(),
    updated_at            timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    UNIQUE (tenant_id, contract_id, service_id),
    FOREIGN KEY (tenant_id, contract_id)
        REFERENCES sigm.contracts (tenant_id, id) ON DELETE CASCADE,
    FOREIGN KEY (tenant_id, service_id)
        REFERENCES sigm.service_catalog (tenant_id, id) ON DELETE CASCADE
);

-- ----------------------------------------------------------------------------
-- Autorizaciones de servicios
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.authorizations (
    id                   uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id            uuid NOT NULL DEFAULT sigm.current_tenant_id()
                         REFERENCES sigm.tenants(id) ON DELETE CASCADE,
    patient_id           uuid NOT NULL,
    encounter_id         uuid,
    insurer_id           uuid NOT NULL REFERENCES sigm.insurers(id) ON DELETE RESTRICT,
    contract_id          uuid,
    service_id           uuid,
    authorization_number varchar(80) NOT NULL,
    status               varchar(20) NOT NULL DEFAULT 'REQUESTED'
                         CHECK (status IN ('REQUESTED', 'APPROVED', 'REJECTED', 'EXPIRED', 'CONSUMED', 'CANCELLED')),
    requested_at         timestamptz NOT NULL DEFAULT now(),
    approved_at          timestamptz,
    valid_from           date,
    valid_until          date,
    approved_amount      numeric(14,2) CHECK (approved_amount IS NULL OR approved_amount >= 0),
    notes                text,
    created_at           timestamptz NOT NULL DEFAULT now(),
    updated_at           timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    UNIQUE (tenant_id, authorization_number),
    FOREIGN KEY (tenant_id, patient_id)
        REFERENCES sigm.patients (tenant_id, id) ON DELETE CASCADE,
    FOREIGN KEY (tenant_id, encounter_id, patient_id)
        REFERENCES sigm.encounters (tenant_id, id, patient_id) ON DELETE SET NULL,
    FOREIGN KEY (tenant_id, contract_id)
        REFERENCES sigm.contracts (tenant_id, id) ON DELETE SET NULL,
    FOREIGN KEY (tenant_id, service_id)
        REFERENCES sigm.service_catalog (tenant_id, id) ON DELETE SET NULL,
    CHECK (valid_until IS NULL OR valid_from IS NULL OR valid_until >= valid_from)
);

CREATE INDEX IF NOT EXISTS ix_authorizations_patient
    ON sigm.authorizations (tenant_id, patient_id, status);

-- ----------------------------------------------------------------------------
-- Facturas
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.invoices (
    id                uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id         uuid NOT NULL DEFAULT sigm.current_tenant_id()
                      REFERENCES sigm.tenants(id) ON DELETE CASCADE,
    invoice_number    varchar(60) NOT NULL,
    patient_id        uuid NOT NULL,
    insurer_id        uuid REFERENCES sigm.insurers(id) ON DELETE SET NULL,
    contract_id       uuid,
    encounter_id      uuid,
    issue_date        date NOT NULL DEFAULT current_date,
    due_date          date,
    currency          char(3) NOT NULL DEFAULT 'COP',
    subtotal          numeric(14,2) NOT NULL DEFAULT 0 CHECK (subtotal >= 0),
    discount_total    numeric(14,2) NOT NULL DEFAULT 0 CHECK (discount_total >= 0),
    tax_total         numeric(14,2) NOT NULL DEFAULT 0 CHECK (tax_total >= 0),
    total             numeric(14,2) NOT NULL DEFAULT 0 CHECK (total >= 0),
    status            varchar(20) NOT NULL DEFAULT 'DRAFT'
                      CHECK (status IN ('DRAFT', 'ISSUED', 'PARTIALLY_PAID', 'PAID', 'VOID')),
    electronic_status varchar(20) NOT NULL DEFAULT 'PENDING'
                      CHECK (electronic_status IN ('PENDING', 'SENT', 'ACCEPTED', 'REJECTED', 'NOT_APPLICABLE')),
    cufe              varchar(120),
    xml_storage_key   text,
    dian_response     jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_by        uuid,
    created_at        timestamptz NOT NULL DEFAULT now(),
    updated_at        timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    UNIQUE (tenant_id, invoice_number),
    FOREIGN KEY (tenant_id, patient_id)
        REFERENCES sigm.patients (tenant_id, id) ON DELETE RESTRICT,
    FOREIGN KEY (tenant_id, contract_id)
        REFERENCES sigm.contracts (tenant_id, id) ON DELETE SET NULL,
    FOREIGN KEY (tenant_id, encounter_id)
        REFERENCES sigm.encounters (tenant_id, id) ON DELETE SET NULL,
    FOREIGN KEY (tenant_id, created_by)
        REFERENCES sigm.user_tenants (tenant_id, user_id) ON DELETE SET NULL,
    CHECK (due_date IS NULL OR due_date >= issue_date),
    CHECK (total = subtotal - discount_total + tax_total)
);

CREATE INDEX IF NOT EXISTS ix_invoices_patient
    ON sigm.invoices (tenant_id, patient_id, issue_date DESC);

CREATE INDEX IF NOT EXISTS ix_invoices_status
    ON sigm.invoices (tenant_id, status, due_date);

CREATE INDEX IF NOT EXISTS ix_invoices_electronic
    ON sigm.invoices (tenant_id, electronic_status);

-- ----------------------------------------------------------------------------
-- Detalle de factura
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.invoice_items (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       uuid NOT NULL DEFAULT sigm.current_tenant_id(),
    invoice_id      uuid NOT NULL,
    service_id      uuid,
    description     varchar(250) NOT NULL,
    quantity        numeric(12,2) NOT NULL DEFAULT 1 CHECK (quantity > 0),
    unit_price      numeric(14,2) NOT NULL CHECK (unit_price >= 0),
    discount_amount numeric(14,2) NOT NULL DEFAULT 0 CHECK (discount_amount >= 0),
    tax_rate        numeric(5,2) NOT NULL DEFAULT 0 CHECK (tax_rate BETWEEN 0 AND 100),
    tax_amount      numeric(14,2) NOT NULL DEFAULT 0 CHECK (tax_amount >= 0),
    line_total      numeric(14,2) GENERATED ALWAYS AS (
                        round(quantity * unit_price - discount_amount + tax_amount, 2)
                    ) STORED,
    encounter_id    uuid,
    created_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    FOREIGN KEY (tenant_id, invoice_id)
        REFERENCES sigm.invoices (tenant_id, id) ON DELETE CASCADE,
    FOREIGN KEY (tenant_id, service_id)
        REFERENCES sigm.service_catalog (tenant_id, id) ON DELETE SET NULL,
    FOREIGN KEY (tenant_id, encounter_id)
        REFERENCES sigm.encounters (tenant_id, id) ON DELETE SET NULL
);

-- ----------------------------------------------------------------------------
-- Pagos
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.payments (
    id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id      uuid NOT NULL DEFAULT sigm.current_tenant_id(),
    invoice_id     uuid NOT NULL,
    payment_date   timestamptz NOT NULL DEFAULT now(),
    amount         numeric(14,2) NOT NULL CHECK (amount > 0),
    payment_method varchar(40) NOT NULL DEFAULT 'CASH'
                   CHECK (payment_method IN ('CASH', 'CARD', 'TRANSFER', 'CHECK', 'INSURER', 'OTHER')),
    reference      varchar(120),
    received_by    uuid,
    notes          text,
    created_at     timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    FOREIGN KEY (tenant_id, invoice_id)
        REFERENCES sigm.invoices (tenant_id, id) ON DELETE RESTRICT,
    FOREIGN KEY (tenant_id, received_by)
        REFERENCES sigm.user_tenants (tenant_id, user_id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS ix_payments_invoice
    ON sigm.payments (tenant_id, invoice_id, payment_date DESC);

-- ----------------------------------------------------------------------------
-- Notas credito / debito
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.credit_debit_notes (
    id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id     uuid NOT NULL DEFAULT sigm.current_tenant_id(),
    invoice_id    uuid NOT NULL,
    note_number   varchar(60) NOT NULL,
    note_type     varchar(10) NOT NULL CHECK (note_type IN ('CREDIT', 'DEBIT')),
    reason        text NOT NULL,
    amount        numeric(14,2) NOT NULL CHECK (amount > 0),
    issued_at     timestamptz NOT NULL DEFAULT now(),
    status        varchar(20) NOT NULL DEFAULT 'DRAFT'
                  CHECK (status IN ('DRAFT', 'ISSUED', 'ACCEPTED', 'REJECTED', 'VOID')),
    cufe          varchar(120),
    created_by    uuid,
    created_at    timestamptz NOT NULL DEFAULT now(),
    updated_at    timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    UNIQUE (tenant_id, note_number),
    FOREIGN KEY (tenant_id, invoice_id)
        REFERENCES sigm.invoices (tenant_id, id) ON DELETE RESTRICT,
    FOREIGN KEY (tenant_id, created_by)
        REFERENCES sigm.user_tenants (tenant_id, user_id) ON DELETE SET NULL
);

-- ----------------------------------------------------------------------------
-- RIPS
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sigm.rips_reports (
    id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id      uuid NOT NULL DEFAULT sigm.current_tenant_id()
                   REFERENCES sigm.tenants(id) ON DELETE CASCADE,
    period_start   date NOT NULL,
    period_end     date NOT NULL,
    report_type    varchar(40) NOT NULL DEFAULT 'CONSULTATION',
    status         varchar(20) NOT NULL DEFAULT 'DRAFT'
                   CHECK (status IN ('DRAFT', 'GENERATED', 'TRANSMITTED', 'ACCEPTED', 'REJECTED')),
    total_records  integer NOT NULL DEFAULT 0 CHECK (total_records >= 0),
    total_amount   numeric(14,2) NOT NULL DEFAULT 0 CHECK (total_amount >= 0),
    file_key       text,
    generated_at   timestamptz,
    transmitted_at timestamptz,
    response       jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at     timestamptz NOT NULL DEFAULT now(),
    updated_at     timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    UNIQUE (tenant_id, period_start, period_end, report_type),
    CHECK (period_end >= period_start)
);

CREATE TABLE IF NOT EXISTS sigm.rips_report_details (
    id                uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id         uuid NOT NULL DEFAULT sigm.current_tenant_id(),
    rips_report_id    uuid NOT NULL,
    invoice_id        uuid,
    patient_id        uuid NOT NULL,
    service_id        uuid,
    payload           jsonb NOT NULL DEFAULT '{}'::jsonb,
    validation_status varchar(20) NOT NULL DEFAULT 'PENDING'
                      CHECK (validation_status IN ('PENDING', 'VALID', 'INVALID')),
    validation_errors jsonb NOT NULL DEFAULT '[]'::jsonb,
    created_at        timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, id),
    FOREIGN KEY (tenant_id, rips_report_id)
        REFERENCES sigm.rips_reports (tenant_id, id) ON DELETE CASCADE,
    FOREIGN KEY (tenant_id, invoice_id)
        REFERENCES sigm.invoices (tenant_id, id) ON DELETE SET NULL,
    FOREIGN KEY (tenant_id, patient_id)
        REFERENCES sigm.patients (tenant_id, id) ON DELETE CASCADE,
    FOREIGN KEY (tenant_id, service_id)
        REFERENCES sigm.service_catalog (tenant_id, id) ON DELETE SET NULL
);

-- ----------------------------------------------------------------------------
-- Vista de cartera (respeta RLS de las tablas subyacentes)
-- ----------------------------------------------------------------------------
CREATE OR REPLACE VIEW sigm.v_accounts_receivable
WITH (security_invoker = true)
AS
SELECT
    i.tenant_id,
    i.id AS invoice_id,
    i.invoice_number,
    i.patient_id,
    i.insurer_id,
    i.issue_date,
    i.due_date,
    i.total,
    coalesce(p.paid_amount, 0) AS paid_amount,
    i.total - coalesce(p.paid_amount, 0) AS balance,
    CASE
        WHEN i.status = 'VOID' THEN 'VOID'
        WHEN i.total - coalesce(p.paid_amount, 0) <= 0 THEN 'PAID'
        WHEN i.due_date IS NOT NULL AND i.due_date < current_date THEN 'OVERDUE'
        ELSE 'PENDING'
    END AS receivable_status
FROM sigm.invoices i
LEFT JOIN (
    SELECT tenant_id, invoice_id, sum(amount) AS paid_amount
    FROM sigm.payments
    GROUP BY tenant_id, invoice_id
) p ON p.tenant_id = i.tenant_id AND p.invoice_id = i.id
WHERE i.status <> 'VOID';

COMMIT;
