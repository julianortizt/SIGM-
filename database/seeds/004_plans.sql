-- ============================================================================
-- SIGM CLOUD - SEED 004 - Planes comerciales iniciales
-- Los valores son parametros de ejemplo sujetos a decision comercial.
-- ============================================================================
BEGIN;

INSERT INTO sigm.plans (
    code, name, product_type, description, price, currency, billing_period,
    max_users, max_sites, storage_limit_mb, features, is_active
) VALUES
(
    'PRACTICE_BASIC_MONTHLY',
    'Consultorio Basico',
    'PRACTICE',
    'Plan inicial para medico independiente.',
    99000, 'COP', 'MONTHLY', 2, 1, 5120,
    '{"patients": true, "clinical_records": true, "appointments": true, "prescriptions": true, "documents": true, "patient_portal": true, "billing": true, "laboratory": false, "multi_site": false, "reports": "basic"}'::jsonb,
    true
),
(
    'PRACTICE_PRO_MONTHLY',
    'Consultorio Profesional',
    'PRACTICE',
    'Plan para consultorio con equipo y laboratorio opcional.',
    179000, 'COP', 'MONTHLY', 6, 2, 20480,
    '{"patients": true, "clinical_records": true, "appointments": true, "prescriptions": true, "documents": true, "patient_portal": true, "billing": true, "laboratory": true, "multi_site": true, "reports": "professional"}'::jsonb,
    true
),
(
    'CLINIC_STARTER_MONTHLY',
    'Clinica Starter',
    'CLINIC',
    'Plan para clinicas pequenas de una sede.',
    349000, 'COP', 'MONTHLY', 15, 1, 51200,
    '{"patients": true, "clinical_records": true, "appointments": true, "prescriptions": true, "documents": true, "patient_portal": true, "billing": true, "laboratory": true, "multi_site": false, "reports": "advanced", "eps": true, "rips": true}'::jsonb,
    true
),
(
    'CLINIC_PRO_MONTHLY',
    'Clinica Profesional',
    'CLINIC',
    'Plan para clinicas multiservicio y varias sedes.',
    699000, 'COP', 'MONTHLY', 50, 5, 204800,
    '{"patients": true, "clinical_records": true, "appointments": true, "prescriptions": true, "documents": true, "patient_portal": true, "billing": true, "laboratory": true, "multi_site": true, "reports": "advanced", "eps": true, "rips": true, "interoperability": true}'::jsonb,
    true
),
(
    'CLINIC_ENTERPRISE_YEARLY',
    'Clinica Enterprise',
    'CLINIC',
    'Plan institucional con aislamiento dedicado y soporte extendido.',
    12990000, 'COP', 'YEARLY', NULL, NULL, NULL,
    '{"patients": true, "clinical_records": true, "appointments": true, "prescriptions": true, "documents": true, "patient_portal": true, "billing": true, "laboratory": true, "multi_site": true, "reports": "enterprise", "eps": true, "rips": true, "interoperability": true, "dedicated_database": true, "priority_support": true}'::jsonb,
    true
)
ON CONFLICT (code) DO UPDATE
SET name = EXCLUDED.name,
    product_type = EXCLUDED.product_type,
    description = EXCLUDED.description,
    price = EXCLUDED.price,
    currency = EXCLUDED.currency,
    billing_period = EXCLUDED.billing_period,
    max_users = EXCLUDED.max_users,
    max_sites = EXCLUDED.max_sites,
    storage_limit_mb = EXCLUDED.storage_limit_mb,
    features = EXCLUDED.features,
    is_active = EXCLUDED.is_active;

COMMIT;
