import { useEffect, useState } from 'react';
import { api } from '../api';

type Props = { onClose: () => void };

/**
 * Seguridad de la cuenta: enrolamiento de segundo factor (TOTP) y desactivación
 * con re-autenticación (contraseña + código actual).
 */
export default function SecurityModal({ onClose }: Props) {
  const [enabled, setEnabled] = useState<boolean | null>(null);
  const [email, setEmail] = useState('');
  const [remaining, setRemaining] = useState(0);
  const [recoveryCodes, setRecoveryCodes] = useState<string[]>([]);
  const [regenerating, setRegenerating] = useState(false);
  const [secret, setSecret] = useState('');
  const [provisioningUrl, setProvisioningUrl] = useState('');
  const [code, setCode] = useState('');
  const [password, setPassword] = useState('');
  const [disabling, setDisabling] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');

  async function loadStatus() {
    try {
      const status = await api.mfaStatus();
      setEnabled(Boolean(status.mfaEnabled));
      setEmail(status.email ?? '');
      setRemaining(status.recoveryCodesRemaining ?? 0);
    } catch (e: any) {
      setError(e?.message || 'No fue posible consultar el estado de seguridad.');
      setEnabled(false);
    }
  }

  useEffect(() => {
    void loadStatus();
  }, []);

  async function start() {
    setBusy(true);
    setError('');
    setNotice('');
    try {
      const result = await api.setupMfa();
      setSecret(result.secret);
      setProvisioningUrl(result.provisioningUrl);
      setEnabled(false);
    } catch (e: any) {
      setError(e?.message || 'No fue posible iniciar el enrolamiento.');
    } finally {
      setBusy(false);
    }
  }

  async function confirm(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError('');
    try {
      const result = await api.verifyMfa(code.trim());
      setRecoveryCodes(result.recoveryCodes ?? []);
      setNotice('Verificación en dos pasos activada. Guarde los códigos de recuperación: no se volverán a mostrar.');
      setCode('');
      setSecret('');
      setProvisioningUrl('');
      await loadStatus();
    } catch (e: any) {
      setError(e?.message || 'Código inválido.');
    } finally {
      setBusy(false);
    }
  }

  async function regenerate(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError('');
    try {
      const result = await api.regenerateRecoveryCodes(password);
      setRecoveryCodes(result.recoveryCodes ?? []);
      setNotice('Nuevos códigos generados. Los anteriores dejaron de funcionar.');
      setPassword('');
      setRegenerating(false);
      await loadStatus();
    } catch (e: any) {
      setError(e?.message || 'No fue posible regenerar los códigos.');
    } finally {
      setBusy(false);
    }
  }

  async function disable(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError('');
    try {
      await api.disableMfa(password, code.trim());
      setNotice('Verificación en dos pasos desactivada.');
      setPassword('');
      setCode('');
      setDisabling(false);
      setRecoveryCodes([]);
      await loadStatus();
    } catch (e: any) {
      setError(e?.message || 'No fue posible desactivar la verificación.');
    } finally {
      setBusy(false);
    }
  }

  async function copySecret() {
    try {
      await navigator.clipboard.writeText(secret);
      setNotice('Secreto copiado al portapapeles.');
    } catch {
      setError('El navegador no permitió copiar; transcríbalo manualmente.');
    }
  }

  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div className="modal-card" onClick={(e) => e.stopPropagation()} role="dialog" aria-modal="true" aria-label="Seguridad de la cuenta">
        <div className="modal-head">
          <div>
            <h3>Seguridad de la cuenta</h3>
            <p>{email || 'Verificación en dos pasos (TOTP)'}</p>
          </div>
          <button className="btn ghost sm" type="button" onClick={onClose} aria-label="Cerrar">×</button>
        </div>
        <div className="modal-body">
          {error && <div className="alert error">{error}</div>}
          {notice && <div className="alert" style={{ fontWeight: 500 }}>{notice}</div>}

          {recoveryCodes.length > 0 && (
            <div style={{ padding: 12, border: '1px dashed var(--line)', borderRadius: 10, marginBottom: 12 }}>
              <div className="muted" style={{ fontSize: 12, marginBottom: 6 }}>
                Códigos de recuperación: cada uno sirve una sola vez si pierde el teléfono.
              </div>
              <div style={{ fontFamily: 'monospace', display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 4 }}>
                {recoveryCodes.map(c => <span key={c}>{c}</span>)}
              </div>
              <div className="toolbar" style={{ marginTop: 8, padding: 0 }}>
                <button
                  className="btn subtle sm"
                  type="button"
                  onClick={() => void navigator.clipboard.writeText(recoveryCodes.join('\n')).then(() => setNotice('Códigos copiados.')).catch(() => setError('No se pudo copiar.'))}
                >
                  Copiar códigos
                </button>
                <button className="btn ghost sm" type="button" onClick={() => setRecoveryCodes([])}>Ya los guardé</button>
              </div>
            </div>
          )}

          {enabled === null && <div className="empty">Consultando estado…</div>}

          {enabled === true && !disabling && !regenerating && (
            <>
              <p className="muted">
                La verificación en dos pasos está <strong>activa</strong>. Cada inicio de sesión solicita un código
                de 6 dígitos. Códigos de recuperación disponibles: <strong>{remaining}</strong>.
              </p>
              <div className="toolbar" style={{ padding: 0 }}>
                <button className="btn ghost" type="button" onClick={() => { setRegenerating(true); setNotice(''); setError(''); }}>
                  Regenerar códigos
                </button>
                <button className="btn danger" type="button" onClick={() => { setDisabling(true); setNotice(''); setError(''); }}>
                  Desactivar verificación
                </button>
                <button className="btn ghost" type="button" onClick={onClose}>Cerrar</button>
              </div>
            </>
          )}

          {regenerating && (
            <form onSubmit={regenerate}>
              <p className="muted">Los códigos actuales dejarán de funcionar. Confirme su contraseña para generar diez nuevos.</p>
              <label className="field">
                <span>Contraseña</span>
                <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} required autoComplete="current-password" />
              </label>
              <div className="toolbar" style={{ marginTop: 14, padding: 0 }}>
                <button className="btn" type="submit" disabled={busy || password.length === 0}>
                  {busy ? 'Generando…' : 'Generar códigos'}
                </button>
                <button className="btn ghost" type="button" onClick={() => { setRegenerating(false); setPassword(''); }} disabled={busy}>
                  Volver
                </button>
              </div>
            </form>
          )}

          {enabled === false && !secret && (
            <>
              <p className="muted">
                Añada un segundo factor a su cuenta. Necesitará una aplicación autenticadora (Google Authenticator,
                Microsoft Authenticator, 1Password, Authy…).
              </p>
              <div className="toolbar" style={{ padding: 0 }}>
                <button className="btn" type="button" onClick={() => void start()} disabled={busy}>
                  {busy ? 'Generando…' : 'Comenzar enrolamiento'}
                </button>
                <button className="btn ghost" type="button" onClick={onClose}>Cancelar</button>
              </div>
            </>
          )}

          {secret && (
            <form onSubmit={confirm}>
              <p className="muted">
                Escanee el código o escriba la clave en su aplicación. Luego introduzca el código de 6 dígitos para
                confirmar.
              </p>
              <div style={{ padding: 12, border: '1px dashed var(--line)', borderRadius: 10, marginBottom: 12 }}>
                <div className="muted" style={{ fontSize: 12 }}>Clave secreta</div>
                <div style={{ fontFamily: 'monospace', fontSize: 16, letterSpacing: 1.5, wordBreak: 'break-all' }}>{secret}</div>
                <div className="toolbar" style={{ marginTop: 8, padding: 0 }}>
                  <button className="btn subtle sm" type="button" onClick={() => void copySecret()}>Copiar clave</button>
                  {provisioningUrl && (
                    <a className="btn ghost sm" href={provisioningUrl} style={{ textDecoration: 'none' }}>
                      Abrir en la app
                    </a>
                  )}
                </div>
              </div>
              <label className="field">
                <span>Código de verificación</span>
                <input
                  value={code}
                  onChange={(e) => setCode(e.target.value.replace(/\D/g, '').slice(0, 6))}
                  inputMode="numeric"
                  autoComplete="one-time-code"
                  placeholder="123456"
                  required
                />
              </label>
              <div className="toolbar" style={{ marginTop: 14, padding: 0 }}>
                <button className="btn" type="submit" disabled={busy || code.length !== 6}>
                  {busy ? 'Verificando…' : 'Activar verificación'}
                </button>
                <button
                  className="btn ghost"
                  type="button"
                  onClick={() => { setSecret(''); setProvisioningUrl(''); setCode(''); }}
                  disabled={busy}
                >
                  Cancelar
                </button>
              </div>
            </form>
          )}

          {disabling && (
            <form onSubmit={disable}>
              <p className="muted">
                Para desactivar el segundo factor confirme su contraseña y el código actual de su aplicación.
              </p>
              <label className="field">
                <span>Contraseña</span>
                <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} required autoComplete="current-password" />
              </label>
              <label className="field">
                <span>Código actual</span>
                <input
                  value={code}
                  onChange={(e) => setCode(e.target.value.replace(/\D/g, '').slice(0, 6))}
                  inputMode="numeric"
                  autoComplete="one-time-code"
                  required
                />
              </label>
              <div className="toolbar" style={{ marginTop: 14, padding: 0 }}>
                <button className="btn danger" type="submit" disabled={busy || password.length === 0 || code.length !== 6}>
                  {busy ? 'Desactivando…' : 'Desactivar'}
                </button>
                <button className="btn ghost" type="button" onClick={() => setDisabling(false)} disabled={busy}>
                  Volver
                </button>
              </div>
            </form>
          )}
        </div>
      </div>
    </div>
  );
}
