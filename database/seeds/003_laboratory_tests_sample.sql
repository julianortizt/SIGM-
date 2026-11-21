-- ============================================================================
-- SIGM CLOUD - SEED 003 - Examenes de laboratorio (muestra)
-- El catalogo oficial de examenes de laboratorio depende de la institucion.
-- Estas filas permiten operar el modulo de laboratorio en desarrollo.
-- ============================================================================
BEGIN;

INSERT INTO sigm.laboratory_tests (code, name, sample_type, unit, reference_range) VALUES
('LAB_HEMOGRAMA',    'Hemograma completo', 'Sangre total', 'cells/uL', 'Ver reporte'),
('LAB_GLUCOSA',      'Glucosa en ayunas', 'Suero', 'mg/dL', '70 - 100'),
('LAB_CREATININA',   'Creatinina serica', 'Suero', 'mg/dL', '0.6 - 1.2'),
('LAB_UREA',         'Urea serica', 'Suero', 'mg/dL', '15 - 45'),
('LAB_COLESTEROL',   'Colesterol total', 'Suero', 'mg/dL', '< 200'),
('LAB_TRIGLICERIDOS','Trigliceridos', 'Suero', 'mg/dL', '< 150'),
('LAB_HBA1C',        'Hemoglobina glicosilada', 'Sangre total', '%', '4.0 - 5.6'),
('LAB_TSH',          'Hormona estimulante de tiroides', 'Suero', 'mUI/L', '0.4 - 4.0'),
('LAB_UROANALISIS',  'Uroanalisis', 'Orina', 'N/A', 'Ver reporte'),
('LAB_HEPATICA',     'Perfil hepatico', 'Suero', 'N/A', 'Ver reporte'),
('LAB_EMBARAZO',     'Prueba de embarazo en sangre', 'Suero', 'mUI/mL', '< 5 no embarazo'),
('LAB_COVID',        'Prueba antigeno SARS-CoV-2', 'Hisopado', 'N/A', 'Negativo')
ON CONFLICT (code) DO UPDATE
SET name = EXCLUDED.name,
    sample_type = EXCLUDED.sample_type,
    unit = EXCLUDED.unit,
    reference_range = EXCLUDED.reference_range;

COMMIT;
