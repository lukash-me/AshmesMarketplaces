<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { X } from 'lucide-vue-next';
import { useRoute, useRouter } from 'vue-router';

import { getProblemMessage } from '@/shared/api/problemDetails';
import Button from '@/shared/ui/Button.vue';
import Input from '@/shared/ui/Input.vue';

import { createAccessRequest } from './auth.api';
import { useAuthPromptStore } from './authPrompt.store';
import { useAuthStore } from './auth.store';

const auth = useAuthStore();
const prompt = useAuthPromptStore();
const route = useRoute();
const router = useRouter();

const login = ref('');
const password = ref('');
const contact = ref('');
const comment = ref('');
const error = ref('');
const status = ref('');
const loading = ref(false);

const canLogin = computed(() => login.value.trim().length > 0 && password.value.length > 0);
const canRequestAccess = computed(() => contact.value.trim().length > 0);

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

async function submitAccessRequest(): Promise<void> {
  if (!canRequestAccess.value || loading.value) {
    return;
  }

  loading.value = true;
  error.value = '';
  status.value = '';

  try {
    await createAccessRequest({
      contact: contact.value.trim(),
      comment: comment.value.trim(),
      sourcePath: route.fullPath
    });
    contact.value = '';
    comment.value = '';
    status.value = 'Заявка отправлена. Мы свяжемся с вами и откроем доступ.';
  } catch (err) {
    error.value = getProblemMessage(err, 'Не удалось отправить заявку.');
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
            <h2 id="auth-modal-title">Доступ к Ashmes</h2>
            <p>{{ prompt.message || 'Войдите или оставьте заявку, чтобы пользоваться персональными инструментами.' }}</p>
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
            Войти
          </button>
          <button
            type="button"
            :class="{ 'auth-modal__tab--active': prompt.mode === 'access' }"
            class="auth-modal__tab"
            @click="prompt.showAccess(prompt.message)"
          >
            Получить доступ
          </button>
        </div>

        <form v-if="prompt.mode === 'login'" class="auth-modal__form" @submit.prevent="submitLogin">
          <Input
            id="auth-login"
            v-model="login"
            label="Логин"
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

        <form v-else class="auth-modal__form" @submit.prevent="submitAccessRequest">
          <Input
            id="auth-contact"
            v-model="contact"
            label="Email или телефон"
            autocomplete="email"
            placeholder="seller@example.com"
          />
          <label class="auth-modal__field" for="auth-comment">
            <span>Комментарий</span>
            <textarea
              id="auth-comment"
              v-model="comment"
              rows="4"
              placeholder="Коротко опишите, какие товары и задачи хотите анализировать"
            />
          </label>
          <p v-if="error" class="auth-modal__error">{{ error }}</p>
          <p v-if="status" class="auth-modal__status">{{ status }}</p>
          <Button type="submit" variant="primary" :disabled="!canRequestAccess" :loading="loading">
            Отправить заявку
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

.auth-modal__header p {
  margin: var(--space-1) 0 0;
  color: var(--color-text-muted);
  font-size: 0.875rem;
  line-height: 1.45;
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

.auth-modal__form,
.auth-modal__field {
  display: grid;
  gap: var(--space-3);
}

.auth-modal__field span {
  color: var(--color-text-muted);
  font-size: 0.72rem;
  font-weight: 680;
  letter-spacing: 0.02em;
  text-transform: uppercase;
}

.auth-modal__field textarea {
  width: 100%;
  resize: vertical;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--surface-control);
  color: var(--color-text);
  padding: var(--space-3);
  font: inherit;
}

.auth-modal__field textarea:focus {
  border-color: var(--color-primary);
  background: var(--surface-control-focus);
  box-shadow: var(--focus-ring);
  outline: none;
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
