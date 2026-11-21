-- SIGM Cloud - Datos ficticios de demostración (NO usar en producción)
-- Ejecutar sobre una base con run_all.sql aplicado.
-- Usuario demo: demo@sigm.local / Demo12345! (tenant CLINIC-DEMO)
-- Nota: ajusta hashes si cambia el esquema de PasswordService.

BEGIN;
DO $$
DECLARE
  v_tid uuid := gen_random_uuid();
  v_user uuid := gen_random_uuid();
  v_site uuid := gen_random_uuid();
  v_pro uuid := gen_random_uuid();
  v_pat uuid := gen_random_uuid();
  v_role uuid;
  v_spec uuid;
  v_hash text := 'v1.100000.K1iZ2bZ6Xb8e2u9mYV0mTg==.m6r2mT4m2o9mU0m7n1a2b3c4d5e6f7g8h9i0j1k2l3m='; -- placeholder, reemplazar por hash real
BEGIN
  IF NOT EXISTS (SELECT 1 FROM sigm.tenants WHERE tenant_code='CLINIC-DEMO') THEN
    INSERT INTO sigm.tenants(id,tenant_code,legal_name,commercial_name,product_type,status)
    VALUES (v_tid,'CLINIC-DEMO','Clínica Demo SAS','Clínica Demo','CLINIC','ACTIVE');
    INSERT INTO sigm.organizations(tenant_id,legal_name,commercial_name,product_type,status,email)
    VALUES (v_tid,'Clínica Demo SAS','Clínica Demo','CLINIC','ACTIVE','demo@sigm.local');
    INSERT INTO sigm.users(id,email,password_hash,full_name,is_active)
    VALUES (v_user,'demo@sigm.local',NULL,'Administrador Demo',true)
    ON CONFLICT (email) DO UPDATE SET full_name=EXCLUDED.full_name RETURNING id INTO v_user;
    INSERT INTO sigm.user_tenants(tenant_id,user_id,status,joined_at) VALUES (v_tid,v_user,'ACTIVE',now()) ON CONFLICT DO NOTHING;
    SELECT id INTO v_role FROM sigm.roles WHERE code='TENANT_ADMIN' AND tenant_id IS NULL LIMIT 1;
    IF v_role IS NOT NULL THEN INSERT INTO sigm.user_roles(tenant_id,user_id,role_id) VALUES (v_tid,v_user,v_role) ON CONFLICT DO NOTHING; END IF;
    SELECT id INTO v_spec FROM sigm.specialties WHERE is_active ORDER BY name LIMIT 1;
    IF v_spec IS NOT NULL THEN INSERT INTO sigm.tenant_specialties(tenant_id,specialty_id,is_active) VALUES (v_tid,v_spec,true) ON CONFLICT DO NOTHING; END IF;
    INSERT INTO sigm.sites(id,tenant_id,name,code,city,is_main,is_active) VALUES (v_site,v_tid,'Sede Principal','SEDE-1','Bogotá',true,true);
    RAISE NOTICE 'Tenant demo creado: % (usuario demo@sigm.local). Defina contraseña vía API /api/v1/users o actualice password_hash con hash PBKDF2 v1 de la app.', v_tid;
  ELSE
    RAISE NOTICE 'CLINIC-DEMO ya existe, omitiendo.';
  END IF;
END $$;
COMMIT;
