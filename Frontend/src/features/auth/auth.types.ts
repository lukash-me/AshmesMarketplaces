export interface LoginRequest {
  login: string;
  password: string;
}

export interface RefreshRequest {
  sessionId: number;
  refreshToken: string;
}

export interface AuthUserWorkspace {
  idWorkspace: string;
  idRole: string;
}

export interface AuthPermission {
  id: string;
  idCategory: string;
  name: string;
  description: string;
  domain: number;
}

export interface AuthUser {
  id: string;
  idRole: string;
  login: string;
  email: string | null;
  phone: string;
  status: number;
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
