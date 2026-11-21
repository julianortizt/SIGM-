# Despliegue en Vercel + Neon (guía corta)

Este repositorio **no es Laravel**. Es una API **.NET 10 (C#)** + 5 portales **React/Vite** + **PostgreSQL 18 (Neon)**. De `GUIA-DESPLIEGUE-LARAVEL-VERCEL.md` solo se toma la parte de "subir a Vercel, BD administrada, migraciones manuales"; lo demás (Laravel, Turso, R2) no aplica.

## 0. ¿Para qué sirve .NET aquí? (en corto)

Los portales React son la **carátula**: pantallas, botones y formularios. El backend .NET es el **motor y la cerradura**:

- comprueba quién eres: login, JWT con revocación, MFA, bloqueo por intentos;
- decide qué puedes ver de qué clínica: multi-tenant con RLS (fija `app.tenant_id` en cada conexión a Postgres);
- aplica permisos finos y licencias por módulo;
- valida reglas clínicas/legales: firma y adendas de notas, estados de citas, totales de facturación, PDF con hash SHA-256, auditoría;
- envía recordatorios y gestiona backups.

Hay unas **190 rutas en 36 controladores**. Si se elimina .NET no se simplifica el proyecto: hay que reescribir esa capa dentro de Vercel Functions/Supabase, con riesgo en datos clínicos. Por eso se despliega como **un contenedor** y los portales como sitios estáticos.

> Nota: de los 11 proyectos C#, 8 son cáscaras vacías (sin archivos `.cs`). No afectan al despliegue: `backend/Dockerfile.vercel` solo publica `SIGM.Api`. Se pueden ignorar.

## 1. Qué se despliega (un solo proyecto Vercel)

| Pieza | Servicio Vercel | Resultado |
|---|---|---|
| API .NET | `api` (container, puerto 80) | `/api/*` y `/health*` |
| Clínicas, Consultorios, Pacientes, Admin, Welcome | 5 servicios Vite | un subdominio cada uno |
| Base de datos | Neon (Marketplace) | PostgreSQL 18 |
| Migraciones | `psql` manual | nunca en el arranque |

## 2. Pasos (en este orden)

1. **Repo:** `git init && git add . && git commit -m "deploy Vercel"`, crear repo en GitHub y hacer push. (`git status` antes: sin `.env` ni `node_modules`.)
2. **Vercel:** Add New → Project → importar repo → Root Directory `.` → **Framework Preset: Services** (Beta) → Deploy. El primer fallo por variables es normal.
3. **Neon:** Marketplace → Neon → Create database con **PostgreSQL 18**. La integración inyecta `DATABASE_URL` (pooler) y `DATABASE_URL_UNPOOLED` (directa).
4. **Roles (antes de migrar):** en el SQL Editor de Neon:
   ```sql
   CREATE ROLE sigm_app WITH LOGIN PASSWORD '<largo-y-aleatorio>';
   CREATE ROLE sigm_platform WITH LOGIN BYPASSRLS PASSWORD '<largo-y-aleatorio>';
   ```
   `sigm_app` **debe** tener `rolbypassrls = false`; créalo con SQL, nunca desde el asistente de Neon (heredaría `BYPASSRLS` y rompería el aislamiento).
5. **Migrar** (una vez, desde local): `psql "$DATABASE_URL_UNPOOLED" -v ON_ERROR_STOP=1 -f database/run_all.sql`. Es idempotente; aplica las 22 migraciones y los seeds.
6. **Variables** en Project Settings → Environment Variables (Production y Preview): copiar `infrastructure/vercel.env.example`. Lo mínimo es `ConnectionStrings__Default`, `ConnectionStrings__Platform`, `Jwt__Key` (≥32 chars) y `Database__AutoBackupEnabled=false`. Los `VITE_*` ya van en el `buildCommand` de `vercel.json`.
7. **Dominio:** buscar/reemplazar `TU_DOMINIO.com` en `vercel.json` y añadir los 6 dominios en Project Settings → Domains (CNAME a `cname.vercel-dns.com`). Sin dominio, `*.vercel.app` sirve la landing y `/api/*` funciona igual.
8. **Push** a `main` → Vercel construye los 6 servicios.

## 3. Verificación

```bash
curl https://TU_PROYECTO.vercel.app/health
curl https://TU_PROYECTO.vercel.app/health/ready   # {"status":"ready","database":true}
curl https://TU_PROYECTO.vercel.app/api/v1/public/welcome
curl -I https://app.TU_DOMINIO.com
```

Nota: `/api/health` **no existe**; el health de la API está en `/health` y `/health/ready`. Todo lo demás de la API sí va bajo `/api/v1/*`.

Después: login en el navegador y comprobar que se escribe auditoría con `sigm_app` sin `BYPASSRLS`.

## 4. Problemas típicos

| Síntoma | Arreglo |
|---|---|
| `502` en `/api/*` | No cambiar `ASPNETCORE_URLS=http://+:80` en el Dockerfile |
| Redirect infinito http↔https | Ya está `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` |
| CORS bloqueado | Añadir `CORS__AllowedOrigins__N` con el origen exacto (`https://...`) |
| RLS "no funciona" | Recrear `sigm_app` con SQL; verificar `rolbypassrls = false` |
| `/health/ready` da 503 | Revisar `ConnectionStrings__Platform` |
| Todo muestra la landing | Dominios no configurados o placeholder sin reemplazar |

## 5. Estado real de este despliegue

- Proyecto Vercel: `hcmmedical` (equipo `julianortizt-7539s-projects`), repo `https://github.com/julianortizt/SIGM-`, rama `main`.
- URL pública: **https://hcmmedical.vercel.app**.
- Deploy verificado: `/health` y `/health/ready` → 200, `/api/v1/public/welcome` → 200, landing → 200, login con credenciales inválidas → 401.
- Neon creado desde Marketplace (`sigm`), PostgreSQL 18.7, 96 policies RLS, 105 tablas, extensiones `pgcrypto`, `btree_gist`, `pg_trgm`, `citext`, `unaccent`.
- Roles: `sigm_app` (login, sin `BYPASSRLS`) y `sigm_platform` (login, con `BYPASSRLS`).
- Migraciones 001–022 aplicadas. Detalle operativo: `run_all.sql` se ejecutó como `neondb_owner`; la migración 020 requiere `current_user = sigm_platform` (se hizo con `SET ROLE` tras `GRANT sigm_platform TO neondb_owner` y `GRANT CREATE ON SCHEMA sigm TO sigm_platform`); las migraciones 021–022 requieren ser propietario de las tablas y se ejecutaron como `neondb_owner`.
- Variables de entorno de **Production** cargadas. Las de **Preview** quedan pendientes: la CLI no permite aplicar "todas las ramas de preview" y `main` es la rama de producción; se pueden añadir desde el dashboard (Project Settings → Environment Variables → Preview → All Preview branches).
- Sin dominio propio: funcionan la landing, `/health*` y `/api/v1/*`; los otros 4 portales requieren sus subdominios.

## 6. Límites que quedan (no son del deploy)

- **Archivos:** `LocalFileStorageService` escribe en disco efímero → migrar a Vercel Blob/R2 antes de subir documentos reales.
- **Workers:** recordatorios y backups programados no son fiables en contenedores que escalan a cero → usar Vercel Cron y PITR de Neon.
- **Previews:** comparten base con Production si no se configura branching de Neon → no cargar datos clínicos reales.
