#!/usr/bin/env python3
# ============================================================================
# SIGM Cloud - Importador de catalogos oficiales de SISPRO
#
# Fuente autorizada:
#   https://web.sispro.gov.co/WebPublico/Consultas/ConsultarReferenciaBasica.aspx
#
# El script descarga las tablas de referencia CIE10, CUPS e IUM, las normaliza
# a CSV UTF-8 y genera seeds SQL idempotentes con COPY ... FROM STDIN para
# cargarlas en PostgreSQL. No requiere dependencias externas: usa solo la
# biblioteca estandar de Python 3.10+.
#
# Uso:
#   python import_catalogs.py                # descarga, normaliza y genera SQL
#   python import_catalogs.py --skip-scrape  # regenera SQL desde los CSV
#   python import_catalogs.py --only CIE10,CUPS
#   python import_catalogs.py --check        # valida los CSV existentes
# ============================================================================
from __future__ import annotations

import argparse
import csv
import hashlib
import html as htmlmod
import http.cookiejar
import json
import re
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path

BASE_URL = (
    "https://web.sispro.gov.co/WebPublico/Consultas/"
    "ConsultarDetalleReferenciaBasica.aspx"
)
USER_AGENT = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) SigmCatalogImporter/1.0"
GRID_ID = "ctl00_cntContenido_grvTablaReferencia"
TARGET_SIZE = f"ctl00$cntContenido$dpggrvTablaReferencia$ddlPageSize"
TARGET_NEXT = f"ctl00$cntContenido$dpggrvTablaReferencia$imgNext"
SIZE_FIELD = f"ctl00$cntContenido$dpggrvTablaReferencia$ddlPageSize"


@dataclass(frozen=True)
class Catalog:
    code: str
    label: str
    csv_name: str
    seed_name: str
    minimum_rows: int
    columns: tuple[str, ...]


CATALOGS = {
    "CIE10": Catalog(
        code="CIE10",
        label="CIE-10 Colombia",
        csv_name="cie10.csv",
        seed_name="006_catalogs_cie10.sql",
        minimum_rows=12_000,
        columns=("code", "description", "chapter", "chapter_code", "group_code", "is_active"),
    ),
    "CUPS": Catalog(
        code="CUPS",
        label="CUPS",
        csv_name="cups.csv",
        seed_name="007_catalogs_cups.sql",
        minimum_rows=9_500,
        columns=("code", "name", "chapter", "is_active"),
    ),
    "IUM": Catalog(
        code="IUM",
        label="IUM (medicamentos)",
        csv_name="ium.csv",
        seed_name="008_catalogs_ium.sql",
        minimum_rows=33_000,
        columns=(
            "code",
            "name",
            "presentation",
            "concentration",
            "pharmaceutical_form",
            "route",
            "is_active",
        ),
    ),
}


def log(message: str) -> None:
    print(message, flush=True)


def decode(payload: bytes) -> str:
    """SISPRO responde en windows-1252; algunos metadatos estan en UTF-8."""
    try:
        return payload.decode("utf-8")
    except UnicodeDecodeError:
        return payload.decode("cp1252", errors="replace")


def clean(value: str | None) -> str:
    if not value:
        return ""
    value = htmlmod.unescape(value)
    value = value.replace("\xa0", " ").replace("\r", " ").replace("\n", " ")
    return re.sub(r"\s+", " ", value).strip()


def extract_hidden(page: str) -> dict[str, str]:
    fields: dict[str, str] = {}
    patterns = (
        r'<input[^>]*type="hidden"[^>]*name="([^"]+)"[^>]*value="([^"]*)"',
        r'<input[^>]*name="([^"]+)"[^>]*type="hidden"[^>]*value="([^"]*)"',
    )
    for pattern in patterns:
        for match in re.finditer(pattern, page):
            fields[match.group(1)] = clean(match.group(2))
    return fields


