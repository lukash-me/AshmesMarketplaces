<script setup lang="ts">
import Button from '@/shared/ui/Button.vue';

import { useAuthPromptStore } from './authPrompt.store';

withDefaults(
  defineProps<{
    title?: string;
    description: string;
  }>(),
  {
    title: 'Войдите, чтобы пользоваться разделом'
  }
);

const prompt = useAuthPromptStore();
</script>

<template>
  <section class="auth-required app-surface">
    <div>
      <h2>{{ title }}</h2>
      <p>{{ description }}</p>
    </div>
    <div class="auth-required__actions">
      <Button type="button" variant="primary" @click="prompt.showLogin()">Авторизоваться</Button>
      <Button type="button" variant="secondary" @click="prompt.showRegister()">Регистрация</Button>
    </div>
  </section>
</template>

<style scoped>
.auth-required {
  display: grid;
  gap: var(--space-4);
  max-width: 46rem;
  padding: var(--space-5);
}

.auth-required h2 {
  margin: 0 0 var(--space-2);
  font-size: 1.05rem;
}

.auth-required p {
  max-width: 42rem;
  margin: 0;
  color: var(--color-text-muted);
  line-height: 1.55;
}

.auth-required__actions {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
}
</style>
