// Las rutas ya empiezan por /api/v1; si VITE_API_URL termina en /api se quita
// para no generar /api/api/v1 (404 en Vercel).
export const API = (import.meta.env.VITE_API_URL || 'http://localhost:5000').replace(/\/api\/?$/, '');

export type Session = {
  accessToken: string;
  expiresAt: string;
  userId: string;
  tenantId?: string | null;
  fullName: string;
  permissions: string[];
};

export type Tenant = {
  id: string;
  tenantCode: string;
  commercialName: string;
  legalName: string;
  taxId?: string | null;
  productType: string;
  status: string;
  timezone?: string;
  createdAt: string;
  users: number;
  patients: number;
  appointments: number;
  disabledModules: number;
  planName?: string | null;
};

export type ModuleInfo = {
  code: string;
  name: string;
  description: string;
  enabled?: boolean;
  updatedAt?: string | null;
};

export type TenantDetail = {
  tenant: Tenant & { locale?: string; currency?: string; professionals?: number; sites?: number; subscriptionStatus?: string | null };
  modules: ModuleInfo[];
};

export type AuditEvent = {
  id: string;
  tenantId?: string | null;
  commercialName?: string | null;
  email?: string | null;
  eventType: string;
  success: boolean;
  createdAt: string;
};

export type PlatformStats = {
  tenants: number;
  activeTenants: number;
  suspendedTenants: number;
  users: number;
  patients: number;
  appointments: number;
};

export type WelcomeContent = { title: string; message: string; updatedAt?: string | null };

export function getToken(): string | null {
  return localStorage.getItem('sigm_token');
}

export function getSession(): Session | null {
  try {
    return JSON.parse(localStorage.getItem('sigm_user') || 'null');
  } catch {
    return null;
  }
}

export function setSession(session: Session) {
  localStorage.setItem('sigm_token', session.accessToken);
  localStorage.setItem('sigm_user', JSON.stringify(session));
  if (session.tenantId) localStorage.setItem('sigm_tenant', session.tenantId);
}

export function clearSession() {
  localStorage.removeItem('sigm_token');
  localStorage.removeItem('sigm_user');
  localStorage.removeItem('sigm_tenant');
}

/** Error de API que conserva el código HTTP y el cuerpo (p. ej. mfaRequired). */
export class ApiError extends Error {
  status: number;
  data: any;
  constructor(message: string, status: number, data: any) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.data = data;
  }
}

async function req<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers: Record<string, string> = { 'Content-Type': 'application/json', ...(init.headers as Record<string, string>) };
  const token = getToken();
  if (token) headers.Authorization = `Bearer ${token}`;
  const res = await fetch(`${API}${path}`, { ...init, headers });
  const text = await res.text();
  const data = text ? JSON.parse(text) : null;
  if (!res.ok) {
    if (res.status === 401 && !data?.mfaRequired) clearSession();
    throw new ApiError(data?.message || data?.title || res.statusText || `Error ${res.status}`, res.status, data);
  }
  return data as T;
}

export type DbStatus = {
  database: string;
  serverVersion?: string | null;
  size?: string | null;
  schemaTables: number;
  scripts: number;
  backups: number;
  backupPath: string;
  backupRetention?: number;
  autoBackupEnabled?: boolean;
  autoBackupIntervalHours?: number;
  lastAutomaticBackupAt?: string | null;
  pgDumpAvailable: boolean;
  pgRestoreAvailable?: boolean;
  maxScriptBytes: number;
};

export type DbScript = {
  id: string;
  name: string;
  description?: string | null;
  checksum: string;
  status: 'PENDING' | 'VALIDATED' | 'APPLIED' | 'FAILED';
  destructive: boolean;
  statements: number;
  sizeBytes: number;
  createdAt: string;
  validatedAt?: string | null;
  appliedAt?: string | null;
  lastError?: string | null;
  approvedBy?: string | null;
  approvedAt?: string | null;
};

export type DbScriptDetail = {
  script: DbScript & { sqlText: string };
  runs: {
    mode: string;
    success: boolean;
    message?: string | null;
    statements: number;
    durationMs: number;
    executedAt: string;
  }[];
};

export type DbBackup = {
  id: string;
  fileName: string;
  sizeBytes: number;
  checksum?: string | null;
  note?: string | null;
  createdAt: string;
};

export type SqlAnalysis = {
  allowed: boolean;
  destructive: boolean;
  statements: number;
  reason?: string | null;
  warnings: string[];
};

async function upload(path: string, form: FormData): Promise<any> {
  const headers: Record<string, string> = {};
  const token = getToken();
  if (token) headers.Authorization = `Bearer ${token}`;
  const res = await fetch(`${API}${path}`, { method: 'POST', body: form, headers });
  const text = await res.text();
  const data = text ? JSON.parse(text) : null;
  if (!res.ok) throw new ApiError(data?.message || res.statusText, res.status, data);
  return data;
}

export async function downloadBlob(path: string): Promise<Blob> {
  const headers: Record<string, string> = {};
  const token = getToken();
  if (token) headers.Authorization = `Bearer ${token}`;
  const res = await fetch(`${API}${path}`, { headers });
  if (!res.ok) {
    const text = await res.text();
    let message = res.statusText;
    try { message = JSON.parse(text)?.message || message; } catch { /* respuesta no JSON */ }
    throw new ApiError(message, res.status, null);
  }
  return res.blob();
}

