import { defineStore } from 'pinia';
import { computed, ref } from 'vue';

import { configureHttpAuth } from '@/shared/api/http';

import * as authApi from './auth.api';
import { useAuthPromptStore } from './authPrompt.store';
import type { AuthUser, LoginRequest, LoginResponse, StoredAuthSession } from './auth.types';

const STORAGE_KEY = 'ashmes.auth.session';

export const useAuthStore = defineStore('auth', () => {
  const accessToken = ref<string | null>(null);
  const accessTokenExpiresAtUtc = ref<string | null>(null);
  const refreshToken = ref<string | null>(null);
  const refreshTokenExpiresAtUtc = ref<string | null>(null);
  const sessionId = ref<number | null>(null);
  const user = ref<AuthUser | null>(null);
  const ready = ref(false);
  const bootstrapping = ref(false);

  const isAuthenticated = computed(() => Boolean(accessToken.value));

  configureHttpAuth({
    getAccessToken: () => accessToken.value,
    refresh: refreshSession,
    onUnauthorized: handleUnauthorized
  });

  function hydrate(): void {
    if (ready.value) {
      return;
    }

    const stored = localStorage.getItem(STORAGE_KEY);
    if (!stored) {
      ready.value = true;
      return;
    }

    try {
      applySession(JSON.parse(stored) as StoredAuthSession);
    } catch {
      localStorage.removeItem(STORAGE_KEY);
    } finally {
      ready.value = true;
    }
  }

  async function bootstrap(): Promise<void> {
    hydrate();

    if (!accessToken.value || bootstrapping.value) {
      return;
    }

    bootstrapping.value = true;
    try {
      user.value = await authApi.getMe();
      persist();
    } catch {
      const refreshed = await refreshSession();
      if (!refreshed) {
        clearSession();
      }
    } finally {
      bootstrapping.value = false;
    }
  }

  async function login(request: LoginRequest): Promise<void> {
    const session = await authApi.login(request);
    applySession(session);
    persist();
  }

  async function logout(): Promise<void> {
    try {
      if (accessToken.value) {
        await authApi.logout();
      }
    } finally {
      clearSession();
    }
  }

  async function refreshSession(): Promise<boolean> {
    if (!refreshToken.value || !sessionId.value) {
      return false;
    }

    try {
      const session = await authApi.refresh({
        sessionId: sessionId.value,
        refreshToken: refreshToken.value
      });
      applySession(session);
      persist();
      return true;
    } catch {
      clearSession();
      return false;
    }
  }

  function applySession(session: LoginResponse | StoredAuthSession): void {
    accessToken.value = session.accessToken;
    accessTokenExpiresAtUtc.value = session.accessTokenExpiresAtUtc;
    refreshToken.value = session.refreshToken;
    refreshTokenExpiresAtUtc.value = session.refreshTokenExpiresAtUtc;
    sessionId.value = session.sessionId;
    user.value = session.user;
  }

  function persist(): void {
    if (!accessToken.value || !refreshToken.value || !sessionId.value) {
      localStorage.removeItem(STORAGE_KEY);
      return;
    }

    // Temporary MVP approach: v1 persists both access and refresh tokens in localStorage.
    // Future hardening can keep access tokens memory-only while preserving refresh/session UX.
    const session: StoredAuthSession = {
      accessToken: accessToken.value,
      accessTokenExpiresAtUtc: accessTokenExpiresAtUtc.value ?? '',
      refreshToken: refreshToken.value,
      refreshTokenExpiresAtUtc: refreshTokenExpiresAtUtc.value ?? '',
      sessionId: sessionId.value,
      user: user.value
    };

    localStorage.setItem(STORAGE_KEY, JSON.stringify(session));
  }

  function clearSession(): void {
    accessToken.value = null;
    accessTokenExpiresAtUtc.value = null;
    refreshToken.value = null;
    refreshTokenExpiresAtUtc.value = null;
    sessionId.value = null;
    user.value = null;
    localStorage.removeItem(STORAGE_KEY);
  }

  function handleUnauthorized(): void {
    clearSession();
    useAuthPromptStore().showLogin('Войдите, чтобы продолжить.');
  }

  return {
    accessToken,
    accessTokenExpiresAtUtc,
    refreshToken,
    refreshTokenExpiresAtUtc,
    sessionId,
    user,
    ready,
    bootstrapping,
    isAuthenticated,
    hydrate,
    bootstrap,
    login,
    logout,
    refreshSession,
    clearSession
  };
});
