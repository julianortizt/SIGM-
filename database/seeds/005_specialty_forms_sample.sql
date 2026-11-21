-- ============================================================================
-- SIGM CLOUD - SEED 005 - Formularios clinicos globales de ejemplo
-- ============================================================================
BEGIN;

INSERT INTO sigm.specialty_forms (
    tenant_id, specialty_id, name, version, description, schema, is_active
)
SELECT
    NULL,
    s.id,
    'Historia clinica de medicina general',
    1,
    'Formulario base para consulta de medicina general.',
    '{
      "sections": [
        {"key": "motivo", "title": "Motivo de consulta"},
        {"key": "enfermedad_actual", "title": "Enfermedad actual"},
        {"key": "antecedentes", "title": "Antecedentes"},
        {"key": "revision_sistemas", "title": "Revision por sistemas"},
        {"key": "examen_fisico", "title": "Examen fisico"},
        {"key": "diagnostico", "title": "Diagnostico"},
        {"key": "plan", "title": "Plan de tratamiento"}
      ]
    }'::jsonb,
    true
FROM sigm.specialties s
WHERE s.code = 'GENERAL_MEDICINE'
ON CONFLICT (specialty_id, name, version) WHERE tenant_id IS NULL DO NOTHING;

INSERT INTO sigm.specialty_form_fields (
    tenant_id, form_id, field_key, label, field_type, required, options, config, sort_order
)
SELECT
    NULL,
    f.id,
    v.field_key,
    v.label,
    v.field_type,
    v.required,
    v.options::jsonb,
    '{}'::jsonb,
    v.sort_order
FROM sigm.specialty_forms f
CROSS JOIN (VALUES
    ('chief_complaint', 'Motivo de consulta', 'TEXTAREA', true,  '[]', 10),
    ('current_illness', 'Enfermedad actual', 'TEXTAREA', true,  '[]', 20),
    ('personal_history', 'Antecedentes personales', 'TEXTAREA', false, '[]', 30),
    ('family_history', 'Antecedentes familiares', 'TEXTAREA', false, '[]', 40),
    ('allergies', 'Alergias', 'TEXTAREA', false, '[]', 50),
    ('review_systems', 'Revision por sistemas', 'TEXTAREA', false, '[]', 60),
    ('physical_exam', 'Examen fisico', 'TEXTAREA', true,  '[]', 70),
    ('diagnosis', 'Diagnostico', 'TEXTAREA', true,  '[]', 80),
    ('plan', 'Plan de tratamiento', 'TEXTAREA', true,  '[]', 90),
    ('observations', 'Observaciones', 'TEXTAREA', false, '[]', 100)
) AS v(field_key, label, field_type, required, options, sort_order)
WHERE f.tenant_id IS NULL
  AND f.name = 'Historia clinica de medicina general'
  AND f.version = 1
ON CONFLICT (form_id, field_key) DO NOTHING;

INSERT INTO sigm.specialty_forms (
    tenant_id, specialty_id, name, version, description, schema, is_active
)
SELECT
    NULL,
    s.id,
    'Historia clinica pediatrica',
    1,
    'Formulario base para consulta pediatrica.',
    '{
      "sections": [
        {"key": "motivo", "title": "Motivo de consulta"},
        {"key": "enfermedad_actual", "title": "Enfermedad actual"},
        {"key": "antecedentes_perinatales", "title": "Antecedentes perinatales"},
        {"key": "crecimiento_desarrollo", "title": "Crecimiento y desarrollo"},
        {"key": "examen_fisico", "title": "Examen fisico"},
        {"key": "diagnostico", "title": "Diagnostico"},
        {"key": "plan", "title": "Plan"}
      ]
    }'::jsonb,
    true
FROM sigm.specialties s
WHERE s.code = 'PEDIATRICS'
ON CONFLICT (specialty_id, name, version) WHERE tenant_id IS NULL DO NOTHING;

INSERT INTO sigm.specialty_form_fields (
    tenant_id, form_id, field_key, label, field_type, required, options, config, sort_order
)
SELECT
    NULL,
    f.id,
    v.field_key,
    v.label,
    v.field_type,
    v.required,
    v.options::jsonb,
    '{}'::jsonb,
    v.sort_order
FROM sigm.specialty_forms f
CROSS JOIN (VALUES
    ('chief_complaint', 'Motivo de consulta', 'TEXTAREA', true,  '[]', 10),
    ('current_illness', 'Enfermedad actual', 'TEXTAREA', true,  '[]', 20),
    ('perinatal_history', 'Antecedentes perinatales', 'TEXTAREA', false, '[]', 30),
    ('growth_development', 'Crecimiento y desarrollo', 'TEXTAREA', false, '[]', 40),
    ('physical_exam', 'Examen fisico', 'TEXTAREA', true,  '[]', 50),
    ('diagnosis', 'Diagnostico', 'TEXTAREA', true,  '[]', 60),
    ('plan', 'Plan', 'TEXTAREA', true,  '[]', 70)
) AS v(field_key, label, field_type, required, options, sort_order)
WHERE f.tenant_id IS NULL
  AND f.name = 'Historia clinica pediatrica'
  AND f.version = 1
ON CONFLICT (form_id, field_key) DO NOTHING;

COMMIT;
