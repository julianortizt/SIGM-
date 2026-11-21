-- ============================================================================
-- SIGM CLOUD - SEED 003 (FALLBACK) - Muestras minimas de catalogos clinicos
--
-- NO se ejecuta desde run_all.sql. En instalaciones normales los catalogos
-- completos se cargan desde los seeds 006 (CIE-10), 007 (CUPS) y 008 (IUM),
-- generados por import/import_catalogs.py desde SISPRO.
--
-- Use este archivo solo para demos sin conectividad o para pruebas unitarias
-- que no requieran los catalogos oficiales completos.
-- ============================================================================
BEGIN;

INSERT INTO sigm.diagnosis_catalog (code, description, chapter, chapter_code, group_code) VALUES
('A09',   'Diarrea y gastroenteritis de presunto origen infeccioso', 'Enfermedades infecciosas y parasitarias', 'I', 'A00-A09'),
('A15.0', 'Tuberculosis pulmonar confirmada por microscopia', 'Enfermedades infecciosas y parasitarias', 'I', 'A15-A19'),
('B34.9', 'Infeccion viral no especificada', 'Enfermedades infecciosas y parasitarias', 'I', 'B25-B34'),
('C50.9', 'Tumor maligno de la mama, no especificado', 'Tumores', 'II', 'C50-C50'),
('D50.9', 'Anemia por deficiencia de hierro no especificada', 'Enfermedades de la sangre', 'III', 'D50-D53'),
('E03.9', 'Hipotiroidismo no especificado', 'Enfermedades endocrinas y metabolicas', 'IV', 'E00-E07'),
('E11.9', 'Diabetes mellitus tipo 2 sin complicaciones', 'Enfermedades endocrinas y metabolicas', 'IV', 'E10-E14'),
('E66.9', 'Obesidad no especificada', 'Enfermedades endocrinas y metabolicas', 'IV', 'E65-E68'),
('F32.9', 'Episodio depresivo no especificado', 'Trastornos mentales y del comportamiento', 'V', 'F30-F39'),
('F41.1', 'Trastorno de ansiedad generalizada', 'Trastornos mentales y del comportamiento', 'V', 'F40-F48'),
('G43.9', 'Migrana no especificada', 'Enfermedades del sistema nervioso', 'VI', 'G40-G47'),
('H10.9', 'Conjuntivitis no especificada', 'Enfermedades del ojo y sus anexos', 'VII', 'H10-H13'),
('I10',   'Hipertension esencial (primaria)', 'Enfermedades del sistema circulatorio', 'IX', 'I10-I15'),
('I25.9', 'Enfermedad isquemica cronica del corazon no especificada', 'Enfermedades del sistema circulatorio', 'IX', 'I20-I25'),
('J00',   'Rinofaringitis aguda (resfriado comun)', 'Enfermedades del sistema respiratorio', 'X', 'J00-J06'),
('J18.9', 'Neumonia no especificada', 'Enfermedades del sistema respiratorio', 'X', 'J09-J18'),
('J45.9', 'Asma no especificada', 'Enfermedades del sistema respiratorio', 'X', 'J40-J47'),
('K21.9', 'Enfermedad por reflujo gastroesofagico sin esofagitis', 'Enfermedades del aparato digestivo', 'XI', 'K20-K31'),
('K29.7', 'Gastritis no especificada', 'Enfermedades del aparato digestivo', 'XI', 'K20-K31'),
('L30.9', 'Dermatitis no especificada', 'Enfermedades de la piel', 'XII', 'L20-L30'),
('M54.5', 'Lumbago no especificado', 'Enfermedades del sistema osteomuscular', 'XIII', 'M50-M54'),
('N39.0', 'Infeccion de vias urinarias, sitio no especificado', 'Enfermedades del aparato genitourinario', 'XIV', 'N30-N39'),
('O80',   'Parto unico espontaneo', 'Embarazo, parto y puerperio', 'XV', 'O80-O84'),
('R05',   'Tos', 'Sintomas y signos no clasificados', 'XVIII', 'R00-R09'),
('R50.9', 'Fiebre no especificada', 'Sintomas y signos no clasificados', 'XVIII', 'R50-R69'),
('S93.4', 'Esguince y torcedura de tobillo', 'Traumatismos y envenenamientos', 'XIX', 'S90-S99'),
('Z00.0', 'Examen medico general', 'Factores que influyen en el estado de salud', 'XXI', 'Z00-Z13'),
('Z71.3', 'Consejo y supervision dietetica', 'Factores que influyen en el estado de salud', 'XXI', 'Z70-Z76'),
('Z76.0', 'Repeticion de receta', 'Factores que influyen en el estado de salud', 'XXI', 'Z70-Z76')
ON CONFLICT (code) DO UPDATE
SET description = EXCLUDED.description,
    chapter = EXCLUDED.chapter,
    chapter_code = EXCLUDED.chapter_code,
    group_code = EXCLUDED.group_code;

