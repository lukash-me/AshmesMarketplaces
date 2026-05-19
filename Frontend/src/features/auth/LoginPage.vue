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
    error.value = getProblemMessage(err, 'Unable to sign in with these credentials.');
  } finally {
    loading.value = false;
  }
}
</script>

<template>
  <Card class="login-card">
    <div class="login-card__header">
      <div class="login-card__mark">A</div>
      <div>
        <h1>Ashmes Marketplaces</h1>
        <p>Seller analytics workspace</p>
      </div>
    </div>

    <form class="login-form" @submit.prevent="submit">
      <Input
        id="login"
        v-model="login"
        label="Login"
        autocomplete="username"
        placeholder="seller@example.com"
      />
      <Input
        id="password"
        v-model="password"
        label="Password"
        type="password"
        autocomplete="current-password"
        placeholder="Enter password"
      />
      <p v-if="error" class="login-form__error">{{ error }}</p>
      <Button type="submit" variant="primary" :disabled="!canSubmit" :loading="loading">
        Sign in
      </Button>
    </form>
  </Card>
</template>

<style scoped>
.login-card {
  width: min(100%, 26rem);
  padding: var(--space-6);
}

.login-card__header {
  display: flex;
  align-items: center;
  gap: var(--space-4);
  margin-bottom: var(--space-6);
}

.login-card__mark {
  display: grid;
  height: 2.75rem;
  width: 2.75rem;
  place-items: center;
  border-radius: var(--radius-md);
  background: var(--color-primary);
  color: white;
  font-weight: 800;
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
  background: var(--color-danger-soft);
  color: var(--color-danger);
  padding: var(--space-3);
}
</style>
