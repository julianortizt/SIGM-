import { api } from './api';

/** Abre el PDF oficial de historia clínica (con hash de integridad) en una pestaña nueva. */
export async function openClinicalRecordPdf(patientId: string) {
  const blob = await api.clinicalRecordPdf(patientId);
  const url = URL.createObjectURL(blob);
  const win = window.open(url, '_blank', 'noopener');
  if (!win) {
    const link = document.createElement('a');
    link.href = url;
    link.download = `historia-clinica-${patientId.slice(0, 8)}.pdf`;
    link.click();
  }
  setTimeout(() => URL.revokeObjectURL(url), 60_000);
}

function esc(value: unknown) {
  return String(value ?? '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;');
}

function fmt(value: unknown) {
  if (!value) return '—';
  const date = new Date(String(value));
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleString('es-CO', { dateStyle: 'medium', timeStyle: 'short' });
}

function age(birth?: string | null) {
  if (!birth) return null;
  const born = new Date(`${birth}T00:00:00`);
  if (Number.isNaN(born.getTime())) return null;
  const now = new Date();
  let years = now.getFullYear() - born.getFullYear();
  const beforeBirthday =
    now.getMonth() < born.getMonth() || (now.getMonth() === born.getMonth() && now.getDate() < born.getDate());
  if (beforeBirthday) years -= 1;
  return years >= 0 ? years : null;
}

function table(headers: string[], rows: string[][]): string {
  if (rows.length === 0) return '<p class="muted">Sin registros.</p>';
  return `<table><thead><tr>${headers.map((h) => `<th>${esc(h)}</th>`).join('')}</tr></thead><tbody>${rows
    .map((row) => `<tr>${row.map((cell) => `<td>${cell}</td>`).join('')}</tr>`)
    .join('')}</tbody></table>`;
}

/**
 * Imprime la historia clínica con el mismo contenido del PDF oficial.
 * Abre una ventana con estilos de impresión y lanza el diálogo del navegador.
 */
