-- SIGM Cloud - Seed de datos ficticios completo para entorno de desarrollo/demo
-- Credenciales:
--   admin@demo.sigm.local     / Demo12345!  (CLINIC_ADMIN)
--   doctor@demo.sigm.local    / Doctor123!  (PHYSICIAN)
--   recepcion@demo.sigm.local / Recep123!   (RECEPTIONIST)
-- Tenant: CLINIC-DEMO (11111111-1111-4111-8111-111111111111)
-- Ejecutar: psql -v ON_ERROR_STOP=1 -d sigm_cloud_dev -f database/scripts/seed_demo_full.sql

\set ON_ERROR_STOP on
BEGIN;

DO $$
DECLARE
  v_tid uuid := '11111111-1111-4111-8111-111111111111';
  v_org uuid := '11111111-1111-4111-8111-222222222222';
  v_site uuid := '22222222-2222-4222-8222-222222222222';
  v_office uuid := '22222222-2222-4222-8222-333333333333';
  v_dept uuid := '22222222-2222-4222-8222-444444444444';
  v_admin uuid := 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
  v_doc uuid := 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb';
  v_rec uuid := 'cccccccc-cccc-4ccc-8ccc-cccccccccccc';
  v_pro uuid := 'dddddddd-dddd-4ddd-8ddd-dddddddddddd';
  v_pat1 uuid := 'eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee1';
  v_pat2 uuid := 'eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee2';
  v_appt uuid := 'f1111111-1111-4111-8111-111111111111';
  v_enc uuid := 'f2222222-2222-4222-8222-222222222222';
  v_note uuid := 'f3333333-3333-4333-8333-333333333333';
  v_sched uuid := 'f4444444-4444-4444-8444-444444444444';
  v_type uuid := 'f5555555-5555-4555-8555-555555555555';
  v_rule uuid := 'f6666666-6666-4666-8666-666666666666';
  v_role_admin uuid; v_role_doc uuid; v_role_rec uuid; v_spec uuid; v_spec2 uuid; v_plan uuid;
  v_hash_admin text := 'v1.100000.ykoAsMm6PxRelpXpRNE3lA==.r4ONpHkULraX4CRnVWqJYgXNIzPLna/Lv26CSrCESiE=';
  v_hash_doc text := 'v1.100000.LVrpwdfZAd1Y18yFxYz+0Q==.kiKz1DnaHZKH4RWALipxPRB3as035Q+OZifG+ToeJI8=';
  v_hash_rec text := 'v1.100000.yCGrABn5u49+W2Skj74vqQ==.LRYmeot7sLza/BMMqxrGbIFyo3aSHvhlcXAwlvU1Tr8=';
