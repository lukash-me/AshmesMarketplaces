<script setup lang="ts">
import { LogOut, Menu, Search } from 'lucide-vue-next';
import { computed } from 'vue';
import { useRouter } from 'vue-router';

import { useAuthStore } from '@/features/auth/auth.store';

defineEmits<{
  menu: [];
}>();

const auth = useAuthStore();
const router = useRouter();

const userLabel = computed(() => auth.user?.login ?? 'Signed in');

async function signOut() {
  await auth.logout();
  await router.push({ name: 'login' });
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
    <div class="topbar__user">
      <span class="topbar__status">Workspace</span>
      <span>{{ userLabel }}</span>
      <button class="app-icon-button" type="button" title="Sign out" @click="signOut">
        <LogOut :size="17" />
      </button>
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