def extract_rows(page: str) -> list[list[str]]:
    block = re.search(
        r'(?s)<table[^>]*id="' + re.escape(GRID_ID) + r'".*?</table>', page
    )
    if not block:
        return []
    rows: list[list[str]] = []
    for row in re.findall(r"(?s)<tr[^>]*>(.*?)</tr>", block.group(0)):
        if "<td" not in row:
            continue
        cells = []
        for cell in re.finditer(r"(?s)<td[^>]*>(.*?)</td>", row):
            text = re.sub(r"(?s)<[^>]+>", "", cell.group(1))
            cells.append(clean(text))
        if len(cells) >= 5:
            rows.append(cells)
    return rows


def extract_total(page: str) -> int:
    match = re.search(r'class="lblRowCount">([^<]*)<', page)
    if not match:
        return 0
    digits = re.sub(r"[^0-9]", "", match.group(1))
    return int(digits) if digits else 0


def chapter_code(code: str) -> str:
    """Mapea el codigo CIE-10 al capitulo romano de la CIE-10 de la OMS."""
    code = code.upper()
    if not code:
        return ""
    letter = code[0]
    try:
        numeric = int(re.sub(r"[^0-9]", "", code[:3]) or "0")
    except ValueError:
        numeric = 0
    if letter in ("A", "B"):
        return "I"
    if letter == "C":
        return "II"
    if letter == "D":
        return "II" if numeric <= 48 else "III"
    if letter == "E":
        return "IV"
    if letter == "F":
        return "V"
    if letter == "G":
        return "VI"
    if letter == "H":
        return "VII" if numeric <= 59 else "VIII"
    if letter == "I":
        return "IX"
    if letter == "J":
        return "X"
    if letter == "K":
        return "XI"
    if letter == "L":
        return "XII"
    if letter == "M":
        return "XIII"
    if letter == "N":
        return "XIV"
    if letter == "O":
        return "XV"
    if letter == "P":
        return "XVI"
    if letter == "Q":
        return "XVII"
    if letter == "R":
        return "XVIII"
    if letter in ("S", "T"):
        return "XIX"
    if letter in ("V", "W", "X", "Y"):
        return "XX"
    if letter == "Z":
        return "XXI"
    if letter == "U":
        return "XXII"
    return ""


def normalize(catalog: Catalog, rows: list[list[str]]) -> list[dict[str, str]]:
    records: list[dict[str, str]] = []
    for row in rows:
        code = clean(row[1]).upper().replace(" ", "")
        if not code:
            continue
        active = clean(row[4]).upper() in ("SI", "SÍ", "TRUE", "1")
        if catalog.code == "CIE10":
            # En la tabla CIE10 de SISPRO, Extra_V trae el nombre del capitulo
            # y Extra_VI:Capitulo trae el grupo de tres caracteres (p. ej. A00).
            records.append(
                {
                    "code": code,
                    "description": clean(row[2]),
                    "chapter": clean(row[12]),
                    "chapter_code": chapter_code(code),
                    "group_code": clean(row[13]),
                    "is_active": "true" if active else "false",
                }
            )
        elif catalog.code == "CUPS":
            records.append(
                {
                    "code": code,
                    "name": clean(row[2]),
                    "chapter": clean(row[3]) or clean(row[13]) or clean(row[16]),
                    "is_active": "true" if active else "false",
                }
            )
        elif catalog.code == "IUM":
            # La tabla de SISPRO incluye una fila centinela ("CERO") que no es
            # un IUM valido; los IUM reales tienen 15 caracteres alfanumericos.
            if len(code) < 8:
                continue
            records.append(
                {
                    "code": code,
                    "name": clean(row[2]),
                    "presentation": clean(row[17]),
                    "concentration": "",
                    "pharmaceutical_form": clean(row[11]),
                    "route": "",
                    "is_active": "true" if active else "false",
                }
            )
    return records


