import axios, { AxiosError, type InternalAxiosRequestConfig } from 'axios';

type AuthHandlers = {
  getAccessToken: () => string | null;
  refresh: () => Promise<boolean>;
  onUnauthorized: () => void;
};

type RetryConfig = InternalAxiosRequestConfig & {
  _retry?: boolean;
};

let authHandlers: AuthHandlers | null = null;

export const publicHttp = axios.create({
  baseURL: '/api/v1',
  headers: {
    Accept: 'application/json'
  }
});

export const http = axios.create({
  baseURL: '/api/v1',
  headers: {
    Accept: 'application/json'
  }
});

export function configureHttpAuth(handlers: AuthHandlers): void {
  authHandlers = handlers;
}

http.interceptors.request.use((config) => {
  const token = authHandlers?.getAccessToken();

  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }

  return config;
});

http.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const config = error.config as RetryConfig | undefined;

    if (error.response?.status !== 401 || !config || config._retry || !authHandlers) {
      return Promise.reject(error);
    }

    config._retry = true;
    const refreshed = await authHandlers.refresh();

    if (!refreshed) {
      authHandlers.onUnauthorized();
      return Promise.reject(error);
    }

    return http(config);
  }
);
