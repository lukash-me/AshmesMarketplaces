<script setup lang="ts">
import { computed, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';

import Button from '@/shared/ui/Button.vue';
import Card from '@/shared/ui/Card.vue';
import Input from '@/shared/ui/Input.vue';
import { getProblemMessage } from '@/shared/api/problemDetails';

import { useAuthStore } from './auth.store';

const route = useRoute();
const router = useRouter();
const auth = useAuthStore();

const login = ref('');
const password = ref('');
const error = ref('');
const loading = ref(false);

const canSubmit = computed(() => login.value.trim().length > 0 && password.value.length > 0);

async function submit() {
  if (!canSubmit.value || loading.value) {
    return;
  }

  error.value = '';
  loading.value = true;

  try {
    await auth.login({
      login: login.value.trim(),
      password: password.value
    });

    const redirect = typeof route.query.redirect === 'string' ? route.query.redirect : '/overview';
    await router.push(redirect);
  } catch (err) {
    error.value = getProblemMessage(err, 'Не удалось войти с этими данными.');
  } finally {
    loading.value = false;
  }
}
</script>

<template>
  <Card class="login-card">
    <div class="login-card__header">
      <div class="login-card__mark" aria-hidden="true">
        <svg viewBox="0 0 96 150" focusable="false">
          <path
            class="login-card__flame-outer"
            d="M47 145C25 131 10 111 11 86c1-21 13-33 18-48 4-12 1-23-4-34 19 11 31 29 30 48 11-12 17-29 13-48 21 19 29 43 23 66 8-7 12-17 11-29 13 17 17 39 10 60-8 25-31 40-65 44Z"
          />
          <path
            class="login-card__flame-middle"
            d="M49 132c-18-11-28-26-27-45 1-16 11-25 20-36 7-9 9-20 6-32 17 13 23 30 17 49 10-7 16-18 17-33 13 15 17 32 11 49 7-4 12-11 15-21 5 19 0 39-13 52-10 10-24 16-46 17Z"
          />
          <path
            class="login-card__flame-inner"
            d="M50 126c-13-9-20-21-18-35 2-12 11-20 20-30 8-9 11-17 10-27 13 13 15 27 8 42 7-3 12-9 16-18 5 17 1 34-10 47-7 9-15 16-26 21Z"
          />
          <path
            class="login-card__flame-core"
            d="M52 116c-8-7-11-15-8-25 2-8 9-14 15-21 4-5 7-11 7-18 8 10 8 21 2 32 5-2 9-6 12-12 1 16-9 34-28 44Z"
          />
        </svg>
      </div>
      <div>
        <h1>Ashmes Marketplaces</h1>
        <p>Рабочее пространство селлера</p>
      </div>
    </div>

    <form class="login-form" @submit.prevent="submit">
      <Input
        id="login"
        v-model="login"
        label="Логин"
        autocomplete="username"
        placeholder="seller@example.com"
      />
      <Input
        id="password"
        v-model="password"
        label="Пароль"
        type="password"
        autocomplete="current-password"
        placeholder="Введите пароль"
      />
      <p v-if="error" class="login-form__error">{{ error }}</p>
      <Button type="submit" variant="primary" :disabled="!canSubmit" :loading="loading">
        Войти
      </Button>
    </form>
  </Card>
</template>

<style scoped>
.login-card {
  width: min(100%, 26rem);
  padding: var(--space-5);
}

.login-card__header {
  display: flex;
  align-items: center;
  gap: var(--space-4);
  margin-bottom: var(--space-6);
}

.login-card__mark {
  display: grid;
  position: relative;
  height: 3.1rem;
  width: 3.1rem;
  place-items: center;
  flex: 0 0 auto;
  filter:
    drop-shadow(0 0 0.45rem rgb(249 115 22 / 0.22))
    drop-shadow(0 0.35rem 0.75rem rgb(0 0 0 / 0.34));
}

.login-card__mark svg {
  width: 2.45rem;
  height: 3.2rem;
  overflow: visible;
}

.login-card__flame-outer {
  fill: rgb(185 28 28 / 0.82);
  stroke: rgb(248 113 113 / 0.72);
  stroke-width: 2.5;
}

.login-card__flame-middle {
  fill: rgb(234 88 12 / 0.88);
}

.login-card__flame-inner {
  fill: rgb(249 115 22 / 0.9);
}

.login-card__flame-core {
  fill: rgb(254 240 138 / 0.88);
}

h1 {
  margin: 0;
  font-size: 1.35rem;
  font-weight: 760;
}

p {
  margin: var(--space-1) 0 0;
  color: var(--color-text-muted);
}

.login-form {
  display: grid;
  gap: var(--space-4);
}

.login-form__error {
  margin: 0;
  border-radius: var(--radius-md);
  border: 1px solid var(--state-danger-border);
  background: var(--color-danger-soft);
  color: var(--color-danger);
  padding: var(--space-3);
}
</style>
