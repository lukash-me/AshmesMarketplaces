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
      <span>Search products, orders, campaigns</span>
    </div>
    <div class="topbar__user">
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
  min-height: 4rem;
  border-bottom: 1px solid var(--color-border);
  background: rgb(255 255 255 / 0.86);
  padding: 0 var(--space-4);
  backdrop-filter: blur(16px);
}

.topbar__search {
  display: none;
  align-items: center;
  gap: var(--space-2);
  height: 2.25rem;
  max-width: 28rem;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  color: var(--color-text-subtle);
  padding: 0 var(--space-3);
}

.topbar__user {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  color: var(--color-text-muted);
  font-weight: 600;
}

@media (min-width: 720px) {
  .topbar__search {
    display: flex;
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