export async function printClinicalRecord(patientId: string) {
  const snapshot = await api.clinicalRecord(patientId);
  const patient = snapshot.patient ?? {};
  const patientsAge = age(patient.birthDate);
  const encounters: any[] = snapshot.encounters ?? [];

  const html = `<!doctype html>
<html lang="es"><head><meta charset="utf-8" />
<title>Historia clínica ${esc(patient.patientNumber ?? '')} · ${esc(snapshot.tenantName ?? 'SIGM')}</title>
<style>
  *{box-sizing:border-box}
  body{font-family:"Helvetica Neue",Arial,sans-serif;color:#0f172a;margin:28px;font-size:12px;line-height:1.5}
  header{display:flex;justify-content:space-between;align-items:flex-start;border-bottom:2px solid #0f172a;padding-bottom:10px;margin-bottom:14px}
  h1{font-size:18px;margin:0 0 4px}
  h2{font-size:13px;margin:18px 0 6px;text-transform:uppercase;letter-spacing:.06em;color:#334155}
  .muted{color:#64748b;font-size:11px}
  table{width:100%;border-collapse:collapse;font-size:11.5px}
  th{text-align:left;background:#f1f5f9;padding:5px 6px;border-bottom:1px solid #cbd5e1}
  td{padding:5px 6px;border-bottom:1px solid #e2e8f0;vertical-align:top}
  .grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:4px 18px}
  .chip{display:inline-block;border:1px solid #cbd5e1;border-radius:999px;padding:1px 8px;font-size:10.5px}
  .note{margin-top:6px;padding-left:8px;border-left:2px solid #e2e8f0;white-space:pre-wrap}
  footer{margin-top:22px;border-top:1px solid #cbd5e1;padding-top:8px;font-size:10px;color:#64748b}
  @media print{body{margin:12mm} h2{page-break-after:avoid} tr{page-break-inside:avoid}}
</style></head>
<body>
<header>
  <div>
    <h1>${esc(snapshot.tenantName ?? 'SIGM Cloud')}</h1>
    <div class="muted">${snapshot.tenantTaxId ? `NIT ${esc(snapshot.tenantTaxId)} · ` : ''}Organización ${esc(snapshot.tenantCode ?? '—')}</div>
    <div class="muted">SIGM Cloud · Historia clínica</div>
  </div>
  <div style="text-align:right">
    <div><strong>Historia clínica</strong></div>
    <div class="muted">${esc(patient.patientNumber ?? '—')}</div>
    <div class="muted">Impreso ${esc(fmt(new Date().toISOString()))}</div>
    <div class="muted">Por ${esc(snapshot.generatedBy ?? '—')}</div>
  </div>
</header>

<h2>1. Identificación del paciente</h2>
<div class="grid">
  <div><strong>Nombre:</strong> ${esc(patient.fullName)}</div>
  <div><strong>Documento:</strong> ${esc(patient.documentType ?? '')} ${esc(patient.documentNumber ?? '')}</div>
  <div><strong>Nacimiento:</strong> ${esc(patient.birthDate ?? '—')}${patientsAge != null ? ` (${patientsAge} años)` : ''}</div>
  <div><strong>Sexo:</strong> ${esc(patient.sexAtBirth ?? '—')}</div>
  <div><strong>Teléfono:</strong> ${esc(patient.phone ?? patient.mobilePhone ?? '—')}</div>
  <div><strong>Correo:</strong> ${esc(patient.email ?? '—')}</div>
  <div><strong>Dirección:</strong> ${esc([patient.addressLine, patient.city, patient.department].filter(Boolean).join(', ') || '—')}</div>
  <div><strong>Estado:</strong> ${patient.isActive ? 'Activo' : 'Inactivo'}</div>
</div>

<h2>2. Alertas clínicas (alergias)</h2>
${table(['Alérgeno', 'Tipo', 'Severidad', 'Reacción', 'Estado'], (snapshot.allergies ?? []).map((a: any) => [esc(a.allergen), esc(a.allergenType), esc(a.severity), esc(a.reaction ?? '—'), esc(a.status)]))}

<h2>3. Diagnósticos</h2>
${table(['Código', 'Descripción', 'Tipo', 'Certeza', 'Fecha'], (snapshot.diagnoses ?? []).map((x: any) => [esc(x.code ?? '—'), esc(x.description ?? '—'), esc(x.diagnosisType ?? '—'), esc(x.certainty ?? '—'), esc(x.createdAt ?? '—')]))}

<h2>4. Antecedentes</h2>
${table(['Tipo', 'Descripción', 'Desde', 'Estado'], (snapshot.history ?? []).map((h: any) => [esc(h.historyType), esc(h.description), esc(h.onsetDate ?? '—'), esc(h.status ?? '—')]))}

<h2>5. Signos vitales</h2>
${table(
  ['Fecha', 'Temp °C', 'FC', 'FR', 'PA', 'SpO₂ %', 'Peso kg', 'Talla cm', 'IMC'],
  (snapshot.vitals ?? []).map((v: any) => [
    esc(fmt(v.measuredAt)),
    esc(v.temperatureC ?? '—'),
    esc(v.heartRate ?? '—'),
    esc(v.respiratoryRate ?? '—'),
    `${esc(v.systolicBp ?? '—')}/${esc(v.diastolicBp ?? '—')}`,
    esc(v.oxygenSaturation ?? '—'),
    esc(v.weightKg ?? '—'),
    esc(v.heightCm ?? '—'),
    esc(v.bmi ?? '—'),
  ]),
)}

<h2>6. Atenciones y notas clínicas</h2>
${
  encounters.length === 0
    ? '<p class="muted">Sin atenciones registradas.</p>'
    : encounters
        .map((e: any) => {
          const notes: any[] = e.notes ?? [];
          return `<div style="margin-bottom:12px">
            <div><strong>${esc(fmt(e.encounterDate))}</strong> · ${esc(e.encounterType ?? '')} · ${esc(e.professionalName ?? 'Profesional no asignado')} <span class="chip">${esc(e.status ?? '')}</span></div>
            ${e.specialtyName ? `<div class="muted">Especialidad: ${esc(e.specialtyName)}</div>` : ''}
            <div class="muted">Motivo: ${esc(e.chiefComplaint ?? '—')}</div>
            ${notes
              .map(
                (n: any) => `<div class="note">
                  <div class="muted">${esc(n.noteType ?? '')} · ${esc(n.status ?? '')} · v${esc(n.currentVersion ?? 1)}${n.signedAt ? ` · firmada ${esc(fmt(n.signedAt))}` : ''}</div>
                  <div>${esc(n.content ?? '')}</div>
                  ${(n.amendments ?? [])
                    .map((a: any) => `<div class="muted">Adenda ${esc(a.number)}: ${esc(a.reason)} — ${esc(a.content)}</div>`)
                    .join('')}
                </div>`,
              )
              .join('')}
            ${notes.length === 0 ? '<div class="muted">(sin notas registradas)</div>' : ''}
          </div>`;
        })
        .join('')
}

<h2>7. Fórmulas médicas</h2>
${table(
  ['Fecha', 'Número', 'Medicamento', 'Dosis', 'Frecuencia', 'Vía', 'Duración'],
  (snapshot.prescriptions ?? []).flatMap((p: any) =>
    (p.items ?? []).map((i: any) => [
      esc(fmt(p.issuedAt)),
      esc(p.number ?? '—'),
      esc(i.medicationName),
      esc(i.dose ?? '—'),
      esc(i.frequency ?? '—'),
      esc(i.route ?? '—'),
      i.durationDays != null ? `${esc(i.durationDays)} días` : '—',
    ]),
  ),
)}

<h2>8. Resultados de laboratorio</h2>
${table(
  ['Fecha', 'Orden', 'Prueba', 'Resultado', 'Unidad', 'Estado'],
  (snapshot.labOrders ?? []).flatMap((o: any) =>
    (o.tests ?? []).map((t: any) => [
      esc(fmt(o.orderedAt)),
      esc(o.orderNumber ?? '—'),
      esc(t.name),
      esc(t.result ?? '—'),
      esc(t.unit ?? '—'),
      esc(t.status ?? '—'),
    ]),
  ),
)}

<footer>
  Documento generado por SIGM Cloud · Por ${esc(snapshot.generatedBy ?? '—')}.
  La impresión y el PDF comparten el mismo contenido; el PDF incluye el hash SHA-256 para verificar su integridad.
</footer>
<script>window.addEventListener('load', function () { setTimeout(function () { window.print(); }, 250); });</script>
</body></html>`;

  const win = window.open('', '_blank');
  if (!win) {
    const blob = new Blob([html], { type: 'text/html' });
    window.location.href = URL.createObjectURL(blob);
    return;
  }
  win.document.open();
  win.document.write(html);
  win.document.close();
}
