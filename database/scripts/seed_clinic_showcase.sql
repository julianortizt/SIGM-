\set ON_ERROR_STOP on
\pset format unaligned

DO $$
DECLARE
  v_admin uuid; v_doc uuid; v_rec uuid;
  v_tenant uuid;
  v_pats uuid[];
  v_site uuid; v_office uuid;
  v_prov uuid;
  v_fam uuid; v_cardi uuid; v_derm uuid; v_gastro uuid; v_endo uuid; v_dent uuid;
  v_at_cons uuid;
  v_ins_sura uuid; v_ins_nueva uuid;
  v_service_cons1 uuid; v_service_cons2 uuid; v_service_lab1 uuid; v_service_lab2 uuid;
  v_contract uuid;
  v_doc_cat_other uuid; v_doc_cat_pres uuid;
  v_str_today text; v_str_tm1 text; v_str_tm2 text; v_str_tm3 text; v_str_tm4 text;
  v_tomorrow text; v_day_after text;
  v_enc uuid; v_note uuid; v_pat uuid;
  v_appt_id uuid;
BEGIN
  -- lookups del tenant/clinico
  SELECT id INTO v_admin FROM sigm.users WHERE lower(email)='admin@demo.sigm.local' AND is_active LIMIT 1;
  SELECT id INTO v_doc FROM sigm.users WHERE lower(email)='doctor@demo.sigm.local' AND is_active LIMIT 1;
  SELECT id INTO v_rec FROM sigm.users WHERE lower(email)='recepcion@demo.sigm.local' AND is_active LIMIT 1;
  IF v_admin IS NULL OR v_doc IS NULL OR v_rec IS NULL THEN
    RAISE EXCEPTION 'Credenciales demo no encontradas: admin/doc/rec';
  END IF;
  SELECT id INTO v_tenant FROM sigm.tenants WHERE tenant_code='CLINIC-DEMO' LIMIT 1;
  IF v_tenant IS NULL THEN RAISE EXCEPTION 'Tenant CLINIC-DEMO no encontrado'; END IF;

  -- pacientes demo existentes
  SELECT array_agg(id ORDER BY patient_number)
  INTO v_pats
  FROM sigm.patients
  WHERE tenant_id=v_tenant
    AND id IN ('eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee1'::uuid,'eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee2'::uuid,
               'eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee4'::uuid,'eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee5'::uuid,
               'eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee6'::uuid,'eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee7'::uuid,
               'eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee8'::uuid,'eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee9'::uuid);
  IF v_pats IS NULL OR array_length(v_pats,1)<8 THEN
    RAISE EXCEPTION 'Pacientes demo insuficientes (count=%)', COALESCE(array_length(v_pats,1),0);
  END IF;

  -- recursos clinics
  SELECT id INTO v_prov FROM sigm.professionals WHERE id='dddddddd-dddd-4ddd-8ddd-dddddddddddd'::uuid LIMIT 1;
  SELECT id INTO v_site FROM sigm.sites WHERE id='22222222-2222-4222-8222-222222222222'::uuid LIMIT 1;
  SELECT id INTO v_office FROM sigm.medical_offices WHERE id='22222222-2222-4222-8222-333333333333'::uuid LIMIT 1;
  IF v_prov IS NULL OR v_site IS NULL OR v_office IS NULL THEN
    RAISE EXCEPTION 'Recursos clinics default no encontrados: professional/site/office';
  END IF;

  -- especialidades del tenant
  SELECT id INTO v_fam   FROM sigm.specialties WHERE code='FAMILY_MEDICINE' LIMIT 1;
  SELECT id INTO v_cardi FROM sigm.specialties WHERE code='CARDIOLOGY' LIMIT 1;
  SELECT id INTO v_derm  FROM sigm.specialties WHERE code='DERMATOLOGY' LIMIT 1;
  SELECT id INTO v_gastro FROM sigm.specialties WHERE code='GASTROENTEROLOGY' LIMIT 1;
  SELECT id INTO v_endo  FROM sigm.specialties WHERE code='ENDOCRINOLOGY' LIMIT 1;
  SELECT id INTO v_dent  FROM sigm.specialties WHERE code='DENTISTRY' LIMIT 1;

  FOR v_pat IN (SELECT unnest(ARRAY[v_fam::uuid, v_cardi::uuid, v_derm::uuid, v_gastro::uuid, v_endo::uuid, v_dent::uuid]))
  LOOP
    INSERT INTO sigm.tenant_specialties (tenant_id, specialty_id, is_active, created_at, updated_at)
    VALUES (v_tenant, v_pat, true, now(), now())
    ON CONFLICT (tenant_id, specialty_id) DO UPDATE SET is_active=true, updated_at=now();
  END LOOP;
  INSERT INTO sigm.professional_specialties (tenant_id, professional_id, specialty_id, is_active, created_at, updated_at)
  SELECT v_tenant, v_prov, unnest(ARRAY[v_fam::uuid, v_cardi::uuid, v_derm::uuid, v_gastro::uuid, v_endo::uuid, v_dent::uuid]), true, now(), now()
  ON CONFLICT (tenant_id, professional_id, specialty_id) DO NOTHING;

  -- appointment tipo y aseguradoras
  SELECT id INTO v_at_cons FROM sigm.appointment_types WHERE tenant_id=v_tenant AND code='CONS-GEN' LIMIT 1;
  SELECT id INTO v_ins_sura FROM sigm.insurers WHERE lower(name) LIKE '%sura%' AND insurer_type='EPS' LIMIT 1;
  SELECT id INTO v_ins_nueva FROM sigm.insurers WHERE lower(name) LIKE '%nueva eps%' AND insurer_type='EPS' LIMIT 1;

  -- service catalog
  SELECT id INTO v_service_cons1 FROM sigm.service_catalog WHERE tenant_id=v_tenant AND code='CONSULTA_01' LIMIT 1;
  SELECT id INTO v_service_cons2 FROM sigm.service_catalog WHERE tenant_id=v_tenant AND code='CONSULTA_02' LIMIT 1;
  SELECT id INTO v_service_lab1 FROM sigm.service_catalog WHERE tenant_id=v_tenant AND code='LAB_HEMOGRAMA' LIMIT 1;
  SELECT id INTO v_service_lab2 FROM sigm.service_catalog WHERE tenant_id=v_tenant AND code='LAB_GLUCOSA' LIMIT 1;

  -- contrato con Sura
  INSERT INTO sigm.contracts (tenant_id, insurer_id, contract_number, name, billing_type, valid_from, valid_to, status, notes, created_at, updated_at)
  SELECT v_tenant, v_ins_sura, 'CC-2026-SURA-FEE-DEMO', 'Contrato EPS Sura 2026 (honorarios) Demo', 'FEE_FOR_SERVICE', now()::date, (now() + '3 years'::interval)::date, 'ACTIVE', 'Contrato vigente EPS Sura Demo', now(), now()
  ON CONFLICT (tenant_id, contract_number) DO NOTHING;
  SELECT id INTO v_contract FROM sigm.contracts WHERE tenant_id=v_tenant AND insurer_id=v_ins_sura AND billing_type='FEE_FOR_SERVICE' ORDER BY id DESC LIMIT 1;
  IF v_contract IS NULL THEN
    RAISE EXCEPTION 'Contrato Sura no creado';
  END IF;
  INSERT INTO sigm.contract_services (tenant_id, contract_id, service_id, agreed_price, copay_percent, requires_authorization, created_at, updated_at)
  SELECT v_tenant, v_contract, v_service_cons1, 80000, 0, false, now(), now()
  ON CONFLICT (tenant_id, contract_id, service_id) DO NOTHING;
  INSERT INTO sigm.contract_services (tenant_id, contract_id, service_id, agreed_price, copay_percent, requires_authorization, created_at, updated_at)
  SELECT v_tenant, v_contract, v_service_cons2, 120000, 0, false, now(), now()
  ON CONFLICT (tenant_id, contract_id, service_id) DO NOTHING;

  -- document categories / types
  SELECT id INTO v_doc_cat_other FROM sigm.document_categories WHERE code='OTHER' LIMIT 1;
  SELECT id INTO v_doc_cat_pres  FROM sigm.document_categories WHERE code='PRESCRIPTION' LIMIT 1;

  -- fechas
  v_str_today  := now()::date::text;
  v_str_tm1    := (now() - '4 days'::interval)::date::text;
  v_str_tm2    := (now() - '3 days'::interval)::date::text;
  v_str_tm3    := (now() - '2 days'::interval)::date::text;
  v_str_tm4    := (now() - '1 days'::interval)::date::text;
  v_tomorrow   := (now() + '1 days'::interval)::date::text;
  v_day_after  := (now() + '2 days'::interval)::date::text;

  -- ================= CITAS =================
  INSERT INTO sigm.appointments (tenant_id, patient_id, professional_id, specialty_id, site_id, medical_office_id,
    appointment_type_id, appointment_number, starts_at, ends_at, status, reason, notes, booked_by, confirmed_at, created_at, updated_at)
  SELECT v_tenant, v_pats[3], v_prov, v_fam, v_site, v_office, v_at_cons,
    'APT-' || substr(to_char(now(),'YYYYMMDDHH24MISS'),1,50),
    (v_str_tm1||' 08:30')::timestamptz, (v_str_tm1||' 09:00')::timestamptz,
    'COMPLETED', 'Control medico de enfermedad cronica. Dosificacion de medicamentos.',
    'Paciente refiere control rutinario de diabetes tipo 2. Sin quejas new.',
    v_rec, now() - '2 hours'::interval, now(), now()
  ON CONFLICT (tenant_id, id) DO NOTHING;

  INSERT INTO sigm.appointments (tenant_id, patient_id, professional_id, specialty_id, site_id, medical_office_id,
    appointment_type_id, appointment_number, starts_at, ends_at, status, reason, notes, booked_by, confirmed_at, created_at, updated_at)
  SELECT v_tenant, v_pats[4], v_prov, v_fam, v_site, v_office, v_at_cons,
    'APT-' || substr(to_char(now(),'YYYYMMDDHH24MISS'),1,50),
    (v_str_tm2||' 10:00')::timestamptz, (v_str_tm2||' 10:30')::timestamptz,
    'COMPLETED', 'Dolor abdominal recurrente y diarrea.',
    'Paciente con dolor abdominal que irradia a region epigastrica. Sin fiebre. Solicitar laboratorio basico.',
    v_rec, now() - '3 hours'::interval, now(), now()
  ON CONFLICT (tenant_id, id) DO NOTHING;

  INSERT INTO sigm.appointments (tenant_id, patient_id, professional_id, specialty_id, site_id, medical_office_id,
    appointment_type_id, appointment_number, starts_at, ends_at, status, reason, notes, booked_by, confirmed_at, created_at, updated_at)
  SELECT v_tenant, v_pats[5], v_prov, v_fam, v_site, v_office, v_at_cons,
    'APT-' || substr(to_char(now(),'YYYYMMDDHH24MISS'),1,50),
    (v_str_today||' 11:00')::timestamptz, (v_str_today||' 11:30')::timestamptz,
    'IN_PROGRESS', 'Control de peso y presion arterial. Preguntar sobre cansancio y cefalea.',
    'Control mensual de salud general. Paciente refiere cansancio y algo de dolor de cabeza.',
    v_rec, now(), now(), now()
  ON CONFLICT (tenant_id, id) DO NOTHING;

  INSERT INTO sigm.appointments (tenant_id, patient_id, professional_id, specialty_id, site_id, medical_office_id,
    appointment_type_id, appointment_number, starts_at, ends_at, status, reason, notes, booked_by, confirmed_at, created_at, updated_at)
  SELECT v_tenant, v_pats[6], v_prov, v_fam, v_site, v_office, v_at_cons,
    'APT-' || substr(to_char(now(),'YYYYMMDDHH24MISS'),1,50),
    (v_str_today||' 13:00')::timestamptz, (v_str_today||' 13:30')::timestamptz,
    'WAITING', 'Antecedentes de hipertension y cardiopatia isquemica. Chequeo cardiovascular.',
    'Control cardiovascular peridiemal.',
    v_rec, now(), now(), now()
  ON CONFLICT (tenant_id, id) DO NOTHING;

  INSERT INTO sigm.appointments (tenant_id, patient_id, professional_id, specialty_id, site_id, medical_office_id,
    appointment_type_id, appointment_number, starts_at, ends_at, status, reason, notes, booked_by, confirmed_at, created_at, updated_at)
  SELECT v_tenant, v_pats[7], v_prov, v_fam, v_site, v_office, v_at_cons,
    'APT-' || substr(to_char(now(),'YYYYMMDDHH24MISS'),1,50),
    (v_tomorrow||' 09:00')::timestamptz, (v_tomorrow||' 09:30')::timestamptz,
    'CONFIRMED', 'Amigdalitis recurrente. Preguntar sobre fiebre y farmacos.',
    'Historial de amigdalitis recurrente con necesidad de valoracion para amigtalectomia eventual.',
    v_rec, now(), now(), now()
  ON CONFLICT (tenant_id, id) DO NOTHING;

  INSERT INTO sigm.appointments (tenant_id, patient_id, professional_id, specialty_id, site_id, medical_office_id,
    appointment_type_id, appointment_number, starts_at, ends_at, status, reason, notes, booked_by, confirmed_at, created_at, updated_at)
  SELECT v_tenant, v_pats[8], v_prov, v_fam, v_site, v_office, v_at_cons,
    'APT-' || substr(to_char(now(),'YYYYMMDDHH24MISS'),1,50),
    (v_day_after||' 14:00')::timestamptz, (v_day_after||' 14:30')::timestamptz,
    'CONFIRMED', 'Revision de resultados de laboratorio. Control de glucosa y colesterol.',
    'Paciente adulto joven con factores de riesgo metabolicos. Lab solicitado pendiente.',
    v_rec, now(), now(), now()
  ON CONFLICT (tenant_id, id) DO NOTHING;

  INSERT INTO sigm.appointments (tenant_id, patient_id, professional_id, specialty_id, site_id, medical_office_id,
    appointment_type_id, appointment_number, starts_at, ends_at, status, reason, notes, booked_by, confirmed_at, cancelled_at, cancellation_reason, created_at, updated_at)
  SELECT v_tenant, v_pats[1], v_prov, v_fam, v_site, v_office, v_at_cons,
    'APT-' || substr(to_char(now(),'FFFFFFMMDDHH24MISS'),1,50),
    (v_str_tm4||' 15:00')::timestamptz, (v_str_tm4||' 15:30')::timestamptz,
    'CANCELLED',    'Cita de rutina mensual.',
    'Cancelada por el paciente por imposibilidad de asistencia.',
    v_rec, now() - '1 days'::interval, now() - '1 days'::interval,  'Paciente solicito reschedule por causa personal.', now(), now()
  ON CONFLICT (tenant_id, id) DO NOTHING;

  INSERT INTO sigm.appointments (tenant_id, patient_id, professional_id, specialty_id, site_id, medical_office_id,
    appointment_type_id, appointment_number, starts_at, ends_at, status, reason, notes, booked_by, confirmed_at, created_at, updated_at)
  SELECT v_tenant, v_pats[2], v_prov, v_fam, v_site, v_office, v_at_cons,
    'APT-' || substr(to_char(now(),'YYYYMMDDHH24MISS'),1,50) || '-' || substr(v_pats[2]::text,1,20),
    (v_str_tm3||' 16:00')::timestamptz, (v_str_tm3||' 16:30')::timestamptz,
    'NO_SHOW', 'Cita de seguimiento post-operatorio leve.',
    'Paciente no presento. Se notifico por WhatsApp. Reagendar proximamente.',
    v_rec, now() - '1 days'::interval, now(), now()
  ON CONFLICT (tenant_id, id) DO NOTHING;

  -- ================= ENCOUNTERS =================
  INSERT INTO sigm.encounters (tenant_id, patient_id, professional_id, specialty_id, site_id, medical_office_id,
    appointment_id, encounter_type, encounter_date, started_at, ended_at, chief_complaint, status, created_by, created_at, updated_at)
  SELECT v_tenant, v_pats[3], v_prov, v_fam, v_site, v_office,
    (SELECT id FROM sigm.appointments WHERE tenant_id=v_tenant AND patient_id=v_pats[3] AND status='ATTENDED' ORDER BY id DESC LIMIT 1),
    'OUTPATIENT', v_str_tm1::date, (v_str_tm1||' 09:00')::timestamptz, (v_str_tm1||' 09:40')::timestamptz,
    'Dolor de cabeza y controles de diabetes. Paciente estable.', 'ATTENDED', v_doc, now(), now()
  ON CONFLICT (tenant_id, id) DO NOTHING;

  INSERT INTO sigm.encounters (tenant_id, patient_id, professional_id, specialty_id, site_id, medical_office_id,
    appointment_id, encounter_type, encounter_date, started_at, ended_at, chief_complaint, status, created_by, created_at, updated_at)
  SELECT v_tenant, v_pats[4], v_prov, v_gastro, v_site, v_office,
    (SELECT id FROM sigm.appointments WHERE tenant_id=v_tenant AND patient_id=v_pats[4] AND status='ATTENDED' ORDER BY id DESC LIMIT 1),
    'OUTPATIENT', v_str_tm2::date, (v_str_tm2||' 10:30')::timestamptz, (v_str_tm2||' 11:15')::timestamptz,
    'Dolor abdominal recurrente, diarrea y perdida de peso leve. Valorar laboratorio.', 'ATTENDED', v_doc, now(), now()
  ON CONFLICT (tenant_id, id) DO NOTHING;

  INSERT INTO sigm.encounters (tenant_id, patient_id, professional_id, specialty_id, site_id, medical_office_id,
    appointment_id, encounter_type, encounter_date, started_at, ended_at, chief_complaint, status, created_by, created_at, updated_at)
  SELECT v_tenant, v_pats[5], v_prov, v_fam, v_site, v_office,
    (SELECT id FROM sigm.appointments WHERE tenant_id=v_tenant AND patient_id=v_pats[5] AND status='IN_PROGRESS' ORDER BY id DESC LIMIT 1),
    'OUTPATIENT', v_str_today::date, (v_str_today||' 11:05')::timestamptz, NULL,
    'Control de salud general, cansancio y cefalea leve.', 'IN_PROGRESS', v_doc, now(), now()
  ON CONFLICT (tenant_id, id) DO NOTHING;

  INSERT INTO sigm.encounters (tenant_id, patient_id, professional_id, specialty_id, site_id, medical_office_id,
    appointment_id, encounter_type, encounter_date, started_at, ended_at, chief_complaint, status, created_by, created_at, updated_at)
  SELECT v_tenant, v_pats[6], v_prov, v_cardi, v_site, v_office,
    (SELECT id FROM sigm.appointments WHERE tenant_id=v_tenant AND patient_id=v_pats[6] AND status='WAITING' ORDER BY id DESC LIMIT 1),
    'OUTPATIENT', v_str_today::date, NULL, NULL,
    'Control cardiovascular. Paciente llego y espera.', 'WAITING', v_doc, now(), now()
  ON CONFLICT (tenant_id, id) DO NOTHING;

  INSERT INTO sigm.encounters (tenant_id, patient_id, professional_id, specialty_id, site_id, medical_office_id,
    appointment_id, encounter_type, encounter_date, started_at, ended_at, chief_complaint, status, created_by, created_at, updated_at)
  SELECT v_tenant, v_pats[7], v_prov, v_fam, v_site, v_office,
    (SELECT id FROM sigm.appointments WHERE tenant_id=v_tenant AND patient_id=v_pats[7] AND status='CONFIRMED' ORDER BY id DESC LIMIT 1),
    'OUTPATIENT', v_tomorrow::date, NULL, NULL,
    'Amigdalitis recurrente. Valorar manejo medico y posible amigtalectomia.', 'SCHEDULED', v_rec, now(), now()
  ON CONFLICT (tenant_id, id) DO NOTHING;

  INSERT INTO sigm.encounters (tenant_id, patient_id, professional_id, specialty_id, site_id, medical_office_id,
    appointment_id, encounter_type, encounter_date, started_at, ended_at, chief_complaint, status, created_by, created_at, updated_at)
  SELECT v_tenant, v_pats[8], v_prov, v_endo, v_site, v_office,
    (SELECT id FROM sigm.appointments WHERE tenant_id=v_tenant AND patient_id=v_pats[8] AND status='ATTENDED' ORDER BY id DESC LIMIT 1),
    'OUTPATIENT', v_str_tm2::date, (v_str_tm2||' 14:30')::timestamptz, (v_str_tm2||' 15:05')::timestamptz,
    'Revision de laboratorio metabolico y control de glucosa.', 'ATTENDED', v_doc, now(), now()
  ON CONFLICT (tenant_id, id) DO NOTHING;

  -- ================= NOTAS CLINICAS =================
  INSERT INTO sigm.clinical_notes (tenant_id, encounter_id, patient_id, author_user_id, note_type, status, current_version, signed_at, signed_by, created_at, updated_at)
  SELECT v_tenant,
    (SELECT id FROM sigm.encounters WHERE tenant_id=v_tenant AND patient_id=v_pats[3] AND status='ATTENDED' ORDER BY id DESC LIMIT 1),
    v_pats[3], v_doc, 'EVOLUTION', 'SIGNED', 1, now(), v_doc, now(), now()
  ON CONFLICT (tenant_id, id) DO NOTHING;

  INSERT INTO sigm.clinical_note_versions (tenant_id, note_id, version_number, content, author_user_id, signed_at, signature_hash, created_at)
  SELECT v_tenant,
    (SELECT id FROM sigm.clinical_notes WHERE tenant_id=v_tenant AND patient_id=v_pats[3] AND note_type='EVOLUTION' ORDER BY id DESC LIMIT 1),
    1,
    E'\nEVOLUTION NOTE\n' ||
    E'Signa examen general: paciente alerta, orientado, sin vitreos.\n' ||
    E'Evaluation: control de diabetes tipo 2 bajo tratamiento. PA estable 128/76. Glucometria capilar 138 mg/dL.\n' ||
    E'Plan: mantener actualidad de tratamiento, renovar formulacion de metformina al proximo mes, solicitar hemograma y perfil lipidico proximamente. Paciente educado sobre dieta.',
    v_doc, now(), md5('signed-1'), now()
  ON CONFLICT (tenant_id, note_id, version_number) DO NOTHING;

  INSERT INTO sigm.clinical_notes (tenant_id, encounter_id, patient_id, author_user_id, note_type, status, current_version, signed_at, signed_by, created_at, updated_at)
  SELECT v_tenant,
    (SELECT id FROM sigm.encounters WHERE tenant_id=v_tenant AND patient_id=v_pats[4] AND status='ATTENDED' ORDER BY id DESC LIMIT 1),
    v_pats[4], v_doc, 'CONSULTATION', 'SIGNED', 1, now(), v_doc, now(), now()
  ON CONFLICT (tenant_id, id) DO NOTHING;

  INSERT INTO sigm.clinical_note_versions (tenant_id, note_id, version_number, content, author_user_id, signed_at, signature_hash, created_at)
  SELECT v_tenant,
    (SELECT id FROM sigm.clinical_notes WHERE tenant_id=v_tenant AND patient_id=v_pats[4] AND note_type='CONSULTATION' ORDER BY id DESC LIMIT 1),
    1,
    E'\nCONSULTATION NOTE\n' ||
    E'History: dolor abdominal recurrente epigastrico, asociado a comidas. Diarrea intermitente (2-3 episodios/dia). Perdida de peso no intencional leve (2 kg en mes). Sin fiebre. Sin emesis.\n' ||
    E'Exploration: abdomen distendido levemente, dolor a palpacacion en epigastrio, sin rebote, sin defensa. Resto dean normal.\n' ||
    E'Assessment: sospecha de gastropathy relacionada con NSAID e infeccion gastrointestinal leve.\n' ||
    E'Plan: dieta blanda y probiotico, suspension de NSAID si aplica. Reagenda con resultados de laboratorio.',
    v_doc, now(), md5('signed-1'), now()
  ON CONFLICT (tenant_id, note_id, version_number) DO NOTHING;

  INSERT INTO sigm.clinical_notes (tenant_id, encounter_id, patient_id, author_user_id, note_type, status, current_version, signed_at, signed_by, created_at, updated_at)
  SELECT v_tenant,
    (SELECT id FROM sigm.encounters WHERE tenant_id=v_tenant AND patient_id=v_pats[5] AND status='IN_PROGRESS' ORDER BY id DESC LIMIT 1),
    v_pats[5], v_doc, 'CONSULTATION', 'DRAFT', 0, NULL, NULL, now(), now()
  ON CONFLICT (tenant_id, id) DO NOTHING;

  INSERT INTO sigm.clinical_note_versions (tenant_id, note_id, version_number, content, author_user_id, signed_at, signature_hash, created_at)
  SELECT v_tenant,
    (SELECT id FROM sigm.clinical_notes WHERE tenant_id=v_tenant AND patient_id=v_pats[5] AND note_type='CONSULTATION' ORDER BY id DESC LIMIT 1),
    0,
    E'\nBORRADOR - aun no firmado.\n' ||
    E'Recogimiento inicial del paciente. Cansancio, cefalea leve. Se solicitan signos vitales y laboratorio basal.',
    v_doc, NULL, NULL, now()
  ON CONFLICT (tenant_id, note_id, version_number) DO NOTHING;

  INSERT INTO sigm.clinical_notes (tenant_id, encounter_id, patient_id, author_user_id, note_type, status, current_version, signed_at, signed_by, created_at, updated_at)
  SELECT v_tenant,
    (SELECT id FROM sigm.encounters WHERE tenant_id=v_tenant AND patient_id=v_pats[4] AND status='ATTENDED' ORDER BY id DESC LIMIT 1),
    v_pats[4], v_rec, 'NURSING', 'SIGNED', 1, now(), v_rec, now(), now()
  ON CONFLICT (tenant_id, id) DO NOTHING;

  INSERT INTO sigm.clinical_note_versions (tenant_id, note_id, version_number, content, author_user_id, signed_at, signature_hash, created_at)
  SELECT v_tenant,
    (SELECT id FROM sigm.clinical_notes WHERE tenant_id=v_tenant AND patient_id=v_pats[4] AND note_type='NURSING' ORDER BY id DESC LIMIT 1),
    1,
    E'\nNURSING NOTE\n' || E'Paciente admitido, signos estables al ingreso. Control de signos vitales cada hora.\n' ||
    E'Evolucion: sin cambios. Recomendar hidratacion adecuada y dieta suave.',
    v_rec, now(), md5('signed-1'), now()
  ON CONFLICT (tenant_id, note_id, version_number) DO NOTHING;

  INSERT INTO sigm.clinical_notes (tenant_id, encounter_id, patient_id, author_user_id, note_type, status, current_version, signed_at, signed_by, created_at, updated_at)
  SELECT v_tenant,
    (SELECT id FROM sigm.encounters WHERE tenant_id=v_tenant AND patient_id=v_pats[8] AND status='ATTENDED' ORDER BY id DESC LIMIT 1),
    v_pats[8], v_doc, 'DISCHARGE', 'SIGNED', 1, now(), v_doc, now(), now()
  ON CONFLICT (tenant_id, id) DO NOTHING;

  INSERT INTO sigm.clinical_note_versions (tenant_id, note_id, version_number, content, author_user_id, signed_at, signature_hash, created_at)
  SELECT v_tenant,
    (SELECT id FROM sigm.clinical_notes WHERE tenant_id=v_tenant AND patient_id=v_pats[8] AND note_type='DISCHARGE' ORDER BY id DESC LIMIT 1),
    1,
    E'\nDISCHARGE NOTE\n' ||
    E'At conclusion of visit: patient stable, no acute events. Lab results reviewed. Continued outpatient follow-up recommended.',
    v_doc, now(), md5('signed-1'), now()
  ON CONFLICT (tenant_id, note_id, version_number) DO NOTHING;

  -- ================= ALERGIAS =================
  INSERT INTO sigm.allergies (tenant_id, patient_id, allergen, allergen_type, reaction, severity, onset_date, status, recorded_by, created_at, updated_at)
  SELECT v_tenant, v_pats[1], 'Penicilina', 'MEDICATION', 'Rash cutaneo e hinchazon leve', 'HIGH', NULL, 'ACTIVE', v_doc, now(), now()
  ON CONFLICT (tenant_id, id) DO NOTHING;
  INSERT INTO sigm.allergies (tenant_id, patient_id, allergen, allergen_type, reaction, severity, onset_date, status, recorded_by, created_at, updated_at)
  SELECT v_tenant, v_pats[2], 'Sulfas', 'MEDICATION', 'Nauseas y mareo', 'MEDIUM', NULL, 'ACTIVE', v_doc, now(), now()
  ON CONFLICT (tenant_id, id) DO NOTHING;
  INSERT INTO sigm.allergies (tenant_id, patient_id, allergen, allergen_type, reaction, severity, onset_date, status, recorded_by, created_at, updated_at)
  SELECT v_tenant, v_pats[3], 'Eritromicina', 'MEDICATION', 'Dolor abdominal', 'LOW', NULL, 'ACTIVE', v_doc, now(), now()
  ON CONFLICT (tenant_id, id) DO NOTHING;
  INSERT INTO sigm.allergies (tenant_id, patient_id, allergen, allergen_type, reaction, severity, onset_date, status, recorded_by, created_at, updated_at)
  SELECT v_tenant, v_pats[4], 'Lacteos', 'FOOD', 'Hinchazon abdominal y diarrea leve', 'MEDIUM', NULL, 'ACTIVE', v_doc, now(), now()
  ON CONFLICT (tenant_id, id) DO NOTHING;
  INSERT INTO sigm.allergies (tenant_id, patient_id, allergen, allergen_type, reaction, severity, onset_date, status, recorded_by, created_at, updated_at)
  SELECT v_tenant, v_pats[5], 'Polen', 'ENVIRONMENTAL', 'Estornudos, conjuntivitis leve y tos seca', 'LOW', NULL, 'ACTIVE', v_rec, now(), now()
  ON CONFLICT (tenant_id, id) DO NOTHING;
  INSERT INTO sigm.allergies (tenant_id, patient_id, allergen, allergen_type, reaction, severity, onset_date, status, recorded_by, created_at, updated_at)
  SELECT v_tenant, v_pats[6], 'Cognac', 'MEDICATION', 'Reaccion moderada', 'HIGH', NULL, 'ACTIVE', v_doc, now(), now()
  ON CONFLICT (tenant_id, id) DO NOTHING;
  INSERT INTO sigm.allergies (tenant_id, patient_id, allergen, allergen_type, reaction, severity, onset_date, status, recorded_by, created_at, updated_at)
  SELECT v_tenant, v_pats[7], 'Acaros del polvo', 'ENVIRONMENTAL', 'Rinitis alergica y estornudos', 'MEDIUM', NULL, 'ACTIVE', v_rec, now(), now()
  ON CONFLICT (tenant_id, id) DO NOTHING;
  INSERT INTO sigm.allergies (tenant_id, patient_id, allergen, allergen_type, reaction, severity, onset_date, status, recorded_by, created_at, updated_at)
  SELECT v_tenant, v_pats[8], 'Mariscos', 'FOOD', 'Sarpullido y picazu leve', 'LOW', NULL, 'INACTIVE', v_doc, now(), now()
  ON CONFLICT (tenant_id, id) DO NOTHING;
END $$;