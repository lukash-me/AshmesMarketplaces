export interface LoginRequest {
  login: string;
  password: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
}

export interface AccessRequestPayload {
  contact: string;
  comment: string;
  sourcePath?: string;
}

export interface AccessRequestResponse {
  id: string;
  contact: string;
  status: string;
  createdAtUtc: string;
}

export interface RefreshRequest {
  sessionId: number;
  refreshToken: string;
}

export interface AuthUserWorkspace {
  idWorkspace: string;
  idRole: string;
  workspaceName: string;
}

export interface AuthPermission {
  id: string;
  idCategory: string;
  name: string;
  description: string;
  domain: number;
}

export interface AuthAnalysisSchedule {
  timezoneId: string;
  hotProductsLocalTime: string;
  overviewLocalTime: string;
  nextHotProductsRunAtUtc: string;
  nextOverviewRunAtUtc: string;
  lastHotProductsStartedAtUtc: string | null;
  lastHotProductsCompletedAtUtc: string | null;
  lastHotProductsStatus: string;
  lastHotProductsError: string | null;
  lastOverviewStartedAtUtc: string | null;
  lastOverviewCompletedAtUtc: string | null;
  lastOverviewStatus: string;
  lastOverviewError: string | null;
}

export interface AuthUser {
  id: string;
  idRole: string;
  roleName: string;
  login: string;
  email: string | null;
  phone: string;
  status: number;
  analysisSchedule: AuthAnalysisSchedule;
  workspaces: AuthUserWorkspace[];
  permissions: AuthPermission[];
}

export interface LoginResponse {
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshToken: string;
  refreshTokenExpiresAtUtc: string;
  sessionId: number;
  user: AuthUser;
}

export interface StoredAuthSession {
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshToken: string;
  refreshTokenExpiresAtUtc: string;
  sessionId: number;
  user: AuthUser | null;
}
