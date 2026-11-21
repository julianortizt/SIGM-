import { useCallback, useEffect, useRef, useState } from 'react';
import {
  api,
  ApiError,
  downloadBlob,
  type DbBackup,
  type DbScript,
  type DbScriptDetail,
  type DbStatus,
} from '../api';

const STATUS_META: Record<string, { label: string; tone: string }> = {
  PENDING: { label: 'Cargado', tone: 'slate' },
  VALIDATED: { label: 'Validado', tone: 'blue' },
  APPLIED: { label: 'Aplicado', tone: 'green' },
  FAILED: { label: 'Con error', tone: 'red' },
};

function formatDate(value?: string | null) {
  if (!value) return '—';
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleString('es-CO', { dateStyle: 'medium', timeStyle: 'short' });
}

function formatSize(bytes?: number | null) {
  if (!bytes || bytes < 0) return '—';
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / 1024 / 1024).toFixed(2)} MB`;
}

export default function DatabaseView() {
  const [status, setStatus] = useState<DbStatus | null>(null);
  const [scripts, setScripts] = useState<DbScript[]>([]);
  const [backups, setBackups] = useState<DbBackup[]>([]);
  const [backupPath, setBackupPath] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [busy, setBusy] = useState<string | null>(null);

  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [file, setFile] = useState<File | null>(null);
  const fileInput = useRef<HTMLInputElement | null>(null);

  const [detail, setDetail] = useState<DbScriptDetail | null>(null);
  const [applyTarget, setApplyTarget] = useState<DbScript | null>(null);
  const [confirmation, setConfirmation] = useState('');
  const [password, setPassword] = useState('');
  const [approverEmail, setApproverEmail] = useState('');
  const [approverPassword, setApproverPassword] = useState('');
  const [skipBackup, setSkipBackup] = useState(false);
  const [applyNote, setApplyNote] = useState('');

  const refresh = useCallback(async () => {
    setError(null);
    try {
      const [statusRes, scriptsRes, backupsRes] = await Promise.all([
        api.dbStatus(),
        api.dbScripts(),
        api.dbBackups(),
      ]);
      setStatus(statusRes);
      setScripts(scriptsRes);
      setBackups(backupsRes.items);
      setBackupPath(backupsRes.backupPath);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No fue posible consultar la base de datos.');
    }
  }, []);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  const run = async (key: string, action: () => Promise<void>) => {
    setBusy(key);
    setError(null);
    setNotice(null);
    try {
      await action();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : err instanceof Error ? err.message : 'La operación falló.');
    } finally {
      setBusy(null);
    }
  };

  const onUpload = (event: React.FormEvent) => {
    event.preventDefault();
    if (!file) {
      setError('Seleccione un archivo .sql.');
      return;
    }
    void run('upload', async () => {
      const form = new FormData();
      form.append('file', file);
      form.append('name', name.trim() || file.name.replace(/\.sql$/i, ''));
      if (description.trim()) form.append('description', description.trim());
      const result = await api.dbUploadScript(form);
      const analysis = result.analysis;
      setNotice(
        analysis.allowed
          ? `Script cargado y analizado: ${analysis.statements} sentencia(s).${analysis.destructive ? ' Marcado como destructivo.' : ''}`
          : `Script cargado, pero el análisis lo rechaza: ${analysis.reason}`,
      );
      setName('');
      setDescription('');
      setFile(null);
      if (fileInput.current) fileInput.current.value = '';
      await refresh();
    });
  };

  const validate = (script: DbScript) =>
    void run(`validate-${script.id}`, async () => {
      const result = await api.dbValidate(script.id);
      setNotice(result.message);
      await refresh();
    });

  const destroy = (script: DbScript) =>
    void run(`delete-${script.id}`, async () => {
      await api.dbDeleteScript(script.id);
      setNotice('Script eliminado del historial de pendientes.');
      await refresh();
    });

  const openDetail = (script: DbScript) =>
    void run(`detail-${script.id}`, async () => {
      setDetail(await api.dbScript(script.id));
    });

  const openApply = (script: DbScript) => {
    setApplyTarget(script);
    setConfirmation('');
    setPassword('');
    setApproverEmail('');
    setApproverPassword('');
    setSkipBackup(false);
    setApplyNote('');
    setError(null);
  };

  const submitApply = (event: React.FormEvent) => {
    event.preventDefault();
    if (!applyTarget) return;
    const target = applyTarget;
    const needsApproval = target.destructive && !target.approvedAt;
    if (needsApproval && (!approverEmail.trim() || !approverPassword)) {
      setError('Este script destructivo necesita la aprobación de otro administrador de plataforma: indique su correo y contraseña.');
      return;
    }
    void run('apply', async () => {
      if (needsApproval) {
        await api.dbApprove(target.id, {
          email: approverEmail.trim(),
          password: approverPassword,
          note: applyNote.trim() || undefined,
        });
      }
      const result = await api.dbApply(target.id, {
        confirmation: confirmation.trim(),
        password,
        skipBackup,
        note: applyNote.trim() || undefined,
      });
      const verification = result.backupVerification?.message ? ` ${result.backupVerification.message}` : '';
      setNotice(result.message + (result.backup ? ` Respaldo: ${result.backup.fileName}.${verification}` : ' Sin respaldo (omitido).'));
      setApplyTarget(null);
      await refresh();
    });
  };

  const createBackup = () =>
    void run('backup', async () => {
      const result = await api.dbCreateBackup('Respaldo manual desde la consola');
      setNotice(`${result.message} Archivo: ${result.backup.fileName} (${formatSize(result.backup.sizeBytes)}).`);
      await refresh();
    });

  const verifyBackup = (backup: DbBackup) =>
    void run(`verify-${backup.id}`, async () => {
      const result = await api.dbVerifyBackup(backup.id);
      setNotice(`${result.valid ? 'Respaldo válido.' : 'Respaldo con problemas.'} ${result.message}`);
    });

  const downloadBackup = (backup: DbBackup) =>
    void run(`download-${backup.id}`, async () => {
      const blob = await downloadBlob(`/api/v1/platform/database/backups/${backup.id}/download`);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = backup.fileName;
      link.click();
      URL.revokeObjectURL(url);
    });

  return (
    <>
      {error && <div className="alert error">{error}</div>}
      {notice && <div className="alert ok">{notice}</div>}

      <div className="stats">
        <div className="stat">
          <div className="stat-ico blue">
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
              <ellipse cx="12" cy="5.5" rx="7.5" ry="3" />
              <path d="M4.5 5.5v13c0 1.7 3.4 3 7.5 3s7.5-1.3 7.5-3v-13M4.5 12c0 1.7 3.4 3 7.5 3s7.5-1.3 7.5-3" />
            </svg>
          </div>
          <div>
            <div className="stat-val">{status?.database ?? '—'}</div>
            <div className="stat-lbl">Base de datos · {status?.size ?? '—'}</div>
          </div>
        </div>
        <div className="stat">
          <div className="stat-ico green">
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
              <path d="M4 6h16M4 12h16M4 18h10" strokeLinecap="round" />
            </svg>
          </div>
          <div>
            <div className="stat-val">{status?.schemaTables ?? '—'}</div>
            <div className="stat-lbl">Tablas en el esquema sigm</div>
          </div>
        </div>
        <div className="stat">
          <div className="stat-ico amber">
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
              <path d="M7 4h7l4 4v12H7zM14 4v4h4" strokeLinejoin="round" />
            </svg>
          </div>
          <div>
            <div className="stat-val">{status?.scripts ?? scripts.length}</div>
            <div className="stat-lbl">Scripts registrados</div>
          </div>
        </div>
        <div className="stat">
          <div className="stat-ico violet">
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
              <path d="M12 3v12m0 0l-4-4m4 4l4-4M4 17v3h16v-3" strokeLinecap="round" strokeLinejoin="round" />
            </svg>
          </div>
          <div>
            <div className="stat-val">{backups.length}</div>
            <div className="stat-lbl">Respaldos · pg_dump {status?.pgDumpAvailable ? 'disponible' : 'no disponible'}</div>
          </div>
        </div>
      </div>

      <section className="panel">
        <div className="panel-h">
          <div>
            <div className="panel-t">Cargar script SQL</div>
            <div className="panel-d">
              Todo script se versiona con hash y se analiza antes de ejecutarse. Se rechazan sentencias
              catastróficas (DROP DATABASE, COPY PROGRAM, GRANT, meta-comandos de psql, etc.).
            </div>
          </div>
          <span className="badge amber">Acción auditada</span>
        </div>
        <div className="panel-b">
          <form onSubmit={onUpload}>
            <div className="grid2">
              <label className="field">
                <span>Nombre del script</span>
                <input
                  type="text"
                  value={name}
                  maxLength={160}
                  onChange={(e) => setName(e.target.value)}
                  placeholder="p. ej. 021_indices_pacientes"
                />
              </label>
              <label className="field">
                <span>Archivo .sql (máx. 2 MB)</span>
                <input
                  ref={fileInput}
                  type="file"
                  accept=".sql,text/plain,application/sql"
                  onChange={(e) => setFile(e.target.files?.[0] ?? null)}
                />
              </label>
            </div>
            <label className="field" style={{ marginTop: 12 }}>
              <span>Descripción</span>
              <input
                type="text"
                value={description}
                maxLength={300}
                onChange={(e) => setDescription(e.target.value)}
                placeholder="Qué hace este script y por qué se aplica"
              />
            </label>
            <div className="row" style={{ marginTop: 14 }}>
              <button className="btn" type="submit" disabled={busy === 'upload' || !file}>
                {busy === 'upload' ? 'Cargando…' : 'Cargar y analizar'}
              </button>
              <button className="btn ghost sm" type="button" onClick={() => void refresh()} disabled={busy !== null}>
                Actualizar
              </button>
            </div>
            <div className="hint" style={{ marginTop: 10 }}>
              Los scripts deben ser SQL estándar sin BEGIN/COMMIT ni archivos de volcado con COPY ... FROM stdin.
            </div>
          </form>
        </div>
      </section>

      <section className="panel">
        <div className="panel-h">
          <div>
            <div className="panel-t">Scripts cargados</div>
            <div className="panel-d">Validar ejecuta el script dentro de una transacción que siempre se revierte.</div>
          </div>
        </div>
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Script</th>
                <th>Estado</th>
                <th>Sentencias</th>
                <th>Cargado</th>
                <th>Acciones</th>
              </tr>
            </thead>
            <tbody>
              {scripts.length === 0 && (
                <tr>
                  <td colSpan={5}>
                    <div className="empty">Todavía no hay scripts cargados.</div>
                  </td>
                </tr>
              )}
              {scripts.map((script) => {
                const meta = STATUS_META[script.status] ?? { label: script.status, tone: 'slate' };
                return (
                  <tr key={script.id}>
                    <td>
                      <div className="strong">{script.name}</div>
                      <div className="muted" style={{ fontSize: 12 }}>
                        {script.description || 'Sin descripción'} · {formatSize(script.sizeBytes)}
                        {script.destructive && <span className="badge red" style={{ marginLeft: 8 }}>Destructivo</span>}
                        {script.destructive && (
                          <span className={`badge ${script.approvedAt ? 'green' : 'amber'}`} style={{ marginLeft: 6 }}>
                            {script.approvedAt ? 'Aprobado' : 'Sin aprobación'}
                          </span>
                        )}
                      </div>
                    </td>
                    <td>
                      <span className={`badge ${meta.tone}`}>{meta.label}</span>
                      {script.lastError && (
                        <div className="muted" style={{ fontSize: 12, maxWidth: 260 }}>{script.lastError}</div>
                      )}
                    </td>
                    <td className="num">{script.statements}</td>
                    <td>{formatDate(script.createdAt)}</td>
                    <td>
                      <div className="row">
                        <button className="btn ghost sm" type="button" onClick={() => openDetail(script)}>
                          Ver
                        </button>
                        <button
                          className="btn subtle sm"
                          type="button"
                          disabled={busy !== null || script.status === 'APPLIED'}
                          onClick={() => validate(script)}
                        >
                          {busy === `validate-${script.id}` ? 'Validando…' : 'Validar'}
                        </button>
                        <button
                          className="btn sm"
                          type="button"
                          disabled={busy !== null || script.status === 'APPLIED'}
                          onClick={() => openApply(script)}
                        >
                          Aplicar
                        </button>
                        {script.status !== 'APPLIED' && (
                          <button
                            className="btn danger sm"
                            type="button"
                            disabled={busy !== null}
                            onClick={() => destroy(script)}
                          >
                            Quitar
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </section>

      <section className="panel">
        <div className="panel-h">
          <div>
            <div className="panel-t">Respaldos</div>
            <div className="panel-d">
              Se crea un respaldo verificado antes de cada aplicación.
              {status?.autoBackupEnabled
                ? ` Respaldo automático cada ${status.autoBackupIntervalHours ?? 24} h; último: ${formatDate(status.lastAutomaticBackupAt)}.`
                : ' El respaldo automático programado está desactivado.'}{' '}
              Ruta: {backupPath || '—'}
            </div>
          </div>
          <button className="btn subtle sm" type="button" onClick={() => createBackup()} disabled={busy !== null}>
            {busy === 'backup' ? 'Respaldando…' : 'Crear respaldo ahora'}
          </button>
        </div>
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Archivo</th>
                <th>Tamaño</th>
                <th>Nota</th>
                <th>Fecha</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {backups.length === 0 && (
                <tr>
                  <td colSpan={5}>
                    <div className="empty">Sin respaldos registrados.</div>
                  </td>
                </tr>
              )}
              {backups.map((backup) => (
                <tr key={backup.id}>
                  <td className="strong" style={{ fontSize: 12.5 }}>{backup.fileName}</td>
                  <td>{formatSize(backup.sizeBytes)}</td>
                  <td>{backup.note ?? '—'}</td>
                  <td>{formatDate(backup.createdAt)}</td>
                  <td>
                    <div style={{ display: 'flex', gap: 6 }}>
                      <button
                        className="btn ghost sm"
                        type="button"
                        disabled={busy !== null}
                        onClick={() => verifyBackup(backup)}
                      >
                        {busy === `verify-${backup.id}` ? 'Verificando…' : 'Verificar'}
                      </button>
                      <button
                        className="btn ghost sm"
                        type="button"
                        disabled={busy !== null}
                        onClick={() => downloadBackup(backup)}
                      >
                        Descargar
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      {detail && (
        <div className="modal-backdrop" role="presentation" onClick={() => setDetail(null)}>
          <div className="modal" role="dialog" aria-modal="true" onClick={(e) => e.stopPropagation()} style={{ maxWidth: 860 }}>
            <div className="modal-h">
              <div>
                <div className="modal-t">{detail.script.name}</div>
                <div className="modal-d">Checksum {detail.script.checksum.slice(0, 16)}… · {detail.script.statements} sentencias</div>
              </div>
              <button className="icon-btn" type="button" aria-label="Cerrar" onClick={() => setDetail(null)}>×</button>
            </div>
            <div className="modal-b">
              <pre
                style={{
                  background: '#0f172a',
                  color: '#e2e8f0',
                  padding: 14,
                  borderRadius: 10,
                  maxHeight: 300,
                  overflow: 'auto',
                  fontSize: 12,
                  lineHeight: 1.55,
                }}
              >
                {detail.script.sqlText}
              </pre>
              <div className="panel-sec-t">Historial</div>
              {detail.runs.length === 0 && <div className="empty">Sin ejecuciones registradas.</div>}
              {detail.runs.map((run, index) => (
                <div key={index} className="module-row">
                  <div className="grow">
                    <div className="module-name">
                      {run.mode === 'APPLY' ? 'Aplicación' : 'Validación'}{' '}
                      <span className={`badge ${run.success ? 'green' : 'red'}`}>{run.success ? 'Correcta' : 'Fallida'}</span>
                    </div>
                    <div className="module-desc">{run.message || '—'}</div>
                    <div className="module-code">{formatDate(run.executedAt)} · {run.durationMs} ms</div>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      )}

      {applyTarget && (
        <div className="modal-backdrop" role="presentation" onClick={() => setApplyTarget(null)}>
          <div className="modal" role="dialog" aria-modal="true" onClick={(e) => e.stopPropagation()}>
            <div className="modal-h">
              <div>
                <div className="modal-t">Aplicar «{applyTarget.name}»</div>
                <div className="modal-d">
                  {applyTarget.destructive
                    ? applyTarget.approvedAt
                      ? 'Script destructivo ya aprobado por otro administrador. El respaldo es obligatorio.'
                      : 'Script destructivo: requiere aprobación de otro administrador y respaldo obligatorio.'
                    : 'Se creará un respaldo verificado antes de ejecutar.'}
                </div>
              </div>
              <button className="icon-btn" type="button" aria-label="Cerrar" onClick={() => setApplyTarget(null)}>×</button>
            </div>
            <form className="modal-b" onSubmit={submitApply}>
              <label className="field">
                <span>Escriba la confirmación</span>
                <input
                  type="text"
                  value={confirmation}
                  onChange={(e) => setConfirmation(e.target.value)}
                  placeholder={`EJECUTAR ${applyTarget.name}`}
                  required
                />
                <div className="hint">Debe escribir exactamente: EJECUTAR {applyTarget.name}</div>
              </label>
              <label className="field">
                <span>Su contraseña (re-autenticación)</span>
                <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} required autoComplete="current-password" />
              </label>
              {applyTarget.destructive && !applyTarget.approvedAt && (
                <>
                  <label className="field">
                    <span>Correo del administrador que aprueba</span>
                    <input
                      type="email"
                      value={approverEmail}
                      onChange={(e) => setApproverEmail(e.target.value)}
                      placeholder="otro.admin@sigm.cloud"
                      autoComplete="off"
                      required
                    />
                  </label>
                  <label className="field">
                    <span>Contraseña del aprobador</span>
                    <input
                      type="password"
                      value={approverPassword}
                      onChange={(e) => setApproverPassword(e.target.value)}
                      autoComplete="new-password"
                      required
                    />
                    <div className="hint">Debe ser otro administrador de plataforma, distinto del autor y de quien aplica.</div>
                  </label>
                </>
              )}
              <label className="field">
                <span>Nota de la ejecución (opcional)</span>
                <input type="text" value={applyNote} maxLength={200} onChange={(e) => setApplyNote(e.target.value)} />
              </label>
              <label className="switch" style={{ marginTop: 4 }}>
                <input
                  type="checkbox"
                  checked={skipBackup}
                  disabled={applyTarget.destructive}
                  onChange={(e) => setSkipBackup(e.target.checked)}
                />
                <span className="track" />
                <span className="muted">Omitir respaldo {applyTarget.destructive && '(bloqueado para scripts destructivos)'}</span>
              </label>
              <div className="row" style={{ marginTop: 18 }}>
                <button className="btn danger" type="submit" disabled={busy === 'apply'}>
                  {busy === 'apply'
                    ? 'Aplicando…'
                    : applyTarget.destructive && !applyTarget.approvedAt
                      ? 'Aprobar y aplicar'
                      : 'Aplicar script'}
                </button>
                <button className="btn ghost" type="button" onClick={() => setApplyTarget(null)} disabled={busy === 'apply'}>
                  Cancelar
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </>
  );
}