INSERT INTO sigm.procedure_catalog (code, name, chapter) VALUES
('890201', 'Consulta de medicina general', 'Consultas'),
('890301', 'Consulta de medicina especializada', 'Consultas'),
('890401', 'Consulta de urgencias', 'Consultas'),
('903101', 'Electrocardiograma de reposo', 'Diagnostico cardiovascular'),
('930101', 'Toma de muestras de laboratorio', 'Laboratorio clinico'),
('931101', 'Hemograma completo', 'Laboratorio clinico'),
('931201', 'Glucosa en sangre', 'Laboratorio clinico'),
('931301', 'Perfil lipidico', 'Laboratorio clinico'),
('931401', 'Creatinina en sangre', 'Laboratorio clinico'),
('932101', 'Uroanalisis', 'Laboratorio clinico'),
('871101', 'Curacion de herida', 'Procedimientos menores'),
('871201', 'Sutura de herida', 'Procedimientos menores'),
('991101', 'Terapia fisica individual', 'Rehabilitacion'),
('992101', 'Consulta de nutricion', 'Consultas'),
('993101', 'Consulta de psicologia', 'Consultas')
ON CONFLICT (code) DO UPDATE SET name = EXCLUDED.name, chapter = EXCLUDED.chapter;

INSERT INTO sigm.medication_catalog (code, name, presentation, concentration, pharmaceutical_form, route) VALUES
('MED_ACETAMINOFEN', 'Acetaminofen', 'Tableta', '500 mg', 'Tableta', 'Oral'),
('MED_IBUPROFENO',   'Ibuprofeno', 'Tableta', '400 mg', 'Tableta', 'Oral'),
('MED_AMOXICILINA',  'Amoxicilina', 'Capsula', '500 mg', 'Capsula', 'Oral'),
('MED_AZITROMICINA', 'Azitromicina', 'Tableta', '500 mg', 'Tableta', 'Oral'),
('MED_LOSARTAN',     'Losartan', 'Tableta', '50 mg', 'Tableta', 'Oral'),
('MED_ENALAPRIL',    'Enalapril', 'Tableta', '10 mg', 'Tableta', 'Oral'),
('MED_METFORMINA',   'Metformina', 'Tableta', '850 mg', 'Tableta', 'Oral'),
('MED_OMEPRAZOL',    'Omeprazol', 'Capsula', '20 mg', 'Capsula', 'Oral'),
('MED_SALBUTAMOL',   'Salbutamol', 'Inhalador', '100 mcg/dosis', 'Aerosol', 'Inhalatoria'),
('MED_PREDNISONA',   'Prednisona', 'Tableta', '50 mg', 'Tableta', 'Oral'),
('MED_DICLOFENACO',  'Diclofenaco', 'Tableta', '50 mg', 'Tableta', 'Oral'),
('MED_CETIRIZINA',   'Cetirizina', 'Tableta', '10 mg', 'Tableta', 'Oral')
ON CONFLICT (code) DO UPDATE
SET name = EXCLUDED.name,
    presentation = EXCLUDED.presentation,
    concentration = EXCLUDED.concentration,
    pharmaceutical_form = EXCLUDED.pharmaceutical_form,
    route = EXCLUDED.route;

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
