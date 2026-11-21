-- SIGM Cloud - Detalle clínico de las 8 historias demo.
-- Ejecutar DESPUÉS de: run_all.sql -> seed_demo_full.sql -> seed_demo_clinical.sql -> seed_clinic_showcase.sql
-- Idempotente: todos los ids son deterministas y se usan ON CONFLICT (tenant_id, id) DO NOTHING.
-- Incluye: signos vitales, antecedentes personales/familiares, alergias, diagnósticos,
-- medicamentos, afiliación EPS, laboratorio con resultados, fórmulas y facturación.
\set ON_ERROR_STOP on

BEGIN;

DO $$
DECLARE
  v_tid uuid := '11111111-1111-4111-8111-111111111111';
  v_doc uuid; v_rec uuid; v_pro uuid; v_spec uuid; v_site uuid; v_office uuid;
  v_ins uuid; v_service uuid;
  v_row record;
  v_pid uuid; v_enc uuid; v_dx uuid; v_test uuid; v_order uuid; v_item uuid;
  v_id uuid; n int;
BEGIN
  SELECT id INTO v_doc FROM sigm.users WHERE lower(email)='doctor@demo.sigm.local' AND is_active LIMIT 1;
  SELECT id INTO v_rec FROM sigm.users WHERE lower(email)='recepcion@demo.sigm.local' AND is_active LIMIT 1;
  -- Debe existir la combinación profesional/especialidad en professional_specialties (FK de encounters).
  SELECT e.professional_id, e.specialty_id, e.site_id, e.medical_office_id INTO v_pro, v_spec, v_site, v_office
    FROM sigm.encounters e
   WHERE e.tenant_id=v_tid
     AND EXISTS (SELECT 1 FROM sigm.professional_specialties ps
                  WHERE ps.tenant_id=e.tenant_id AND ps.professional_id=e.professional_id
                    AND ps.specialty_id=e.specialty_id)
   LIMIT 1;
  SELECT id INTO v_ins FROM sigm.insurers WHERE lower(name) LIKE '%sura%' AND insurer_type='EPS' LIMIT 1;
  SELECT id INTO v_service FROM sigm.service_catalog WHERE tenant_id=v_tid AND code='CONSULTA_01' LIMIT 1;

  IF v_doc IS NULL OR v_rec IS NULL OR v_spec IS NULL OR v_site IS NULL THEN
    RAISE EXCEPTION 'Faltan datos base. Ejecute primero seed_demo_full.sql, seed_demo_clinical.sql y seed_clinic_showcase.sql';
  END IF;

  -- Toda historia demo necesita al menos una atención para poder asociar vitales, dx y fórmulas.
  FOR v_pid IN SELECT id FROM sigm.patients WHERE tenant_id=v_tid AND is_active
  LOOP
    IF NOT EXISTS (SELECT 1 FROM sigm.encounters WHERE tenant_id=v_tid AND patient_id=v_pid) THEN
      INSERT INTO sigm.encounters(tenant_id,patient_id,professional_id,specialty_id,site_id,medical_office_id,
        encounter_type,encounter_date,started_at,ended_at,chief_complaint,status,created_by,created_at,updated_at)
      VALUES(v_tid,v_pid,v_pro,v_spec,v_site,v_office,'CONSULTATION',
        now() - interval '3 days', now() - interval '3 days', now() - interval '3 days' + interval '30 min',
        'Control y seguimiento clínico demo','COMPLETED',v_doc,now(),now());
    END IF;
  END LOOP;

  -- =====================================================================
  -- 1) Perfil clínico base: alergia, antecedente personal, familiar y vitales
  -- =====================================================================
  FOR v_row IN
    SELECT * FROM (VALUES
      (1,'eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee1'::uuid,'Penicilina','MEDICATION','Exantema cutáneo leve','Hipertensión arterial desde 2019, en tratamiento','Padre','Hipertensión arterial'),
      (2,'eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee2'::uuid,'Mariscos','FOOD','Urticaria leve','Asma bronquial desde la infancia','Madre','Asma bronquial'),
      (3,'eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee4'::uuid,'Ibuprofeno','MEDICATION','Dolor epigástrico','Reflujo gastroesofágico desde 2021','Hermana','Reflujo gastroesofágico'),
      (4,'eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee5'::uuid,'Polen','ENVIRONMENTAL','Rinitis estacional','Diabetes tipo 2 diagnosticada en 2018','Padre','Diabetes tipo 2'),
      (5,'eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee6'::uuid,'Contraste yodado','MEDICATION','Reacción leve','Tendinitis de hombro derecho en 2023','Madre','Hipertensión arterial'),
      (6,'eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee7'::uuid,'Látex','LATEX','Prurito en manos','Hipotiroidismo en control desde 2020','Abuela','Hipotiroidismo'),
      (7,'eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee8'::uuid,'Aspirina','MEDICATION','Epistaxis','Gastroduodenitis crónica desde 2017','Padre','Úlcera péptica'),
      (8,'eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee9'::uuid,'Frutos secos','FOOD','Angioedema leve','Ansiedad y depresión en tratamiento desde 2024','Madre','Trastorno de ansiedad')
    ) AS t(n,patient_id,allergen,allergen_type,reaction,history,fam_rel,fam_cond)
    LOOP
      v_pid := v_row.patient_id;
      SELECT id INTO v_enc FROM sigm.encounters WHERE tenant_id=v_tid AND patient_id=v_pid ORDER BY encounter_date DESC LIMIT 1;

      INSERT INTO sigm.allergies(id,tenant_id,patient_id,allergen,allergen_type,reaction,severity,onset_date,status,recorded_by,created_at,updated_at)
      VALUES(md5(v_tid::text||':demo-allergy:'||v_pid::text)::uuid,v_tid,v_pid,v_row.allergen,v_row.allergen_type,
        v_row.reaction, CASE WHEN v_row.n IN (2,6) THEN 'HIGH' ELSE 'MEDIUM' END,
        (now() - interval '2 years')::date,'ACTIVE',v_doc,now(),now())
      ON CONFLICT (tenant_id,id) DO NOTHING;

      INSERT INTO sigm.medical_history(id,tenant_id,patient_id,encounter_id,history_type,category,description,onset_date,status,recorded_by,created_at,updated_at)
      VALUES(md5(v_tid::text||':demo-history:'||v_pid::text)::uuid,v_tid,v_pid,v_enc,'PERSONAL','ANTECEDENTE',
        v_row.history,(now() - ((v_row.n*3)||' years')::interval)::date,'ACTIVE',v_doc,now(),now())
      ON CONFLICT (tenant_id,id) DO NOTHING;

      INSERT INTO sigm.family_history(id,tenant_id,patient_id,relative_relationship,condition_description,age_at_onset,status,recorded_by,created_at,updated_at)
      VALUES(md5(v_tid::text||':demo-family:'||v_pid::text)::uuid,v_tid,v_pid,v_row.fam_rel,v_row.fam_cond,40+v_row.n,'ACTIVE',v_doc,now(),now())
      ON CONFLICT (tenant_id,id) DO NOTHING;

      FOR n IN 1..2 LOOP
        INSERT INTO sigm.vital_signs(id,tenant_id,encounter_id,patient_id,measured_at,temperature_c,heart_rate,
          respiratory_rate,systolic_bp,diastolic_bp,oxygen_saturation,weight_kg,height_cm,glucose_mg_dl,pain_scale,
          notes,recorded_by,created_at)
        VALUES(md5(v_tid::text||':demo-vitals:'||v_pid::text||':'||n::text)::uuid,v_tid,v_enc,v_pid,
          now() - (v_row.n||' days')::interval - ((n*20)||' minutes')::interval,
          (36.2 + v_row.n*0.1)::numeric(4,1), 68+v_row.n*2+n, 15+v_row.n, 112+v_row.n*3, 70+v_row.n*2,
          (97.5 - v_row.n*0.1)::numeric(5,2), (58.5+v_row.n*2.5)::numeric(6,2), (158+v_row.n*2)::numeric(5,1),
          (86+v_row.n*7)::numeric(7,2), CASE WHEN v_row.n=4 THEN 3 ELSE 0 END,
          'Control clínico demo '||n::text, v_doc, now())
        ON CONFLICT (tenant_id,id) DO NOTHING;
      END LOOP;
    END LOOP;

  -- =====================================================================
  -- 2) Diagnósticos, medicamento crónico y afiliación EPS
  -- =====================================================================
  FOR v_row IN
    SELECT * FROM (VALUES
      ('eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee1'::uuid,'E119','Diabetes mellitus tipo 2 sin complicaciones','Metformina','850 mg','cada 12 horas'),
      ('eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee2'::uuid,'J450','Asma alérgica persistente moderada','Salbutamol','100 mcg','cada 6 horas si necesario'),
      ('eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee4'::uuid,'K219','Enfermedad por reflujo gastroesofágico','Omeprazol','20 mg','cada 24 horas'),
      ('eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee5'::uuid,'I10X','Hipertensión arterial esencial','Losartán','50 mg','cada 24 horas'),
      ('eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee6'::uuid,'K299','Gastroduodenitis no especificada','Ranitidina','150 mg','cada 12 horas'),
      ('eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee7'::uuid,'E039','Hipotiroidismo no especificado','Levotiroxina','75 mcg','cada 24 horas'),
      ('eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee8'::uuid,'E789','Trastorno del metabolismo de lipoproteínas','Atorvastatina','20 mg','cada 24 horas'),
      ('eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee9'::uuid,'F412','Trastorno mixto de ansiedad y depresión','Sertralina','50 mg','cada 24 horas')
    ) AS t(patient_id,dx_code,dx_note,med_name,med_dose,med_freq)
    LOOP
      v_pid := v_row.patient_id;
      SELECT id INTO v_enc FROM sigm.encounters WHERE tenant_id=v_tid AND patient_id=v_pid ORDER BY encounter_date DESC LIMIT 1;
      SELECT id INTO v_dx FROM sigm.diagnosis_catalog WHERE code=v_row.dx_code LIMIT 1;

      IF v_dx IS NOT NULL THEN
        INSERT INTO sigm.diagnoses(id,tenant_id,encounter_id,patient_id,diagnosis_catalog_id,diagnosis_type,certainty,
          onset_date,notes,recorded_by,created_at,updated_at)
        VALUES(md5(v_tid::text||':demo-dx:'||v_pid::text)::uuid,v_tid,v_enc,v_pid,v_dx,'PRIMARY','CONFIRMED',
          (now() - interval '18 months')::date,v_row.dx_note,v_doc,now(),now())
        ON CONFLICT (tenant_id,id) DO NOTHING;
      END IF;

      INSERT INTO sigm.patient_medications(id,tenant_id,patient_id,medication_name,dose,frequency,route,start_date,status,notes,recorded_by,created_at,updated_at)
      VALUES(md5(v_tid::text||':demo-med:'||v_pid::text)::uuid,v_tid,v_pid,v_row.med_name,v_row.med_dose,
        v_row.med_freq,'ORAL',(now() - interval '6 months')::date,'ACTIVE','Tratamiento crónico demo',v_doc,now(),now())
      ON CONFLICT (tenant_id,id) DO NOTHING;

      IF v_ins IS NOT NULL THEN
        INSERT INTO sigm.patient_insurances(id,tenant_id,patient_id,insurer_id,affiliate_number,plan_name,is_primary,valid_from,status,created_at,updated_at)
        VALUES(md5(v_tid::text||':demo-ins:'||v_pid::text)::uuid,v_tid,v_pid,v_ins,
          'AF-DEMO-'||substr(md5(v_pid::text),1,8),'Plan Complementario Demo',false,
          (now() - interval '1 year')::date,'ACTIVE',now(),now())
        ON CONFLICT (tenant_id,id) DO NOTHING;
      END IF;
    END LOOP;

  -- =====================================================================
  -- 3) Laboratorio con resultados
  -- =====================================================================
  FOR v_row IN
    SELECT * FROM (VALUES
      ('eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee1'::uuid,'LAB_HBA1C','7.2 %','HIGH'),
      ('eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee1'::uuid,'LAB_GLUCOSA','138 mg/dL','HIGH'),
      ('eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee2'::uuid,'LAB_HEMOGRAMA','Eosinófilos 7 %','ABNORMAL'),
      ('eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee4'::uuid,'LAB_GLUCOSA','92 mg/dL','NORMAL'),
      ('eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee5'::uuid,'LAB_CREATININA','1.1 mg/dL','NORMAL'),
      ('eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee5'::uuid,'LAB_COLESTEROL','228 mg/dL','HIGH'),
      ('eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee6'::uuid,'LAB_HEMOGRAMA','Hb 13.8 g/dL','NORMAL'),
      ('eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee7'::uuid,'LAB_TSH','5.8 mUI/L','HIGH'),
      ('eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee8'::uuid,'LAB_TRIGLICERIDOS','210 mg/dL','HIGH'),
      ('eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee8'::uuid,'LAB_CREATININA','0.9 mg/dL','NORMAL'),
      ('eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee9'::uuid,'LAB_HEMOGRAMA','Hb 12.4 g/dL','LOW'),
      ('eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee9'::uuid,'LAB_UREA','32 mg/dL','NORMAL')
    ) AS t(patient_id,test_code,result,flag)
    LOOP
      SELECT id INTO v_test FROM sigm.laboratory_tests WHERE code=v_row.test_code LIMIT 1;
      IF v_test IS NULL THEN CONTINUE; END IF;

      v_pid := v_row.patient_id;
      SELECT id INTO v_enc FROM sigm.encounters WHERE tenant_id=v_tid AND patient_id=v_pid ORDER BY encounter_date DESC LIMIT 1;
      v_order := md5(v_tid::text||':demo-lab-order:'||v_pid::text||':'||v_row.test_code)::uuid;

      INSERT INTO sigm.laboratory_orders(id,tenant_id,encounter_id,patient_id,order_number,ordered_by,status,priority,ordered_at,notes,created_at,updated_at)
      VALUES(v_order,v_tid,v_enc,v_pid,'L-DEMO-'||substr(md5(v_pid::text||v_row.test_code),1,10),v_doc,'RESULTED','ROUTINE',
        now() - interval '5 days','Orden demo con resultado',now(),now())
      ON CONFLICT (tenant_id,id) DO NOTHING;

      v_item := md5(v_tid::text||':demo-lab-item:'||v_pid::text||':'||v_row.test_code)::uuid;
      INSERT INTO sigm.laboratory_order_items(id,tenant_id,laboratory_order_id,test_id,status,created_at,updated_at)
      VALUES(v_item,v_tid,v_order,v_test,'VALIDATED',now(),now())
      ON CONFLICT (tenant_id,id) DO NOTHING;

      INSERT INTO sigm.laboratory_results(id,tenant_id,order_item_id,result_value,unit,reference_range,result_flag,
        resulted_at,validated_by,validated_at,created_at)
      VALUES(md5(v_tid::text||':demo-lab-result:'||v_pid::text||':'||v_row.test_code)::uuid,v_tid,v_item,
        split_part(v_row.result,' ',1),
        CASE WHEN strpos(v_row.result,' ') > 0 THEN substring(v_row.result from strpos(v_row.result,' ')+1) ELSE NULL END,
        'Ver rango del laboratorio',v_row.flag,now() - interval '4 days',v_doc,now() - interval '4 days',now())
      ON CONFLICT (tenant_id,id) DO NOTHING;
    END LOOP;

  -- =====================================================================
  -- 4) Fórmulas médicas, facturas, items y pagos
  -- =====================================================================
  FOR v_row IN
    SELECT * FROM (VALUES
      ('eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee1'::uuid,'Metformina 850 mg','1 tableta','cada 12 horas',30,'60 tabletas'),
      ('eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee2'::uuid,'Salbutamol 100 mcg','2 inhalaciones','cada 6 horas',15,'1 inhalador'),
      ('eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee4'::uuid,'Omeprazol 20 mg','1 cápsula','cada 24 horas',30,'30 cápsulas'),
      ('eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee5'::uuid,'Losartán 50 mg','1 tableta','cada 24 horas',30,'30 tabletas'),
      ('eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee6'::uuid,'Ranitidina 150 mg','1 tableta','cada 12 horas',15,'30 tabletas'),
      ('eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee7'::uuid,'Levotiroxina 75 mcg','1 tableta','cada 24 horas',60,'60 tabletas'),
      ('eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee8'::uuid,'Atorvastatina 20 mg','1 tableta','cada 24 horas',30,'30 tabletas'),
      ('eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee9'::uuid,'Sertralina 50 mg','1 tableta','cada 24 horas',30,'30 tabletas')
    ) AS t(patient_id,rx_name,dose,freq,days,qty)
    LOOP
      v_pid := v_row.patient_id;
      SELECT id INTO v_enc FROM sigm.encounters WHERE tenant_id=v_tid AND patient_id=v_pid ORDER BY encounter_date DESC LIMIT 1;

      v_id := md5(v_tid::text||':demo-rx:'||v_pid::text)::uuid;
      INSERT INTO sigm.prescriptions(id,tenant_id,encounter_id,patient_id,prescription_number,status,issued_at,issued_by,notes,created_at,updated_at)
      VALUES(v_id,v_tid,v_enc,v_pid,'RX-DEMO-'||substr(md5(v_pid::text),1,8),'ISSUED',now() - interval '5 days',v_doc,
        'Fórmula demo. Suspender si aparece reacción adversa.',now(),now())
      ON CONFLICT (tenant_id,id) DO NOTHING;

      INSERT INTO sigm.prescription_items(id,tenant_id,prescription_id,medication_name,dose,frequency,route,duration_days,quantity,instructions,created_at)
      VALUES(md5(v_tid::text||':demo-rx-item:'||v_pid::text)::uuid,v_tid,v_id,v_row.rx_name,v_row.dose,v_row.freq,
        'ORAL',v_row.days,v_row.qty,'Tomar con agua. No suspender sin indicación médica.',now())
      ON CONFLICT (tenant_id,id) DO NOTHING;

      -- Factura de consulta (total = subtotal - descuento + impuestos = 80000)
      v_id := md5(v_tid::text||':demo-invoice:'||v_pid::text)::uuid;
      INSERT INTO sigm.invoices(id,tenant_id,invoice_number,patient_id,insurer_id,contract_id,encounter_id,issue_date,due_date,
        currency,subtotal,discount_total,tax_total,total,status,electronic_status,created_by,created_at,updated_at)
      VALUES(v_id,v_tid,'FV-DEMO-'||substr(md5(v_pid::text),1,8),v_pid,v_ins,NULL,v_enc,
        (now() - interval '5 days')::date,(now() + interval '25 days')::date,'COP',80000,0,0,80000,
        'ISSUED','NOT_APPLICABLE',v_doc,now(),now())
      ON CONFLICT (tenant_id,id) DO NOTHING;

      INSERT INTO sigm.invoice_items(id,tenant_id,invoice_id,service_id,description,quantity,unit_price,discount_amount,tax_rate,tax_amount,encounter_id,created_at)
      VALUES(md5(v_tid::text||':demo-invoice-item:'||v_pid::text)::uuid,v_tid,v_id,v_service,
        'Consulta médica general demo',1,80000,0,0,0,v_enc,now())
      ON CONFLICT (tenant_id,id) DO NOTHING;
    END LOOP;

  RAISE NOTICE 'Detalle clínico demo cargado: 8 pacientes con vitales, antecedentes, dx, laboratorio, fórmulas y facturación.';
END $$;

COMMIT;