class SisproClient:
    def __init__(self, delay: float = 0.5, retries: int = 4) -> None:
        self.delay = delay
        self.retries = retries
        self.jar = http.cookiejar.CookieJar()
        self.opener = urllib.request.build_opener(
            urllib.request.HTTPCookieProcessor(self.jar)
        )

    def _request(self, url: str, data: bytes | None = None) -> str:
        headers = {
            "User-Agent": USER_AGENT,
            "Accept": "text/html,application/xhtml+xml",
            "Referer": url,
        }
        if data is not None:
            headers["Content-Type"] = "application/x-www-form-urlencoded"
        last_error: Exception | None = None
        for attempt in range(1, self.retries + 1):
            try:
                request = urllib.request.Request(url, data=data, headers=headers)
                with self.opener.open(request, timeout=240) as response:
                    return decode(response.read())
            except (urllib.error.URLError, TimeoutError, ConnectionError) as exc:
                last_error = exc
                wait = min(2 ** attempt, 15)
                log(f"    reintento {attempt}/{self.retries} en {wait}s: {exc}")
                time.sleep(wait)
        raise RuntimeError(f"No fue posible descargar {url}: {last_error}")

    def post(
        self, url: str, page: str, target: str, page_size: int
    ) -> str:
        fields = extract_hidden(page)
        fields["__EVENTTARGET"] = target
        fields["__EVENTARGUMENT"] = ""
        fields[SIZE_FIELD] = str(page_size)
        body = urllib.parse.urlencode(fields).encode("utf-8")
        return self._request(url, body)

    def scrape(self, catalog: Catalog, page_size: int) -> list[list[str]]:
        url = f"{BASE_URL}?Code={catalog.code}"
        log(f"  GET {url}")
        page = self._request(url)
        total = extract_total(page)
        if total == 0:
            raise RuntimeError(
                f"SISPRO no devolvio registros para {catalog.code}. "
                "Verifique el codigo de la tabla de referencia."
            )
        log(f"  {catalog.label}: {total:,} registros declarados")

        page = self.post(url, page, TARGET_SIZE, page_size)
        rows = extract_rows(page)
        log(f"  pagina 1: {len(rows):,} filas")

        seen_codes = {clean(row[1]) for row in rows if len(row) > 1}
        page_number = 1
        while len(rows) < total:
            page_number += 1
            time.sleep(self.delay)
            next_page = self.post(url, page, TARGET_NEXT, page_size)
            new_rows = extract_rows(next_page)
            if not new_rows:
                break
            first_code = clean(new_rows[0][1]) if len(new_rows[0]) > 1 else ""
            if first_code and first_code in seen_codes:
                log("    la paginacion devolvio una pagina repetida; se detiene")
                break
            rows.extend(new_rows)
            seen_codes.update(
                clean(row[1]) for row in new_rows if len(row) > 1
            )
            log(f"  pagina {page_number}: {len(new_rows):,} filas "
                f"(acumulado {len(rows):,}/{total:,})")
            page = next_page

        if len(rows) < total:
            log(f"  ADVERTENCIA: se obtuvieron {len(rows):,} de {total:,} filas")
        return rows


def deduplicate(records: list[dict[str, str]]) -> tuple[list[dict[str, str]], int]:
    unique: dict[str, dict[str, str]] = {}
    duplicates = 0
    for record in records:
        code = record["code"]
        if code in unique:
            duplicates += 1
            # Conserva la descripcion mas rica.
            current = unique[code]
            current_text = current.get("description") or current.get("name", "")
            new_text = record.get("description") or record.get("name", "")
            if len(new_text) > len(current_text):
                unique[code] = record
            continue
        unique[code] = record
    return list(unique.values()), duplicates