BEGIN
  IF EXISTS (SELECT 1 FROM sigm.tenants WHERE id = v_tid) THEN
    RAISE NOTICE 'Demo CLINIC-DEMO ya existe (%), omitiendo.', v_tid;
    RETURN;
  END IF;

  -- Rol admin: CLINIC_ADMIN es el rol de administrador de tenant en el catálogo.
  -- Nunca omitir silenciosamente: si no hay rol admin, abortar (el usuario quedaría sin permisos).
  SELECT id INTO v_role_admin FROM sigm.roles WHERE code='CLINIC_ADMIN' AND tenant_id IS NULL LIMIT 1;
  IF v_role_admin IS NULL THEN
    RAISE EXCEPTION 'Seed demo: rol CLINIC_ADMIN no encontrado. Ejecutar primero seeds/001_roles_permissions.sql';
  END IF;
  SELECT id INTO v_role_doc FROM sigm.roles WHERE code='PHYSICIAN' AND tenant_id IS NULL LIMIT 1;
  IF v_role_doc IS NULL THEN SELECT id INTO v_role_doc FROM sigm.roles WHERE code='DOCTOR' AND tenant_id IS NULL LIMIT 1; END IF;
  IF v_role_doc IS NULL THEN SELECT id INTO v_role_doc FROM sigm.roles WHERE code='TENANT_ADMIN' AND tenant_id IS NULL LIMIT 1; END IF;
  SELECT id INTO v_role_rec FROM sigm.roles WHERE code='RECEPTIONIST' AND tenant_id IS NULL LIMIT 1;
  SELECT id INTO v_spec FROM sigm.specialties WHERE is_active ORDER BY code LIMIT 1;
  SELECT id INTO v_spec2 FROM sigm.specialties WHERE is_active AND id <> v_spec ORDER BY code LIMIT 1;
  IF v_spec2 IS NULL THEN v_spec2 := v_spec; END IF;
  SELECT id INTO v_plan FROM sigm.plans WHERE is_active AND (product_type='CLINIC' OR product_type='BOTH') ORDER BY price LIMIT 1;

  -- Tenant, organización y suscripción
  INSERT INTO sigm.tenants(id,tenant_code,legal_name,commercial_name,tax_id,product_type,status,timezone,locale,currency)
  VALUES (v_tid,'CLINIC-DEMO','Clínica Demo IPS SAS','SIGM Clínica Demo','900123456-1','CLINIC','ACTIVE','America/Bogota','es-CO','COP');

  INSERT INTO sigm.organizations(id,tenant_id,legal_name,commercial_name,tax_id,organization_type,is_primary,status,email,phone,address_line,city,department)
  VALUES (v_org,v_tid,'Clínica Demo IPS SAS','SIGM Clínica Demo','900123456-1','CLINIC',true,'ACTIVE','contacto@demo.sigm.local','6015550000','Cra 7 # 100-20','Bogotá','Bogotá D.C.');

  IF v_plan IS NOT NULL THEN
    INSERT INTO sigm.tenant_subscriptions(tenant_id,plan_id,status,started_at,current_period_start)
    VALUES (v_tid,v_plan,'ACTIVE',now(),now());
  END IF;

  -- Usuarios, membresías y roles
  INSERT INTO sigm.users(id,email,password_hash,full_name,phone,is_active) VALUES
    (v_admin,'admin@demo.sigm.local',v_hash_admin,'Administrador Demo','3000000001',true),
    (v_doc,'doctor@demo.sigm.local',v_hash_doc,'Dra. Laura Gómez','3000000002',true),
    (v_rec,'recepcion@demo.sigm.local',v_hash_rec,'Carlos Recepción','3000000003',true)
  ON CONFLICT (email) DO UPDATE SET password_hash=EXCLUDED.password_hash, full_name=EXCLUDED.full_name, is_active=true;

  SELECT id INTO v_admin FROM sigm.users WHERE email='admin@demo.sigm.local';
  SELECT id INTO v_doc FROM sigm.users WHERE email='doctor@demo.sigm.local';
  SELECT id INTO v_rec FROM sigm.users WHERE email='recepcion@demo.sigm.local';

  INSERT INTO sigm.user_tenants(tenant_id,user_id,status,joined_at) VALUES
    (v_tid,v_admin,'ACTIVE',now()),(v_tid,v_doc,'ACTIVE',now()),(v_tid,v_rec,'ACTIVE',now())
  ON CONFLICT DO NOTHING;

  IF v_role_admin IS NOT NULL THEN INSERT INTO sigm.user_roles(tenant_id,user_id,role_id) VALUES (v_tid,v_admin,v_role_admin) ON CONFLICT DO NOTHING; END IF;
  IF v_role_doc IS NOT NULL THEN INSERT INTO sigm.user_roles(tenant_id,user_id,role_id) VALUES (v_tid,v_doc,v_role_doc) ON CONFLICT DO NOTHING; END IF;
  IF v_role_rec IS NOT NULL THEN INSERT INTO sigm.user_roles(tenant_id,user_id,role_id) VALUES (v_tid,v_rec,v_role_rec) ON CONFLICT DO NOTHING; END IF;

  -- Especialidades, sede, departamento, consultorio
  IF v_spec IS NOT NULL THEN INSERT INTO sigm.tenant_specialties(tenant_id,specialty_id,is_active) VALUES (v_tid,v_spec,true) ON CONFLICT DO NOTHING; END IF;
  IF v_spec2 IS NOT NULL AND v_spec2 <> v_spec THEN INSERT INTO sigm.tenant_specialties(tenant_id,specialty_id,is_active) VALUES (v_tid,v_spec2,true) ON CONFLICT DO NOTHING; END IF;

  INSERT INTO sigm.sites(id,tenant_id,organization_id,code,name,address_line,city,department,phone,is_active)
  VALUES (v_site,v_tid,v_org,'SEDE-01','Sede Principal','Cra 7 # 100-20','Bogotá','Bogotá D.C.','6015550000',true);

  INSERT INTO sigm.departments(id,tenant_id,site_id,code,name,is_active)
  VALUES (v_dept,v_tid,v_site,'CONS-EXT','Consulta Externa',true);

  INSERT INTO sigm.medical_offices(id,tenant_id,site_id,code,name,floor,capacity,is_active)
  VALUES (v_office,v_tid,v_site,'C-101','Consultorio 101','1',1,true);

  -- Profesional y asignación de especialidad
  INSERT INTO sigm.professionals(id,tenant_id,user_id,display_name,professional_type,professional_license,primary_site_id,is_active)
  VALUES (v_pro,v_tid,v_doc,'Dra. Laura Gómez','PHYSICIAN','RM-12345',v_site,true);

  IF v_spec IS NOT NULL THEN
    INSERT INTO sigm.professional_specialties(tenant_id,professional_id,specialty_id,is_active,verification_status)
    VALUES (v_tid,v_pro,v_spec,true,'VERIFIED') ON CONFLICT DO NOTHING;
  END IF;

  -- Agenda, tipo de cita y disponibilidad
  INSERT INTO sigm.schedules(id,tenant_id,professional_id,site_id,medical_office_id,name,timezone,is_active)
  VALUES (v_sched,v_tid,v_pro,v_site,v_office,'Agenda Dra. Gómez','America/Bogota',true);

  INSERT INTO sigm.appointment_types(id,tenant_id,code,name,duration_minutes,is_active)
  VALUES (v_type,v_tid,'CONS-GEN','Consulta general',30,true);

  INSERT INTO sigm.availability_rules(id,tenant_id,schedule_id,weekday,start_time,end_time,slot_duration_minutes,is_active)
  SELECT gen_random_uuid(),v_tid,v_sched,d,'08:00'::time,'12:00'::time,30,true FROM generate_series(1,5) d;

  -- Pacientes y expediente
  INSERT INTO sigm.patients(id,tenant_id,document_type,document_number,first_name,last_name,birth_date,sex_at_birth,email,phone,city,department,is_active)
  VALUES
    (v_pat1,v_tid,'CC','1002003001','Ana María','Pérez Rojas','1990-05-14','F','ana.perez@ejemplo.test','3101112233','Bogotá','Bogotá D.C.',true),
    (v_pat2,v_tid,'CC','1002003002','Juan Carlos','Rodríguez','1985-11-02','M','juan.rodriguez@ejemplo.test','3104445566','Bogotá','Bogotá D.C.',true);

  INSERT INTO sigm.clinical_records(tenant_id,patient_id,status) VALUES (v_tid,v_pat1,'OPEN'),(v_tid,v_pat2,'OPEN') ON CONFLICT DO NOTHING;

  INSERT INTO sigm.patient_alerts(tenant_id,patient_id,alert_type,severity,description,is_active,created_by)
  VALUES (v_tid,v_pat1,'ALLERGY','HIGH','Alergia reportada a penicilina (demo)',true,v_admin);

  -- Cita, encuentro, nota y datos clínicos
  INSERT INTO sigm.appointments(id,tenant_id,patient_id,professional_id,specialty_id,site_id,medical_office_id,appointment_type_id,starts_at,ends_at,status,reason,booked_by)
  VALUES (v_appt,v_tid,v_pat1,v_pro,v_spec,v_site,v_office,v_type,
          (date_trunc('day', now()) + interval '1 day' + interval '9 hour'),
          (date_trunc('day', now()) + interval '1 day' + interval '9 hour 30 min'),
          'CONFIRMED','Control general (demo)',v_rec);

  INSERT INTO sigm.encounters(id,tenant_id,patient_id,professional_id,specialty_id,site_id,medical_office_id,appointment_id,encounter_type,encounter_date,started_at,status,chief_complaint,created_by)
  VALUES (v_enc,v_tid,v_pat1,v_pro,v_spec,v_site,v_office,v_appt,'CONSULTATION',now(),now(),'IN_PROGRESS','Control general de rutina (demo)',v_doc);

  INSERT INTO sigm.clinical_notes(id,tenant_id,encounter_id,patient_id,author_user_id,note_type,status,current_version)
  VALUES (v_note,v_tid,v_enc,v_pat1,v_doc,'EVOLUTION','DRAFT',1);

  INSERT INTO sigm.clinical_note_versions(id,tenant_id,note_id,version_number,content,author_user_id)
  VALUES (gen_random_uuid(),v_tid,v_note,1,'{"subjective":"Paciente en buen estado general.","objective":"Sin hallazgos patológicos agudos.","assessment":"Control preventivo.","plan":"Hábitos saludables y control anual."}'::jsonb,v_doc);

  INSERT INTO sigm.allergies(tenant_id,patient_id,allergen,allergen_type,reaction,severity,status,recorded_by)
  VALUES (v_tid,v_pat1,'Penicilina','MEDICATION','Rash cutáneo','HIGH','ACTIVE',v_doc);

  INSERT INTO sigm.medical_history(tenant_id,patient_id,encounter_id,history_type,description,status,recorded_by)
  VALUES (v_tid,v_pat1,v_enc,'PERSONAL','Sin antecedentes quirúrgicos relevantes (demo).','ACTIVE',v_doc);

  INSERT INTO sigm.vital_signs(tenant_id,encounter_id,patient_id,temperature_c,heart_rate,respiratory_rate,systolic_bp,diastolic_bp,oxygen_saturation,weight_kg,height_cm,recorded_by)
  VALUES (v_tid,v_enc,v_pat1,36.7,72,16,118,76,98.0,65.0,165.0,v_doc);

  RAISE NOTICE 'Seed demo creado. Tenant: % (CLINIC-DEMO). Admin: admin@demo.sigm.local / Demo12345!', v_tid;
END $$;

COMMIT;

SELECT t.id AS tenant_id, t.tenant_code, t.product_type, u.email, u.full_name
FROM sigm.tenants t
JOIN sigm.user_tenants ut ON ut.tenant_id=t.id
JOIN sigm.users u ON u.id=ut.user_id
WHERE t.tenant_code='CLINIC-DEMO'
ORDER BY u.email;
