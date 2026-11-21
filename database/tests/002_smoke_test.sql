-- ============================================================================
-- SIGM CLOUD - PRUEBA FUNCIONAL DEL NUCLEO
-- Valida triggers de integridad, inmutabilidad clinica, exclusion de agenda,
-- recalculo de facturas y vista de cartera. Ejecutar despues de run_all.sql.
-- Todo el script termina en ROLLBACK: no deja datos de prueba.
-- ============================================================================
\set ON_ERROR_STOP on

BEGIN;

-- ----------------------------------------------------------------------------
-- Datos base de un tenant
-- ----------------------------------------------------------------------------
INSERT INTO sigm.tenants (id, tenant_code, legal_name, commercial_name, product_type)
VALUES ('33333333-3333-3333-3333-333333333333', 'SMOKE', 'Smoke Test S.A.S.', 'Smoke Test', 'CLINIC');

INSERT INTO sigm.organizations (id, tenant_id, legal_name, commercial_name, is_primary)
VALUES ('33333333-3333-3333-3333-333333330001',
        '33333333-3333-3333-3333-333333333333',
        'Smoke Test S.A.S.', 'Sede principal', true);

INSERT INTO sigm.sites (id, tenant_id, organization_id, code, name)
VALUES ('33333333-3333-3333-3333-333333330002',
        '33333333-3333-3333-3333-333333333333',
        '33333333-3333-3333-3333-333333330001', 'S1', 'Sede principal');

INSERT INTO sigm.medical_offices (id, tenant_id, site_id, code, name)
VALUES ('33333333-3333-3333-3333-333333330003',
        '33333333-3333-3333-3333-333333333333',
        '33333333-3333-3333-3333-333333330002', 'C1', 'Consultorio 1');

INSERT INTO sigm.users (id, email, full_name)
VALUES ('33333333-3333-3333-3333-333333330004', 'medico@smoke.test', 'Dra. Smoke');

INSERT INTO sigm.user_tenants (tenant_id, user_id, status, default_site_id)
VALUES ('33333333-3333-3333-3333-333333333333',
        '33333333-3333-3333-3333-333333330004', 'ACTIVE',
        '33333333-3333-3333-3333-333333330002');

INSERT INTO sigm.tenant_specialties (tenant_id, specialty_id)
SELECT '33333333-3333-3333-3333-333333333333', id
FROM sigm.specialties WHERE code = 'GENERAL_MEDICINE';

INSERT INTO sigm.professionals (id, tenant_id, user_id, primary_site_id, display_name)
VALUES ('33333333-3333-3333-3333-333333330005',
        '33333333-3333-3333-3333-333333333333',
        '33333333-3333-3333-3333-333333330004',
        '33333333-3333-3333-3333-333333330002', 'Dra. Smoke');

INSERT INTO sigm.professional_specialties (tenant_id, professional_id, specialty_id)
SELECT '33333333-3333-3333-3333-333333333333',
       '33333333-3333-3333-3333-333333330005', id
FROM sigm.specialties WHERE code = 'GENERAL_MEDICINE';

INSERT INTO sigm.patients (id, tenant_id, document_type, document_number, first_name, last_name)
VALUES ('33333333-3333-3333-3333-333333330006',
        '33333333-3333-3333-3333-333333333333', 'CC', '900000001', 'Paciente', 'Smoke');

INSERT INTO sigm.clinical_records (tenant_id, patient_id)
VALUES ('33333333-3333-3333-3333-333333333333',
        '33333333-3333-3333-3333-333333330006');

-- ----------------------------------------------------------------------------
-- Agenda: una cita y bloqueo de solapamiento
-- ----------------------------------------------------------------------------
INSERT INTO sigm.appointments (
    id, tenant_id, patient_id, professional_id, specialty_id, site_id,
    medical_office_id, starts_at, ends_at
) SELECT
    '33333333-3333-3333-3333-333333330007',
    '33333333-3333-3333-3333-333333333333',
    '33333333-3333-3333-3333-333333330006',
    '33333333-3333-3333-3333-333333330005',
    s.id,
    '33333333-3333-3333-3333-333333330002',
    '33333333-3333-3333-3333-333333330003',
    timestamptz '2026-12-01 09:00:00-05',
    timestamptz '2026-12-01 09:30:00-05'
