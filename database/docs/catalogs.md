# Catálogos oficiales - SIGM Cloud

Los catálogos clínicos globales de SIGM Cloud se alimentan desde las **tablas de
referencia oficiales de SISPRO** (Ministerio de Salud y Protección Social de
Colombia). No son muestras: `run_all.sql` carga los conjuntos completos.

## Fuente autorizada

| Catálogo | Tabla SIGM | Fuente |
| --- | --- | --- |
| CIE-10 | `sigm.diagnosis_catalog` | `https://web.sispro.gov.co/WebPublico/Consultas/ConsultarDetalleReferenciaBasica.aspx?Code=CIE10` |
| CUPS | `sigm.procedure_catalog` | `.../ConsultarDetalleReferenciaBasica.aspx?Code=CUPS` |
| IUM | `sigm.medication_catalog` | `.../ConsultarDetalleReferenciaBasica.aspx?Code=IUM` |

SISPRO expone estas tablas como una grilla ASP.NET paginada. El importador
descarga todas las páginas, normaliza los campos y genera tres archivos:

| Catálogo | CSV | Seed SQL | Registros | SHA-256 del CSV |
| --- | --- | --- | ---: | --- |
| CIE-10 | `import/data/cie10.csv` | `seeds/006_catalogs_cie10.sql` | 12.634 | `0c6090267b919dd122a7392ecf1934c37b5b957c9087cb9de4fdd3f4860f1332` |
| CUPS | `import/data/cups.csv` | `seeds/007_catalogs_cups.sql` | 10.024 | `2e70232f1a958c9ba290d9e62e428993bd2f7c928d1ab2c3f118b6fc4b3f106f` |
| IUM | `import/data/ium.csv` | `seeds/008_catalogs_ium.sql` | 34.642 | `84d0be316e5899e473f0fe5f2b264576e6c5fd1a445fd55db90ac7617c263b7c` |

Los hashes y la fecha de descarga quedan registrados en
`import/data/manifest.json`; úselos para verificar que un seed corresponde
exactamente a una descarga concreta.

## Importación

Requisitos: Python 3.10 o superior. El importador usa solo la biblioteca
estándar, por lo que no necesita `pip install` ni Docker.

```powershell
# Descarga los tres catálogos, valida volumenes y regenera los seeds
python database/import/import_catalogs.py

# Solo CIE-10 y CUPS
python database/import/import_catalogs.py --only CIE10,CUPS

# Regenera los seeds desde los CSV ya descargados (sin volver a SISPRO)
python database/import/import_catalogs.py --skip-scrape

# Verifica los CSV existentes contra los minimos esperados
python database/import/import_catalogs.py --check
```

El script aborta si un catálogo queda por debajo del volumen mínimo esperado
(12.000 CIE-10, 9.500 CUPS, 33.000 IUM), si hay descripciones vacías o si la
paginación devuelve una página repetida. Esto evita publicar catálogos truncados.

### Idempotencia

Los seeds usan `COPY` a una tabla temporal y luego
`INSERT ... ON CONFLICT (code) DO UPDATE`. Se pueden ejecutar varias veces; la
segunda ejecución actualiza descripciones y no duplica filas.

### Reimportación en producción

En producción los catálogos globales ya están protegidos por RLS (`tenant_id
NULL`) y `sigm_app` solo tiene `SELECT`. Para recargarlos:

1. Ejecute el importador en un entorno de preparación y revise el
   `manifest.json` y el diff de los CSV.
2. Programe una ventana de mantenimiento.
3. Ejecute los seeds con `sigm_platform` (o con el propietario del esquema),
   nunca con `sigm_app`.
4. Valide con `tests/003_catalog_test.sql`.

## Mapeo de columnas

### CIE-10

| Columna SIGM | Origen SISPRO | Ejemplo |
| --- | --- | --- |
| `code` | `Codigo` | `A000`, `I10X` |
| `description` | `Nombre` | `COLERA DEBIDO A VIBRIO CHOLERAE 01, BIOTIPO CHOLERAE` |
| `chapter` | `Extra_V` | `CIERTAS ENFERMEDADES INFECCIOSAS Y PARASITARIAS (A00-B99)` |
| `chapter_code` | Calculado | `I`, `IX`, `XXI` |
| `group_code` | `Extra_VI:Capitulo` | `A00`, `I10` |

SISPRO conserva el código de cuatro caracteres (`A000`, `I10X`); no se
reconvierte a la notación con punto (`A00.0`) para no romper la trazabilidad con
RIPS y FHIR.

### CUPS

| Columna SIGM | Origen SISPRO |
| --- | --- |
| `code` | `Codigo` (6 dígitos) |
| `name` | `Nombre` |
| `chapter` | `Descripcion` (capítulo o anexo técnico) |

El nombre oficial de dos procedimientos supera los 250 caracteres, por eso
`sigm.procedure_catalog.name` es `text`.

### IUM

| Columna SIGM | Origen SISPRO |
| --- | --- |
| `code` | `Codigo` (15 caracteres alfanuméricos) |
| `name` | `Nombre` (principio activo, concentración, forma y presentación) |
| `presentation` | `Extra_X` (`CAJA`, `TABLETA`, `FRASCO`, ...) |
| `pharmaceutical_form` | `Extra_IV` (`TABLETAS...`, `SOLUCIONES`, ...) |
| `concentration` | Vacío: SISPRO no lo expone como campo separado |
| `route` | Vacío: SISPRO no lo expone como campo separado |

La concentración y la vía están embebidas en `name`
(p. ej. `ASFOTASA ALFA 40mg/1 ml OTRAS SOLUCIONES SUBCUTANEA ...`). Si una
integración requiere esos campos por separado, deben derivarse con un
enriquecimiento posterior o desde el catálogo del INVIMA.

La fila centinela `CERO` de la grilla de SISPRO se descarta: no es un IUM real.

## Prueba de catálogos

`tests/003_catalog_test.sql` verifica:

- Volumen mínimo de cada catálogo.
- Ausencia de descripciones o nombres vacíos.
- Al menos 20 capítulos CIE-10 mapeados.
- Códigos de control: `I10X`, `J00X`, `890201` y `1A1000101000100`.

Se ejecuta automáticamente con `scripts/validate.ps1`.

## Limitaciones y mantenimiento

- SISPRO actualiza las tablas de referencia periódicamente. Reimporte al menos
  una vez por trimestre o cuando el Ministerio publique una actualización.
- CIE-11 aún no se usa en RIPS; el modelo está preparado para convivir con
  CIE-10 y una futura tabla CIE-11.
- Los exámenes de laboratorio (`laboratory_tests`) no provienen de SISPRO: son
  un catálogo operativo de ejemplo que cada institución debe personalizar.
- Los códigos de habilitación, sede y servicios deben validarse contra el
  Registro Especial de Prestadores de Servicios de Salud (REPS) en la capa de
  aplicación; este esquema no replica el REPS completo.