export const api = {
  login: (email: string, password: string, mfaCode?: string) =>
    req<Session>('/api/v1/auth/login', { method: 'POST', body: JSON.stringify({ email, password, mfaCode: mfaCode || null }) }),
  forgotPassword: (email: string) =>
    req<{ message: string; devToken?: string }>('/api/v1/auth/forgot-password', { method: 'POST', body: JSON.stringify({ email }) }),
  resetPassword: (token: string, newPassword: string) =>
    req<{ success: boolean; message: string }>('/api/v1/auth/reset-password', { method: 'POST', body: JSON.stringify({ token, newPassword }) }),
  changePassword: (currentPassword: string, newPassword: string) =>
    req<{ success: boolean; message: string }>('/api/v1/auth/change-password', { method: 'POST', body: JSON.stringify({ currentPassword, newPassword }) }),
  mfaStatus: () => req<{ mfaEnabled: boolean; configured: boolean; recoveryCodesRemaining: number; email?: string }>('/api/v1/auth/mfa-status'),
  setupMfa: () => req<{ secret: string; provisioningUrl: string }>('/api/v1/auth/setup-mfa', { method: 'POST' }),
  verifyMfa: (code: string) =>
    req<{ mfaEnabled: boolean; recoveryCodes?: string[] }>('/api/v1/auth/verify-mfa', { method: 'POST', body: JSON.stringify({ code }) }),
  disableMfa: (password: string, code: string) =>
    req<{ mfaEnabled: boolean; message?: string }>('/api/v1/auth/disable-mfa', { method: 'POST', body: JSON.stringify({ password, code }) }),
  regenerateRecoveryCodes: (password: string) =>
    req<{ recoveryCodes: string[] }>('/api/v1/auth/recovery-codes', { method: 'POST', body: JSON.stringify({ password }) }),
  stats: () => req<PlatformStats>('/api/v1/platform/stats'),
  tenants: () => req<Tenant[]>('/api/v1/platform/tenants'),
  tenant: (id: string) => req<TenantDetail>(`/api/v1/platform/tenants/${id}`),
  modules: () => req<ModuleInfo[]>('/api/v1/platform/modules'),
  setFeature: (tenantId: string, code: string, enabled: boolean) =>
    req<{ tenantId: string; featureCode: string; enabled: boolean }>(
      `/api/v1/platform/tenants/${tenantId}/features/${encodeURIComponent(code)}`,
      { method: 'PUT', body: JSON.stringify({ enabled }) },
    ),
  setStatus: (tenantId: string, status: string) =>
    req<{ id: string; status: string }>(`/api/v1/platform/tenants/${tenantId}/status`, {
      method: 'POST',
      body: JSON.stringify({ status }),
    }),
  audit: (limit = 100) => req<AuditEvent[]>(`/api/v1/platform/audit?limit=${limit}`),
  welcome: () => req<WelcomeContent>('/api/v1/public/welcome'),
  saveWelcome: (title: string, message: string) =>
    req<WelcomeContent>('/api/v1/platform/welcome', { method: 'PUT', body: JSON.stringify({ title, message }) }),

  dbStatus: () => req<DbStatus>('/api/v1/platform/database/status'),
  dbScripts: () => req<DbScript[]>('/api/v1/platform/database/scripts'),
  dbScript: (id: string) => req<DbScriptDetail>(`/api/v1/platform/database/scripts/${id}`),
  dbUploadScript: (form: FormData) => upload('/api/v1/platform/database/scripts', form) as Promise<{ id: string; name: string; checksum: string; analysis: SqlAnalysis }>,
  dbValidate: (id: string) =>
    req<{ success: boolean; message: string; rowsAffected: number; durationMs: number; analysis: SqlAnalysis }>(
      `/api/v1/platform/database/scripts/${id}/validate`,
      { method: 'POST' },
    ),
  dbApprove: (id: string, body: { email: string; password: string; note?: string }) =>
    req<{ approvedBy: string; approvedAt: string; message: string }>(
      `/api/v1/platform/database/scripts/${id}/approve`,
      { method: 'POST', body: JSON.stringify(body) },
    ),
  dbApply: (id: string, body: { confirmation: string; password: string; skipBackup: boolean; note?: string }) =>
    req<{
      success: boolean;
      message: string;
      rowsAffected: number;
      durationMs: number;
      backup?: DbBackup | null;
      backupVerification?: { valid: boolean; checksumMatches: boolean; objects: number; message: string } | null;
    }>(
      `/api/v1/platform/database/scripts/${id}/apply`,
      { method: 'POST', body: JSON.stringify(body) },
    ),
  dbDeleteScript: (id: string) => req<void>(`/api/v1/platform/database/scripts/${id}`, { method: 'DELETE' }),
  dbBackups: () => req<{ backupPath: string; items: DbBackup[] }>('/api/v1/platform/database/backups'),
  dbCreateBackup: (note?: string) =>
    req<{ backup: DbBackup; message: string; verification?: { valid: boolean; message: string } | null }>('/api/v1/platform/database/backups', {
      method: 'POST',
      body: JSON.stringify({ note }),
    }),
  dbDownloadBackup: (id: string) => downloadBlob(`/api/v1/platform/database/backups/${id}/download`),
  dbVerifyBackup: (id: string) =>
    req<{ valid: boolean; checksumMatches: boolean; objects: number; message: string }>(
      `/api/v1/platform/database/backups/${id}/verify`,
      { method: 'POST' },
    ),
};