FROM sigm.specialties s WHERE s.code = 'GENERAL_MEDICINE';

DO $$
BEGIN
    BEGIN
        INSERT INTO sigm.appointments (
            tenant_id, patient_id, professional_id, specialty_id, site_id,
            medical_office_id, starts_at, ends_at
        ) SELECT
            '33333333-3333-3333-3333-333333333333',
            '33333333-3333-3333-3333-333333330006',
            '33333333-3333-3333-3333-333333330005',
            s.id,
            '33333333-3333-3333-3333-333333330002',
            '33333333-3333-3333-3333-333333330003',
            timestamptz '2026-12-01 09:15:00-05',
            timestamptz '2026-12-01 09:45:00-05'
        FROM sigm.specialties s WHERE s.code = 'GENERAL_MEDICINE';

        RAISE EXCEPTION 'Fallo: se permitio una cita solapada';
    EXCEPTION
        WHEN exclusion_violation THEN
            RAISE NOTICE 'Agenda: solapamiento bloqueado correctamente';
    END;
END $$;

-- ----------------------------------------------------------------------------
-- Historia clinica: nota, version, firma e inmutabilidad
-- ----------------------------------------------------------------------------
INSERT INTO sigm.encounters (
    id, tenant_id, patient_id, professional_id, specialty_id, site_id,
    appointment_id, encounter_type, status
) SELECT
    '33333333-3333-3333-3333-333333330008',
    '33333333-3333-3333-3333-333333333333',
    '33333333-3333-3333-3333-333333330006',
    '33333333-3333-3333-3333-333333330005',
    s.id,
    '33333333-3333-3333-3333-333333330002',
    '33333333-3333-3333-3333-333333330007',
    'CONSULTATION', 'COMPLETED'
FROM sigm.specialties s WHERE s.code = 'GENERAL_MEDICINE';

INSERT INTO sigm.clinical_notes (
    id, tenant_id, encounter_id, patient_id, author_user_id, note_type, status
) VALUES (
    '33333333-3333-3333-3333-333333330009',
    '33333333-3333-3333-3333-333333333333',
    '33333333-3333-3333-3333-333333330008',
    '33333333-3333-3333-3333-333333330006',
    '33333333-3333-3333-3333-333333330004',
    'CONSULTATION', 'DRAFT'
);

INSERT INTO sigm.clinical_note_versions (
    tenant_id, note_id, version_number, content, author_user_id, signed_at, signature_hash
) VALUES (
    '33333333-3333-3333-3333-333333333333',
    '33333333-3333-3333-3333-333333330009',
    1,
    '{"chief_complaint": "Control", "plan": "Observacion"}'::jsonb,
    '33333333-3333-3333-3333-333333330004',
    now(),
    encode(digest('version-1', 'sha256'), 'hex')
);

DO $$
DECLARE
    v_version integer;
BEGIN
    SELECT current_version INTO v_version
    FROM sigm.clinical_notes
    WHERE id = '33333333-3333-3333-3333-333333330009';

    IF v_version <> 1 THEN
        RAISE EXCEPTION 'Fallo: current_version esperado 1, obtenido %', v_version;
    END IF;
END $$;

UPDATE sigm.clinical_notes
SET status = 'SIGNED'
WHERE id = '33333333-3333-3333-3333-333333330009';

DO $$
BEGIN
    BEGIN
        UPDATE sigm.clinical_note_versions
        SET content = '{"alterado": true}'::jsonb
        WHERE note_id = '33333333-3333-3333-3333-333333330009';

        RAISE EXCEPTION 'Fallo: se permitio modificar una version firmada';
    EXCEPTION
        WHEN object_not_in_prerequisite_state THEN
            RAISE NOTICE 'Historia clinica: version firmada inmutable';
    END;
