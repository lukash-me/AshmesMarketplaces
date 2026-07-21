<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { X } from 'lucide-vue-next';
import { useRoute, useRouter } from 'vue-router';

import { getProblemMessage } from '@/shared/api/problemDetails';
import Button from '@/shared/ui/Button.vue';
import Input from '@/shared/ui/Input.vue';

import { useAuthPromptStore } from './authPrompt.store';
import { useAuthStore } from './auth.store';

const auth = useAuthStore();
const prompt = useAuthPromptStore();
const route = useRoute();
const router = useRouter();

const login = ref('');
const password = ref('');
const registerEmail = ref('');
const registerPassword = ref('');
const registerPasswordRepeat = ref('');
const error = ref('');
const status = ref('');
const loading = ref(false);

const canLogin = computed(() => login.value.trim().length > 0 && password.value.length > 0);
const canRegister = computed(
  () =>
    registerEmail.value.trim().length > 0 &&
    registerPassword.value.length >= 8 &&
    registerPassword.value === registerPasswordRepeat.value
);

watch(
  () => [prompt.open, prompt.mode] as const,
  () => {
    error.value = '';
    status.value = '';
  }
);

async function submitLogin(): Promise<void> {
  if (!canLogin.value || loading.value) {
    return;
  }

  loading.value = true;
  error.value = '';
  status.value = '';

  try {
    await auth.login({
      login: login.value.trim(),
      password: password.value
    });
    prompt.close();
    await router.replace({ path: route.path, query: withoutAuthQuery(route.query) });
  } catch (err) {
    error.value = getProblemMessage(err, 'Не удалось войти с этими данными.');
  } finally {
    loading.value = false;
  }
}

async function submitRegister(): Promise<void> {
  if (loading.value) {
    return;
  }

  if (registerEmail.value.trim().length === 0) {
    error.value = 'Введите email для регистрации.';
    return;
  }

  if (registerPassword.value.length < 8) {
    error.value = 'Пароль должен содержать минимум 8 символов.';
    return;
  }

  if (registerPassword.value !== registerPasswordRepeat.value) {
    error.value = 'Пароли не совпадают.';
    return;
  }

  loading.value = true;
  error.value = '';
  status.value = '';

  try {
    await auth.register({
      email: registerEmail.value.trim(),
      password: registerPassword.value
    });
    prompt.close();
    await router.replace({ path: route.path, query: withoutAuthQuery(route.query) });
  } catch (err) {
    error.value = getProblemMessage(err, 'Не удалось зарегистрироваться.');
  } finally {
    loading.value = false;
  }
}

function close(): void {
  prompt.close();
  if (route.query.auth) {
    void router.replace({ path: route.path, query: withoutAuthQuery(route.query) });
  }
}

function withoutAuthQuery(query: typeof route.query): Record<string, unknown> {
  const next: Record<string, unknown> = { ...query };
  delete next.auth;
  return next;
}
</script>

