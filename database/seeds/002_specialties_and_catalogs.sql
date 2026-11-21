-- ============================================================================
-- SIGM CLOUD - SEED 002 - Especialidades y catalogos base
-- ============================================================================
BEGIN;

INSERT INTO sigm.specialties (code, name, description) VALUES
('GENERAL_MEDICINE',    'Medicina general', 'Atencion medica general y medicina familiar.'),
('INTERNAL_MEDICINE',   'Medicina interna', 'Diagnostico y tratamiento de enfermedades del adulto.'),
('CARDIOLOGY',          'Cardiologia', 'Enfermedades cardiovasculares.'),
('PEDIATRICS',          'Pediatria', 'Atencion de ninos y adolescentes.'),
('OBGYN',               'Ginecologia y obstetricia', 'Salud femenina, embarazo y parto.'),
('DERMATOLOGY',         'Dermatologia', 'Enfermedades de la piel.'),
('ORTHOPEDICS',         'Ortopedia y traumatologia', 'Sistema musculoesqueletico y traumatismos.'),
('NEUROLOGY',           'Neurologia', 'Enfermedades del sistema nervioso.'),
('OPHTHALMOLOGY',       'Oftalmologia', 'Enfermedades de los ojos.'),
('PSYCHIATRY',          'Psiquiatria', 'Salud mental y trastornos psiquiatricos.'),
('PSYCHOLOGY',          'Psicologia', 'Evaluacion y acompanamiento psicologico.'),
('UROLOGY',             'Urologia', 'Enfermedades del aparato urinario y reproductor masculino.'),
('DENTISTRY',           'Odontologia', 'Salud oral y dental.'),
('NUTRITION',           'Nutricion y dietetica', 'Evaluacion y manejo nutricional.'),
('PHYSIOTHERAPY',       'Fisioterapia', 'Rehabilitacion fisica y funcional.'),
('ANESTHESIOLOGY',      'Anestesiologia', 'Anestesia y manejo del dolor.'),
('GENERAL_SURGERY',     'Cirugia general', 'Procedimientos quirurgicos generales.'),
('ENDOCRINOLOGY',       'Endocrinologia', 'Trastornos hormonales y metabolicos.'),
('GASTROENTEROLOGY',    'Gastroenterologia', 'Enfermedades del aparato digestivo.'),
('PULMONOLOGY',         'Neumologia', 'Enfermedades respiratorias.'),
('NEPHROLOGY',          'Nefrologia', 'Enfermedades renales.'),
('RHEUMATOLOGY',        'Reumatologia', 'Enfermedades autoinmunes y articulares.'),
('OTOLARYNGOLOGY',      'Otorrinolaringologia', 'Oido, nariz y garganta.'),
('FAMILY_MEDICINE',     'Medicina familiar', 'Atencion integral de la familia.'),
('SPORTS_MEDICINE',     'Medicina del deporte', 'Atencion de lesiones y rendimiento deportivo.'),
('RADIOLOGY',           'Radiologia', 'Diagnostico por imagen.'),
('PATHOLOGY',           'Patologia', 'Estudio de tejidos y laboratorio anatomopatologico.'),
('GENETICS',            'Genetica medica', 'Diagnostico y consejeria genetica.'),
('ALLERGY',             'Alergologia', 'Alergias e inmunologia clinica.'),
('INFECTIOUS_DISEASE',  'Infectologia', 'Enfermedades infecciosas.'),
('HEMATOLOGY',          'Hematologia', 'Enfermedades de la sangre.'),
('ONCOLOGY',            'Oncologia', 'Diagnostico y tratamiento del cancer.'),
('GERIATRICS',          'Geriatria', 'Salud del adulto mayor.'),
('OCCUPATIONAL_MEDICINE','Medicina laboral', 'Salud ocupacional y medicina del trabajo.'),
('OPTOMETRY',           'Optometria', 'Evaluacion visual y optica.'),
('SPEECH_THERAPY',      'Fonoaudiologia', 'Comunicacion, voz y audicion.'),
('OCCUPATIONAL_THERAPY','Terapia ocupacional', 'Rehabilitacion de actividades cotidianas.'),
('NURSING',             'Enfermeria', 'Cuidado y procedimientos de enfermeria.'),
('OBSTETRICS',          'Obstetricia', 'Control prenatal y atencion del parto.'),
('OTHER',               'Otra especialidad', 'Especialidad configurable por el cliente.')
ON CONFLICT (code) DO UPDATE
SET name = EXCLUDED.name, description = EXCLUDED.description;