END $$;

-- ----------------------------------------------------------------------------
-- Facturacion: recalculo de totales y cartera
-- ----------------------------------------------------------------------------
INSERT INTO sigm.service_catalog (tenant_id, code, cups_code, name, base_price, tax_rate)
VALUES
    ('33333333-3333-3333-3333-333333333333', 'SRV_CONSULTA', '890201', 'Consulta de medicina general', 120000, 0),
    ('33333333-3333-3333-3333-333333333333', 'SRV_HEMOGRAMA', '931101', 'Hemograma completo', 45000, 0);

INSERT INTO sigm.invoices (
    id, tenant_id, invoice_number, patient_id, encounter_id, issue_date, due_date
) VALUES (
    '33333333-3333-3333-3333-333333330010',
    '33333333-3333-3333-3333-333333333333',
    'FV-SMOKE-001',
    '33333333-3333-3333-3333-333333330006',
    '33333333-3333-3333-3333-333333330008',
    current_date, current_date + 30
);

INSERT INTO sigm.invoice_items (
    tenant_id, invoice_id, service_id, description, quantity, unit_price, discount_amount, tax_rate, tax_amount
)
SELECT
    '33333333-3333-3333-3333-333333333333',
    '33333333-3333-3333-3333-333333330010',
    id, name, 1, base_price, 10000, 0, 0
FROM sigm.service_catalog
WHERE tenant_id = '33333333-3333-3333-3333-333333333333'
  AND code = 'SRV_CONSULTA';

INSERT INTO sigm.invoice_items (
    tenant_id, invoice_id, service_id, description, quantity, unit_price, discount_amount, tax_rate, tax_amount
)
SELECT
    '33333333-3333-3333-3333-333333333333',
    '33333333-3333-3333-3333-333333330010',
    id, name, 2, base_price, 0, 0, 0
FROM sigm.service_catalog
WHERE tenant_id = '33333333-3333-3333-3333-333333333333'
  AND code = 'SRV_HEMOGRAMA';

DO $$
DECLARE
    v_total numeric(14,2);
    v_subtotal numeric(14,2);
BEGIN
    SELECT total, subtotal INTO v_total, v_subtotal
    FROM sigm.invoices
    WHERE id = '33333333-3333-3333-3333-333333330010';

    IF v_subtotal <> 210000 THEN
        RAISE EXCEPTION 'Fallo: subtotal esperado 210000, obtenido %', v_subtotal;
    END IF;

    IF v_total <> 200000 THEN
        RAISE EXCEPTION 'Fallo: total esperado 200000, obtenido %', v_total;
    END IF;

    RAISE NOTICE 'Facturacion: totales recalculados correctamente (subtotal %, total %)', v_subtotal, v_total;
END $$;

INSERT INTO sigm.payments (tenant_id, invoice_id, amount, payment_method)
VALUES ('33333333-3333-3333-3333-333333333333',
        '33333333-3333-3333-3333-333333330010', 50000, 'CASH');

DO $$
DECLARE
    v_balance numeric(14,2);
    v_status text;
BEGIN
    SELECT balance, receivable_status INTO v_balance, v_status
    FROM sigm.v_accounts_receivable
    WHERE invoice_id = '33333333-3333-3333-3333-333333330010';

    IF v_balance <> 150000 THEN
        RAISE EXCEPTION 'Fallo: saldo esperado 150000, obtenido %', v_balance;
    END IF;

    IF v_status <> 'PENDING' THEN
        RAISE EXCEPTION 'Fallo: estado de cartera esperado PENDING, obtenido %', v_status;
    END IF;

    RAISE NOTICE 'Cartera: saldo % y estado % correctos', v_balance, v_status;
END $$;

ROLLBACK;
