<script setup lang="ts">
import { Check, ChevronDown, LogOut, Menu, Search } from 'lucide-vue-next';
import { computed, onBeforeUnmount, onMounted, ref } from 'vue';

import { useAuthPromptStore } from '@/features/auth/authPrompt.store';
import { useAuthStore } from '@/features/auth/auth.store';
import { useActiveWorkspace } from '@/features/workspace-market-products/useActiveWorkspace';

defineEmits<{
  menu: [];
}>();

const auth = useAuthStore();
const authPrompt = useAuthPromptStore();
const workspace = useActiveWorkspace();
const workspacePickerOpen = ref(false);
const workspacePickerRef = ref<HTMLElement | null>(null);

const isSignedIn = computed(() => auth.isAuthenticated && Boolean(auth.user));
const userLabel = computed(() => auth.user?.login ?? '');
const workspaceLabel = computed(() => workspace.activeWorkspaceName.value);

async function signOut() {
  workspacePickerOpen.value = false;
  await auth.logout();
}

function toggleWorkspacePicker(): void {
  workspacePickerOpen.value = !workspacePickerOpen.value;
}

function selectWorkspace(workspaceId: string): void {
  workspace.selectWorkspace(workspaceId);
  workspacePickerOpen.value = false;
}

function handleDocumentClick(event: MouseEvent): void {
  if (!workspacePickerOpen.value || !workspacePickerRef.value) {
    return;
  }

  if (!workspacePickerRef.value.contains(event.target as Node)) {
    workspacePickerOpen.value = false;
  }
}

function handleKeydown(event: KeyboardEvent): void {
  if (event.key === 'Escape') {
    workspacePickerOpen.value = false;
  }
}

onMounted(() => {
  document.addEventListener('click', handleDocumentClick);
  document.addEventListener('keydown', handleKeydown);
});

onBeforeUnmount(() => {
  document.removeEventListener('click', handleDocumentClick);
  document.removeEventListener('keydown', handleKeydown);
});
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
      <div ref="workspacePickerRef" class="topbar__workspace">
        <button
          class="topbar__workspace-button"
          type="button"
          :aria-expanded="workspacePickerOpen"
          @click.stop="toggleWorkspacePicker"
        >
          <span>{{ workspaceLabel }}</span>
          <ChevronDown :size="15" />
        </button>
        <div v-if="workspacePickerOpen" class="topbar__workspace-menu">
          <button
            v-for="option in workspace.workspaceOptions.value"
            :key="option.idWorkspace"
            class="topbar__workspace-option"
            :class="{ 'topbar__workspace-option--active': option.idWorkspace === workspace.activeWorkspaceId.value }"
            type="button"
            @click="selectWorkspace(option.idWorkspace)"
          >
            <span>{{ workspace.getWorkspaceDisplayName(option) }}</span>
            <Check v-if="option.idWorkspace === workspace.activeWorkspaceId.value" :size="15" />
          </button>
          <div v-if="workspace.workspaceOptions.value.length === 0" class="topbar__workspace-empty">
            Вне рабочей области
          </div>
        </div>
      </div>
      <span>{{ userLabel }}</span>
      <button class="app-icon-button" type="button" title="Sign out" @click="signOut">
        <LogOut :size="17" />
      </button>
    </div>
    <div v-else class="topbar__guest">
      <button class="topbar__guest-link" type="button" @click="authPrompt.showLogin()">Вход</button>
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
  min-width: 0;
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

.topbar__workspace {
  position: relative;
  min-width: 0;
}

.topbar__workspace-button {
  display: inline-flex;
  align-items: center;
  gap: var(--space-1);
  max-width: 14rem;
  border: 1px solid var(--state-success-border);
  border-radius: var(--radius-sm);
  background: var(--state-success-soft);
  color: var(--state-success-text);
  padding: 0.25rem 0.45rem;
  font: inherit;
}

.topbar__workspace-button span {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.topbar__workspace-menu {
  position: absolute;
  top: calc(100% + var(--space-2));
  left: 0;
  z-index: 45;
  display: grid;
  gap: var(--space-1);
  min-width: 16rem;
  max-width: min(24rem, calc(100vw - 2rem));
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--surface-panel);
  box-shadow: var(--shadow-lg);
  padding: var(--space-2);
}

.topbar__workspace-option {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-3);
  width: 100%;
  border: 0;
  border-radius: var(--radius-sm);
  background: transparent;
  color: var(--color-text);
  padding: var(--space-2);
  font: inherit;
  text-align: left;
}

.topbar__workspace-option:hover,
.topbar__workspace-option--active {
  background: var(--accent-ember-soft);
  color: var(--accent-ember-text-strong);
}

.topbar__workspace-empty {
  color: var(--color-text-muted);
  padding: var(--space-2);
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
