-- SIGM Cloud - Ampliación del demo: 8 pacientes, tarifario y acceso al portal paciente
-- Ejecutar DESPUÉS de seed_demo_full.sql y ANTES de seed_clinic_showcase.sql.
-- Idempotente: se puede volver a ejecutar sin duplicar nada.
\set ON_ERROR_STOP on

BEGIN;

DO $$
DECLARE
  v_tid uuid := '11111111-1111-4111-8111-111111111111';
  v_role_patient uuid;
  v_patient_user uuid := '99999999-9999-4999-8999-999999999999';
  -- Hash PBKDF2 v1 de "Demo12345!" (mismo que admin@demo.sigm.local).
  v_hash text := 'v1.100000.ykoAsMm6PxRelpXpRNE3lA==.r4ONpHkULraX4CRnVWqJYgXNIzPLna/Lv26CSrCESiE=';
BEGIN
  IF NOT EXISTS (SELECT 1 FROM sigm.tenants WHERE id=v_tid) THEN
    RAISE EXCEPTION 'Ejecute primero seed_demo_full.sql (tenant CLINIC-DEMO no existe)';
  END IF;

  -- --------------------------------------------------------------------------
  -- 1) Seis pacientes adicionales (los dos primeros los crea seed_demo_full).
  --    Se insertan en orden para que el showcase los numeremos P00000001..P00000008.
  -- --------------------------------------------------------------------------
  INSERT INTO sigm.patients(id,tenant_id,document_type,document_number,first_name,last_name,birth_date,sex_at_birth,email,phone,city,department,is_active)
  VALUES
    ('eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee4',v_tid,'CC','1002003004','Marta Elena','Vargas','1978-03-22','F','marta.vargas@ejemplo.test','3102223344','Bogotá','Bogotá D.C.',true),
    ('eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee5',v_tid,'CC','1002003005','Diego Alejandro','Ruiz','1992-07-10','M','diego.ruiz@ejemplo.test','3103334455','Medellín','Antioquia',true),
    ('eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee6',v_tid,'CC','1002003006','Sofía Camila','Torres','1988-01-30','F','sofia.torres@ejemplo.test','3104445566','Cali','Valle del Cauca',true),
    ('eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee7',v_tid,'CC','1002003007','Héctor Javier','Mendoza','1965-09-12','M','hector.mendoza@ejemplo.test','3105556677','Barranquilla','Atlántico',true),
    ('eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee8',v_tid,'TI','1002003008','Valentina','Ríos','2001-04-18','F','valentina.rios@ejemplo.test','3106667788','Bogotá','Bogotá D.C.',true),
    ('eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee9',v_tid,'CC','1002003009','Camila Andrea','Beltrán','1996-12-05','F','camila.beltran@ejemplo.test','3107788990','Bucaramanga','Santander',true)
  ON CONFLICT (id) DO NOTHING;

  INSERT INTO sigm.clinical_records(tenant_id,patient_id,status)
  SELECT v_tid, id, 'OPEN'
  FROM sigm.patients
  WHERE tenant_id=v_tid
    AND id IN ('eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee4'::uuid,'eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee5'::uuid,
               'eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee6'::uuid,'eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee7'::uuid,
               'eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee8'::uuid,'eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee9'::uuid)
  ON CONFLICT DO NOTHING;

  -- --------------------------------------------------------------------------
  -- 2) Tarifario del tenant (el showcase crea contratos que apuntan a estos codes).
  -- --------------------------------------------------------------------------
  INSERT INTO sigm.service_catalog(tenant_id,code,cups_code,name,category,unit,base_price,tax_rate,is_active)
  VALUES
    (v_tid,'CONSULTA_01','890301','Consulta general','CONSULTA','UNIDAD',80000,0,true),
    (v_tid,'CONSULTA_02','890302','Consulta especializada','CONSULTA','UNIDAD',120000,0,true),
    (v_tid,'LAB_HEMOGRAMA','90702','Hemograma completo','LABORATORIO','UNIDAD',25000,0,true),
    (v_tid,'LAB_GLUCOSA','90401','Glucosa en sangre','LABORATORIO','UNIDAD',12000,0,true)
  ON CONFLICT (tenant_id, code) DO UPDATE
    SET name=EXCLUDED.name, base_price=EXCLUDED.base_price, is_active=true, updated_at=now();

  -- --------------------------------------------------------------------------
  -- 3) Usuario del portal de pacientes. El PortalController vincula por email
  --    exacto entre sigm.users y sigm.patients dentro del mismo tenant.
  --    Credencial: ana.perez@ejemplo.test / Demo12345!
  -- --------------------------------------------------------------------------
  SELECT id INTO v_role_patient FROM sigm.roles WHERE code='PATIENT' AND tenant_id IS NULL LIMIT 1;
  IF v_role_patient IS NULL THEN
    RAISE EXCEPTION 'Rol PATIENT no encontrado. Ejecute primero seeds/001_roles_permissions.sql';
  END IF;

  INSERT INTO sigm.users(id,email,password_hash,full_name,phone,is_active)
  VALUES (v_patient_user,'ana.perez@ejemplo.test',v_hash,'Ana María Pérez (Paciente)','3101112233',true)
  ON CONFLICT (email) DO UPDATE SET password_hash=EXCLUDED.password_hash, is_active=true, full_name=EXCLUDED.full_name;

  INSERT INTO sigm.user_tenants(tenant_id,user_id,status,joined_at)
  VALUES (v_tid,v_patient_user,'ACTIVE',now())
  ON CONFLICT DO NOTHING;

  INSERT INTO sigm.user_roles(tenant_id,user_id,role_id)
  VALUES (v_tid,v_patient_user,v_role_patient)
  ON CONFLICT DO NOTHING;

  RAISE NOTICE 'Demo clínico ampliado: 8 pacientes, tarifario y usuario paciente listos.';
END $$;

COMMIT;
