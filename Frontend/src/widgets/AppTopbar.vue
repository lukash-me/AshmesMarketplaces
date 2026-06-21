<script setup lang="ts">
import { LogOut, Menu, Search } from 'lucide-vue-next';
import { computed } from 'vue';

import { useAuthPromptStore } from '@/features/auth/authPrompt.store';
import { useAuthStore } from '@/features/auth/auth.store';

defineEmits<{
  menu: [];
}>();

const auth = useAuthStore();
const authPrompt = useAuthPromptStore();

const isSignedIn = computed(() => auth.isAuthenticated && Boolean(auth.user));
const userLabel = computed(() => auth.user?.login ?? '');

async function signOut() {
  await auth.logout();
}
</script>

<template>
  <header class="topbar">
    <button class="app-icon-button topbar__menu" type="button" @click="$emit('menu')">
      <Menu :size="20" />
    </button>
    <div class="topbar__search">
      <Search :size="16" />
      <span>Search workspace</span>
    </div>
    <div v-if="isSignedIn" class="topbar__user">
      <span class="topbar__status">Workspace</span>
      <span>{{ userLabel }}</span>
      <button class="app-icon-button" type="button" title="Sign out" @click="signOut">
        <LogOut :size="17" />
      </button>
    </div>
    <div v-else class="topbar__guest">
      <button class="topbar__guest-link" type="button" @click="authPrompt.showLogin()">Войти</button>
      <button class="topbar__guest-button" type="button" @click="authPrompt.showRegister()">Регистрация</button>
    </div>
  </header>
</template>

<style scoped>
.topbar {
  position: sticky;
  top: 0;
  z-index: 20;
  display: grid;
  grid-template-columns: auto minmax(0, 1fr) auto;
  align-items: center;
  gap: var(--space-3);
  min-height: 3.5rem;
  border-bottom: 1px solid var(--color-border);
  background: var(--surface-topbar);
  padding: 0 var(--space-4);
  backdrop-filter: blur(16px);
}

.topbar__search {
  display: none;
  align-items: center;
  gap: var(--space-2);
  height: 2.125rem;
  max-width: 28rem;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--surface-control);
  color: var(--color-text-subtle);
  padding: 0 var(--space-3);
  font-size: 0.8125rem;
}

.topbar__user {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  color: var(--color-text-muted);
  font-size: 0.8125rem;
  font-weight: 650;
}

.topbar__guest {
  display: flex;
  align-items: center;
  gap: var(--space-2);
}

.topbar__guest-link,
.topbar__guest-button {
  min-height: 2rem;
  border-radius: var(--radius-md);
  padding: 0 var(--space-3);
  font: inherit;
  font-size: 0.8125rem;
  font-weight: 720;
}

.topbar__guest-link {
  border: 1px solid transparent;
  background: transparent;
  color: var(--accent-ember-text-strong);
}

.topbar__guest-button {
  border: 1px solid var(--accent-primary-border);
  background: var(--button-primary-bg);
  color: var(--text-on-fire);
}

.topbar__status {
  display: none;
  border: 1px solid var(--state-success-border);
  border-radius: var(--radius-sm);
  background: var(--state-success-soft);
  color: var(--state-success-text);
  padding: 0.25rem 0.45rem;
}

@media (min-width: 720px) {
  .topbar__search {
    display: flex;
  }

  .topbar__status {
    display: inline-flex;
  }
}

@media (min-width: 1024px) {
  .topbar__menu {
    display: none;
  }

  .topbar {
    padding: 0 var(--space-6);
  }
}
</style>