def write_csv(catalog: Catalog, records: list[dict[str, str]], path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(
            handle,
            fieldnames=list(catalog.columns),
            extrasaction="ignore",
            lineterminator="\n",
        )
        writer.writeheader()
        writer.writerows(records)


def read_csv(catalog: Catalog, path: Path) -> list[dict[str, str]]:
    if not path.exists():
        raise FileNotFoundError(f"No existe {path}; ejecute primero la descarga.")
    with path.open("r", encoding="utf-8", newline="") as handle:
        reader = csv.DictReader(handle)
        return [dict(row) for row in reader]


def sql_literal(value: str) -> str:
    return "'" + value.replace("'", "''") + "'"


def write_seed(catalog: Catalog, records: list[dict[str, str]], path: Path) -> None:
    """Genera un seed idempotente con COPY FROM STDIN y upsert."""
    path.parent.mkdir(parents=True, exist_ok=True)
    stage = "_seed_" + catalog.code.lower()
    columns = list(catalog.columns)
    column_list = ", ".join(columns)

    if catalog.code == "CIE10":
        table = "sigm.diagnosis_catalog"
        target_columns = "code, description, chapter, chapter_code, group_code, is_active"
        updates = (
            "description = EXCLUDED.description,\n"
            "    chapter = EXCLUDED.chapter,\n"
            "    chapter_code = EXCLUDED.chapter_code,\n"
            "    group_code = EXCLUDED.group_code,\n"
            "    is_active = EXCLUDED.is_active,\n"
            "    updated_at = now()"
        )
    elif catalog.code == "CUPS":
        table = "sigm.procedure_catalog"
        target_columns = "code, name, chapter, is_active"
        updates = (
            "name = EXCLUDED.name,\n"
            "    chapter = EXCLUDED.chapter,\n"
            "    is_active = EXCLUDED.is_active,\n"
            "    updated_at = now()"
        )
    elif catalog.code == "IUM":
        table = "sigm.medication_catalog"
        target_columns = (
            "code, name, presentation, concentration, "
            "pharmaceutical_form, route, is_active"
        )
        updates = (
            "name = EXCLUDED.name,\n"
            "    presentation = EXCLUDED.presentation,\n"
            "    concentration = EXCLUDED.concentration,\n"
            "    pharmaceutical_form = EXCLUDED.pharmaceutical_form,\n"
            "    route = EXCLUDED.route,\n"
            "    is_active = EXCLUDED.is_active,\n"
            "    updated_at = now()"
        )
    else:  # pragma: no cover - defensivo
        raise ValueError(catalog.code)

    stamp = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    with path.open("w", encoding="utf-8", newline="\n") as handle:
        handle.write("-- ============================================================================\n")
        handle.write(f"-- SIGM CLOUD - Catalogo oficial {catalog.label}\n")
        handle.write("-- GENERADO AUTOMATICAMENTE POR import/import_catalogs.py. NO EDITAR A MANO.\n")
        handle.write(f"-- Fuente: {BASE_URL}?Code={catalog.code}\n")
        handle.write(f"-- Descargado: {stamp}\n")
        handle.write(f"-- Registros: {len(records)}\n")
        handle.write("-- ============================================================================\n")
        handle.write("\\encoding UTF8\n")
        handle.write("BEGIN;\n\n")
        handle.write(f"CREATE TEMP TABLE {stage} (\n")
        handle.write("    " + ",\n    ".join(
            f"{name} {'boolean' if name == 'is_active' else 'text'}"
            for name in columns
        ) + "\n")
        handle.write(") ON COMMIT DROP;\n\n")
        handle.write(
            f"COPY {stage} ({column_list}) FROM STDIN "
            "WITH (FORMAT csv, HEADER false, NULL '');\n"
        )
        writer = csv.writer(handle, lineterminator="\n")
        for record in records:
            writer.writerow([record.get(name, "") for name in columns])
        handle.write("\\.\n\n")
        handle.write(
            f"INSERT INTO {table} ({target_columns})\n"
            f"SELECT {target_columns} FROM {stage}\n"
            "ON CONFLICT (code) DO UPDATE SET\n"
            f"    {updates};\n\n"
        )
        handle.write("COMMIT;\n")


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def validate(catalog: Catalog, records: list[dict[str, str]], duplicates: int) -> None:
    if len(records) < catalog.minimum_rows:
        raise RuntimeError(
            f"{catalog.label}: {len(records):,} registros, "
            f"por debajo del minimo esperado de {catalog.minimum_rows:,}. "
            "No se generan seeds para evitar catalogos incompletos."
        )
    key = "description" if catalog.code == "CIE10" else "name"
    empty = sum(1 for record in records if not record.get(key, "").strip())
    if empty:
        raise RuntimeError(
            f"{catalog.label}: {empty:,} registros sin {key}. "
            "Verifique la estructura de la tabla de SISPRO."
        )
    log(f"  OK: {len(records):,} registros unicos "
        f"({duplicates:,} duplicados descartados)")


def main() -> int:
    parser = argparse.ArgumentParser(description="Importa catalogos oficiales de SISPRO.")
    parser.add_argument(
        "--out-dir",
        default=str(Path(__file__).resolve().parent / "data"),
        help="Directorio de salida para los CSV.",
    )
    parser.add_argument(
        "--seeds-dir",
        default=str(Path(__file__).resolve().parent.parent / "seeds"),
        help="Directorio de salida para los seeds SQL.",
    )
    parser.add_argument(
        "--only",
        default="CIE10,CUPS,IUM",
        help="Catalogos a procesar separados por coma (CIE10,CUPS,IUM).",
    )
    parser.add_argument("--page-size", type=int, default=2000)
    parser.add_argument("--delay", type=float, default=0.5)
    parser.add_argument("--skip-scrape", action="store_true")
    parser.add_argument("--skip-seeds", action="store_true")
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()

    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")

    out_dir = Path(args.out_dir)
    seeds_dir = Path(args.seeds_dir)
    out_dir.mkdir(parents=True, exist_ok=True)
    seeds_dir.mkdir(parents=True, exist_ok=True)

    requested = [item.strip().upper() for item in args.only.split(",") if item.strip()]
    unknown = [item for item in requested if item not in CATALOGS]
    if unknown:
        raise SystemExit(f"Catalogos desconocidos: {', '.join(unknown)}")

    client = SisproClient(delay=args.delay) if not args.skip_scrape else None
    database_root = Path(__file__).resolve().parent.parent

    def relative(path: Path) -> str:
        try:
            return path.resolve().relative_to(database_root).as_posix()
        except ValueError:
            return path.as_posix()

    manifest: dict[str, object] = {
        "source": BASE_URL,
        "retrieved_at": datetime.now(timezone.utc).isoformat(),
        "catalogs": {},
    }

    for code in requested:
        catalog = CATALOGS[code]
        log(f"\n== {catalog.label} ==")
        csv_path = out_dir / catalog.csv_name

        if args.skip_scrape or args.check:
            rows = read_csv(catalog, csv_path) if csv_path.exists() else []
            records = rows
            duplicates = 0
        else:
            assert client is not None
            raw_rows = client.scrape(catalog, args.page_size)
            records, duplicates = deduplicate(normalize(catalog, raw_rows))
            write_csv(catalog, records, csv_path)

        validate(catalog, records, duplicates)
        info = {
            "label": catalog.label,
            "source": f"{BASE_URL}?Code={catalog.code}",
            "rows": len(records),
            "duplicates_discarded": duplicates,
            "csv": relative(csv_path),
            "sha256": sha256(csv_path) if csv_path.exists() else None,
            "columns": list(catalog.columns),
            "minimum_rows": catalog.minimum_rows,
        }
        manifest["catalogs"][code] = info  # type: ignore[index]
        log(f"  CSV: {csv_path}")

        if not args.check and not args.skip_seeds:
            seed_path = seeds_dir / catalog.seed_name
            write_seed(catalog, records, seed_path)
            info["seed"] = relative(seed_path)
            log(f"  SQL: {seed_path}")

    manifest_path = out_dir / "manifest.json"
    if not args.check:
        manifest_path.write_text(
            json.dumps(manifest, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
        )
        log(f"\nManifiesto: {manifest_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
