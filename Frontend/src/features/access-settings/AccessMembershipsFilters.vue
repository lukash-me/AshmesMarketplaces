<script setup lang="ts">
import { computed, reactive, watch } from 'vue';

import Button from '@/shared/ui/Button.vue';
import Input from '@/shared/ui/Input.vue';

import {
  compactId,
  getAccessSortLabel,
  getRoleLabel,
  getUserLabel,
  getWorkspaceLabel,
  type RoleLookup,
  type UserLookup,
  type WorkspaceLookup
} from './accessSettingsDisplay';
import type { AccessSettingsQueryFilterKey } from './accessSettingsQuery';
import type { AccessSettingsQueryState } from './accessSettings.types';

const props = defineProps<{
  state: AccessSettingsQueryState;
  usersById: UserLookup;
  workspacesById: WorkspaceLookup;
  rolesById: RoleLookup;
}>();

const emit = defineEmits<{
  apply: [patch: Partial<AccessSettingsQueryState>];
  reset: [];
  remove: [key: AccessSettingsQueryFilterKey];
}>();

const form = reactive({
  search: props.state.search,
  idUser: props.state.idUser,
  idWorkspace: props.state.idWorkspace,
  idRole: props.state.idRole
});

watch(
  () => props.state,
  (state) => {
    form.search = state.search;
    form.idUser = state.idUser;
    form.idWorkspace = state.idWorkspace;
    form.idRole = state.idRole;
  },
  { deep: true }
);

const activeChips = computed(() => {
  const chips: Array<{ key: AccessSettingsQueryFilterKey; label: string; value: string }> = [];

  if (props.state.search) {
    chips.push({ key: 'search', label: 'Search', value: props.state.search });
  }

  if (props.state.idUser) {
    chips.push({
      key: 'idUser',
      label: 'User',
      value: getUserLabel(props.state.idUser, props.usersById)
    });
  }

  if (props.state.idWorkspace) {
    chips.push({
      key: 'idWorkspace',
      label: 'Workspace',
      value: getWorkspaceLabel(props.state.idWorkspace, props.workspacesById)
    });
  }

  if (props.state.idRole) {
    chips.push({
      key: 'idRole',
      label: 'Role',
      value: getRoleLabel(props.state.idRole, props.rolesById)
    });
  }

  if (props.state.sort) {
    chips.push({ key: 'sort', label: 'Sort', value: getAccessSortLabel(props.state.sort) });
  }

  return chips;
});

const hasActiveState = computed(() => activeChips.value.length > 0);

function apply() {
  emit('apply', {
    page: 1,
    search: form.search.trim(),
    idUser: form.idUser.trim(),
    idWorkspace: form.idWorkspace.trim(),
    idRole: form.idRole.trim()
  });
}
</script>

<template>
  <form class="filters app-surface" @submit.prevent="apply">
    <div class="filters__toolbar">
      <Input v-model="form.search" label="Search" placeholder="User login, email or workspace" />

      <div class="filters__actions">
        <Button type="submit" variant="primary">Apply</Button>
        <Button v-if="hasActiveState" type="button" variant="ghost" @click="$emit('reset')">
          Reset
        </Button>
      </div>
    </div>

    <div class="filters__ids">
      <Input v-model="form.idUser" label="User ID" placeholder="uuid" />
      <Input v-model="form.idWorkspace" label="Workspace ID" placeholder="uuid" />
      <Input v-model="form.idRole" label="Role ID" placeholder="uuid" />
    </div>

    <div v-if="activeChips.length" class="filters__chips" aria-label="Active access filters">
      <button
        v-for="chip in activeChips"
        :key="chip.key"
        class="filters__chip"
        type="button"
        :title="`Remove ${chip.label}: ${chip.value}`"
        @click="emit('remove', chip.key)"
      >
        <span>{{ chip.label }}</span>
        <strong>{{ chip.key === 'idUser' ? compactId(chip.value) : chip.value }}</strong>
        <span aria-hidden="true">x</span>
      </button>
    </div>
  </form>
</template>

<style scoped>
.filters {
  display: grid;
  gap: var(--space-3);
  padding: var(--space-3);
}

.filters__toolbar,
.filters__ids {
  display: grid;
  grid-template-columns: repeat(1, minmax(0, 1fr));
  gap: var(--space-3);
}

.filters__actions {
  display: flex;
  align-items: end;
  gap: var(--space-2);
}

.filters__chips {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
}

.filters__chip {
  display: inline-flex;
  max-width: 100%;
  align-items: center;
  gap: var(--space-1);
  border: 1px solid var(--color-border);
  border-radius: 999px;
  background: var(--surface-control-raised);
  color: var(--color-text-muted);
  padding: 0.35rem var(--space-2);
  font-size: 0.75rem;
  line-height: 1;
}

.filters__chip:hover,
.filters__chip:focus-visible {
  border-color: var(--color-border-strong);
  background: var(--color-surface-hover);
  color: var(--color-text);
  outline: none;
}

.filters__chip strong {
  min-width: 0;
  overflow: hidden;
  color: var(--color-text);
  font-family: var(--font-mono);
  font-weight: 650;
  text-overflow: ellipsis;
  white-space: nowrap;
}

@media (min-width: 840px) {
  .filters__toolbar {
    grid-template-columns: minmax(18rem, 1fr) auto;
    align-items: end;
  }

  .filters__ids {
    grid-template-columns: repeat(3, minmax(10rem, 1fr));
  }
}
</style>

