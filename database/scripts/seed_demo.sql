-- ============================================================================
-- SIGM CLOUD - seed_demo.sql (compatibilidad)
-- ============================================================================
-- Este script era una versión antigua y rota: usaba columnas que ya no existen
-- (organizations.product_type, sites.is_main), el rol inválido TENANT_ADMIN y
-- dejaba password_hash en NULL, por lo que la cuenta demo nunca podía entrar.
-- Se conserva el nombre por compatibilidad con documentación antigua, pero
-- redirige al script demo real y mantenido. No ejecutar ningún otro seed demo
-- fuera de la secuencia documentada en docs/functional-site.md.
-- ============================================================================
\echo 'seed_demo.sql está obsoleto; cargando seed_demo_full.sql en su lugar.'
\ir seed_demo_full.sql
