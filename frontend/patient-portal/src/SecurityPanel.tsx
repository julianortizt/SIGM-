import { useEffect, useState } from 'react';
import { api } from './api';

/**
 * Seguridad de la cuenta del paciente: contraseña, verificación en dos pasos y
 * códigos de recuperación. Los códigos solo se muestran al generarlos.
 */
export default function SecurityPanel() {
  const [enabled, setEnabled] = useState<boolean | null>(null);
  const [remaining, setRemaining] = useState(0);
  const [secret, setSecret] = useState('');
  const [provisioningUrl, setProvisioningUrl] = useState('');
  const [code, setCode] = useState('');
  const [password, setPassword] = useState('');
  const [recoveryCodes, setRecoveryCodes] = useState<string[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');

  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [repeatPassword, setRepeatPassword] = useState('');

  async function loadStatus() {
    try {
      const status = await api.mfaStatus();
      setEnabled(Boolean(status.mfaEnabled));
      setRemaining(status.recoveryCodesRemaining ?? 0);
    } catch (e: any) {
      setError(e?.message || 'No fue posible consultar el estado de seguridad.');
      setEnabled(false);
    }
  }

  useEffect(() => {
    void loadStatus();
  }, []);

  async function changePassword(e: React.FormEvent) {
    e.preventDefault();
    setError('');
    setNotice('');
    if (newPassword !== repeatPassword) {
      setError('Las contraseñas no coinciden.');
      return;
    }
    setBusy(true);
    try {
      const result = await api.changePassword(currentPassword, newPassword);
      setNotice(result.message || 'Contraseña actualizada.');
      setCurrentPassword('');
      setNewPassword('');
      setRepeatPassword('');
    } catch (err: any) {
      setError(err?.message || 'No fue posible cambiar la contraseña.');
    } finally {
      setBusy(false);
    }
  }

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
      setNotice('Verificación en dos pasos activada. Guarde sus códigos de recuperación.');
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

  async function disable(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError('');
    try {
      await api.disableMfa(password, code.trim());
      setNotice('Verificación en dos pasos desactivada. Vuelva a iniciar sesión.');
      setPassword('');
      setCode('');
      setRecoveryCodes([]);
      await loadStatus();
    } catch (e: any) {
      setError(e?.message || 'No fue posible desactivar la verificación.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="panel" style={{ marginTop: 18 }}>
      <div className="panel-h">
        <div>
          <div className="panel-t">Seguridad de la cuenta</div>
          <div className="panel-d">Contraseña y segundo factor para proteger su información clínica.</div>
        </div>
      </div>
      <div className="panel-b">
        {error && <div className="alert error">{error}</div>}
        {notice && <div className="alert" style={{ fontWeight: 500 }}>{notice}</div>}

        <form onSubmit={changePassword}>
          <div className="panel-sec-t">Cambiar contraseña</div>
          <div className="fields-grid">
            <label className="field"><span>Contraseña actual</span><input type="password" value={currentPassword} onChange={e => setCurrentPassword(e.target.value)} required autoComplete="current-password" /></label>
            <label className="field"><span>Nueva contraseña</span><input type="password" minLength={8} value={newPassword} onChange={e => setNewPassword(e.target.value)} required autoComplete="new-password" /></label>
            <label className="field"><span>Repetir contraseña</span><input type="password" minLength={8} value={repeatPassword} onChange={e => setRepeatPassword(e.target.value)} required autoComplete="new-password" /></label>
          </div>
          <div className="toolbar" style={{ paddingLeft: 0, paddingRight: 0 }}>
            <button className="btn" disabled={busy}>Actualizar contraseña</button>
          </div>
        </form>

        <div className="panel-sec-t" style={{ marginTop: 18 }}>Verificación en dos pasos</div>
        {enabled === null && <div className="empty">Consultando estado…</div>}
        {enabled === true && (
          <>
            <p className="muted">Activa. Códigos de recuperación disponibles: <b>{remaining}</b>.</p>
            {recoveryCodes.length > 0 && (
              <div style={{ fontFamily: 'monospace', display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 4, marginBottom: 10 }}>
                {recoveryCodes.map(c => <span key={c}>{c}</span>)}
              </div>
            )}
            <form onSubmit={disable}>
              <div className="fields-grid">
                <label className="field"><span>Contraseña</span><input type="password" value={password} onChange={e => setPassword(e.target.value)} required autoComplete="current-password" /></label>
                <label className="field"><span>Código actual</span><input value={code} onChange={e => setCode(e.target.value.replace(/\D/g, '').slice(0, 6))} inputMode="numeric" autoComplete="one-time-code" required /></label>
              </div>
              <div className="toolbar" style={{ paddingLeft: 0, paddingRight: 0 }}>
                <button className="btn danger" disabled={busy || code.length !== 6}>Desactivar</button>
              </div>
            </form>
          </>
        )}
        {enabled === false && !secret && (
          <>
            <p className="muted">Añada un segundo factor con una aplicación autenticadora (Google Authenticator, Microsoft Authenticator…).</p>
            <div className="toolbar" style={{ paddingLeft: 0, paddingRight: 0 }}>
              <button className="btn" type="button" onClick={() => void start()} disabled={busy}>{busy ? 'Generando…' : 'Activar verificación'}</button>
            </div>
          </>
        )}
        {secret && (
          <form onSubmit={confirm}>
            <p className="muted">Escriba esta clave en su aplicación y luego el código de 6 dígitos para confirmar.</p>
            <div style={{ fontFamily: 'monospace', fontSize: 16, letterSpacing: 1.5, wordBreak: 'break-all', marginBottom: 8 }}>{secret}</div>
            {provisioningUrl && <p className="muted" style={{ wordBreak: 'break-all' }}>{provisioningUrl}</p>}
            <div className="fields-grid">
              <label className="field"><span>Código de verificación</span><input value={code} onChange={e => setCode(e.target.value.replace(/\D/g, '').slice(0, 6))} inputMode="numeric" autoComplete="one-time-code" required /></label>
            </div>
            <div className="toolbar" style={{ paddingLeft: 0, paddingRight: 0 }}>
              <button className="btn" disabled={busy || code.length !== 6}>{busy ? 'Verificando…' : 'Confirmar'}</button>
              <button className="btn ghost" type="button" onClick={() => { setSecret(''); setProvisioningUrl(''); setCode(''); }}>Cancelar</button>
            </div>
          </form>
        )}
      </div>
    </div>
  );
}