<template>
  <Teleport to="body">
    <div v-if="prompt.open" class="auth-modal" role="presentation" @click.self="close">
      <section class="auth-modal__panel" role="dialog" aria-modal="true" aria-labelledby="auth-modal-title">
        <header class="auth-modal__header">
          <div>
            <h2 id="auth-modal-title">Доступ к Ashmes Marketplaces</h2>
            <p>{{ prompt.message || 'Войдите или зарегистрируйтесь для доступа к персональным инструментам.' }}</p>
          </div>
          <button class="app-icon-button" type="button" aria-label="Закрыть" @click="close">
            <X :size="18" />
          </button>
        </header>

        <div class="auth-modal__tabs" role="tablist" aria-label="Способ доступа">
          <button
            type="button"
            :class="{ 'auth-modal__tab--active': prompt.mode === 'login' }"
            class="auth-modal__tab"
            @click="prompt.showLogin(prompt.message)"
          >
            Вход
          </button>
          <button
            type="button"
            :class="{ 'auth-modal__tab--active': prompt.mode === 'register' }"
            class="auth-modal__tab"
            @click="prompt.showRegister(prompt.message)"
          >
            Регистрация
          </button>
        </div>

        <form v-if="prompt.mode === 'login'" class="auth-modal__form" @submit.prevent="submitLogin">
          <Input
            id="auth-login"
            v-model="login"
            label="Email"
            autocomplete="username"
            placeholder="seller@example.com"
          />
          <Input
            id="auth-password"
            v-model="password"
            label="Пароль"
            type="password"
            autocomplete="current-password"
            placeholder="Введите пароль"
          />
          <p v-if="error" class="auth-modal__error">{{ error }}</p>
          <p v-if="status" class="auth-modal__status">{{ status }}</p>
          <Button type="submit" variant="primary" :disabled="!canLogin" :loading="loading">Войти</Button>
        </form>

        <form v-else class="auth-modal__form" @submit.prevent="submitRegister">
          <Input
            id="auth-register-email"
            v-model="registerEmail"
            label="Email"
            autocomplete="email"
            placeholder="seller@example.com"
          />
          <Input
            id="auth-register-password"
            v-model="registerPassword"
            label="Пароль"
            type="password"
            autocomplete="new-password"
            placeholder="Минимум 8 символов"
          />
          <Input
            id="auth-register-password-repeat"
            v-model="registerPasswordRepeat"
            label="Пароль еще раз"
            type="password"
            autocomplete="new-password"
            placeholder="Повторите пароль"
          />
          <p v-if="error" class="auth-modal__error">{{ error }}</p>
          <p v-if="status" class="auth-modal__status">{{ status }}</p>
          <Button type="submit" variant="primary" :disabled="!canRegister" :loading="loading">
            Зарегистрироваться
          </Button>
        </form>
      </section>
    </div>
  </Teleport>
</template>

<style scoped>
.auth-modal {
  position: fixed;
  inset: 0;
  z-index: 80;
  display: grid;
  place-items: center;
  background: rgb(15 23 42 / 0.42);
  padding: var(--space-4);
  backdrop-filter: blur(4px);
}

.auth-modal__panel {
  display: grid;
  gap: var(--space-4);
  width: min(100%, 29rem);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  background: var(--surface-panel-raised);
  box-shadow: var(--shadow-panel);
  padding: var(--space-5);
}

.auth-modal__header {
  display: flex;
  justify-content: space-between;
  gap: var(--space-3);
}

.auth-modal__header h2 {
  margin: 0;
  font-size: 1.1rem;
}

.auth-modal__header p,
.auth-modal__hint {
  margin: var(--space-1) 0 0;
  color: var(--color-text-muted);
  font-size: 0.875rem;
  line-height: 1.45;
}

.auth-modal__hint {
  margin: 0;
}

.auth-modal__tabs {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: var(--space-2);
}

.auth-modal__tab {
  min-height: 2.25rem;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--surface-control);
  color: var(--color-text-muted);
  font: inherit;
  font-weight: 720;
}

.auth-modal__tab--active {
  border-color: var(--accent-primary-border);
  background: var(--accent-ember-soft-bg);
  color: var(--accent-ember-text-strong);
}

.auth-modal__form {
  display: grid;
  gap: var(--space-3);
}

.auth-modal :deep(.field__label) {
  font-size: 0.8125rem;
  letter-spacing: 0;
  text-transform: none;
}

.auth-modal__error,
.auth-modal__status {
  margin: 0;
  border-radius: var(--radius-md);
  padding: var(--space-3);
  font-size: 0.86rem;
}

.auth-modal__error {
  border: 1px solid var(--state-danger-border);
  background: var(--state-danger-soft);
  color: var(--state-danger-text);
}

.auth-modal__status {
  border: 1px solid var(--state-success-border);
  background: var(--state-success-soft);
  color: var(--state-success-text);
}
</style>