INSERT INTO sigm.document_types (code, name) VALUES
('CC',  'Cedula de ciudadania'),
('CE',  'Cedula de extranjeria'),
('TI',  'Tarjeta de identidad'),
('RC',  'Registro civil'),
('PA',  'Pasaporte'),
('NIT', 'Numero de identificacion tributaria'),
('MS',  'Menor sin identificacion'),
('AS',  'Adulto sin identificacion'),
('PE',  'Permiso especial de permanencia'),
('PPT', 'Permiso por proteccion temporal'),
('NUIP','Numero unico de identificacion personal'),
('OTHER','Otro tipo de documento')
ON CONFLICT (code) DO UPDATE SET name = EXCLUDED.name;

INSERT INTO sigm.clinical_note_types (code, name, description) VALUES
('CONSULTATION',      'Consulta', 'Nota de consulta medica.'),
('EVOLUTION',         'Evolucion', 'Evolucion clinica del paciente.'),
('ADMISSION',         'Ingreso', 'Nota de ingreso hospitalario.'),
('DISCHARGE',         'Egreso', 'Resumen de egreso.'),
('PROCEDURE',         'Procedimiento', 'Nota de procedimiento.'),
('EMERGENCY',         'Urgencias', 'Nota de atencion de urgencias.'),
('TELEMEDICINE',      'Telemedicina', 'Atencion remota.'),
('NURSING',           'Enfermeria', 'Nota de enfermeria.'),
('INTERCONSULTATION', 'Interconsulta', 'Respuesta a interconsulta.'),
('OTHER',             'Otra nota', 'Nota configurable por el cliente.')
ON CONFLICT (code) DO UPDATE
SET name = EXCLUDED.name, description = EXCLUDED.description;

INSERT INTO sigm.document_categories (tenant_id, code, name, description, is_system) VALUES
(NULL, 'IDENTITY',          'Documento de identidad', 'Documentos de identificacion del paciente.', true),
(NULL, 'CONSENT',           'Consentimiento informado', 'Consentimientos firmados.', true),
(NULL, 'LAB_RESULT',        'Resultado de laboratorio', 'Resultados de examenes.', true),
(NULL, 'IMAGING',           'Imagen diagnostica', 'Radiografias, ecografias, resonancias, etc.', true),
(NULL, 'PRESCRIPTION',      'Formula medica', 'Formulas y prescripciones.', true),
(NULL, 'CERTIFICATE',       'Certificado medico', 'Certificados y constancias.', true),
(NULL, 'DISCHARGE_SUMMARY', 'Resumen de egreso', 'Documentos de egreso.', true),
(NULL, 'INVOICE',           'Factura', 'Documentos financieros.', true),
(NULL, 'RIPS',              'RIPS', 'Reportes de prestacion de servicios.', true),
(NULL, 'OTHER',             'Otro documento', 'Documentos configurables.', true)
ON CONFLICT (code) WHERE tenant_id IS NULL DO UPDATE
SET name = EXCLUDED.name, description = EXCLUDED.description;

INSERT INTO sigm.insurers (code, name, nit, insurer_type) VALUES
('EPS_SURA',      'EPS Sura', '890903937', 'EPS'),
('NUEVA_EPS',     'Nueva EPS', '900156264', 'EPS'),
('SANITAS',       'EPS Sanitas', '800251440', 'EPS'),
('COMPENSAR',     'EPS Compensar', '860066942', 'EPS'),
('FAMISANAR',     'EPS Famisanar', '830003564', 'EPS'),
('SALUD_TOTAL',   'EPS Salud Total', '800130907', 'EPS'),
('COOMEVA',       'EPS Coomeva', '890908481', 'EPS'),
('CAPITAL_SALUD', 'EPS Capital Salud', '830114958', 'EPS'),
('MUTUAL_SER',    'Mutual Ser EPS', '900226715', 'EPS'),
('SOS',           'SOS EPS', '900546463', 'EPS'),
('ARL_SURA',      'ARL Sura', '890903937', 'ARL'),
('OTHER',         'Otra aseguradora', NULL, 'OTHER')
ON CONFLICT (code) DO UPDATE
SET name = EXCLUDED.name, nit = EXCLUDED.nit, insurer_type = EXCLUDED.insurer_type;

COMMIT;
