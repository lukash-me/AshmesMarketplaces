<script setup lang="ts">
import { computed } from 'vue';

import Badge from '@/shared/ui/Badge.vue';
import Button from '@/shared/ui/Button.vue';
import DataTable from '@/shared/ui/DataTable.vue';

import {
  compactId,
  getAccessNeutralTone,
  getRoleLabel,
  getUserLabel,
  getWorkspaceLabel,
  membershipKey,
  type RoleLookup,
  type UserLookup,
  type WorkspaceLookup
} from './accessSettingsDisplay';
import type { UserWorkspaceListItem } from './accessSettings.types';

const props = defineProps<{
  rows: UserWorkspaceListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  sort: string;
  selectedKey?: string | null;
  usersById: UserLookup;
  workspacesById: WorkspaceLookup;
  rolesById: RoleLookup;
}>();

const emit = defineEmits<{
  sort: [value: string];
  page: [value: number];
  open: [row: UserWorkspaceListItem];
}>();

const columns = [
  { key: 'user', label: 'User' },
  { key: 'workspace', label: 'Workspace' },
  { key: 'role', label: 'Role' },
  { key: 'idUser', label: 'User ID', sortable: true },
  { key: 'idWorkspace', label: 'Workspace ID', sortable: true },
  { key: 'idRole', label: 'Role ID' }
];

const pageCount = computed(() => Math.max(1, Math.ceil(props.totalCount / props.pageSize)));
const pageStart = computed(() => (props.totalCount === 0 ? 0 : (props.page - 1) * props.pageSize + 1));
const pageEnd = computed(() => Math.min(props.totalCount, props.page * props.pageSize));
</script>

<template>
  <div class="access-table app-surface">
    <DataTable
      :rows="rows"
      :columns="columns"
      :sort="sort"
      :row-key="(row) => membershipKey(row.idUser, row.idWorkspace)"
      :row-interactive="true"
      :selected-row-key="selectedKey"
      :row-aria-label="(row) => `Open workspace access for user ${compactId(row.idUser)}`"
      @sort="emit('sort', $event)"
      @row-click="emit('open', $event)"
    >
      <template #cell-user="{ row }">
        <span class="primary-cell">{{ getUserLabel(row.idUser, usersById) }}</span>
      </template>

      <template #cell-workspace="{ row }">
        <span class="primary-cell">{{ getWorkspaceLabel(row.idWorkspace, workspacesById) }}</span>
      </template>

      <template #cell-role="{ row }">
        <Badge class="status-badge" :tone="getAccessNeutralTone()">
          {{ getRoleLabel(row.idRole, rolesById) }}
        </Badge>
      </template>

      <template #cell-idUser="{ value }">
        <code class="code-cell" :title="String(value)">{{ compactId(String(value)) }}</code>
      </template>

      <template #cell-idWorkspace="{ value }">
        <code class="code-cell" :title="String(value)">{{ compactId(String(value)) }}</code>
      </template>

      <template #cell-idRole="{ value }">
        <code class="code-cell" :title="String(value)">{{ compactId(String(value)) }}</code>
      </template>
    </DataTable>

    <footer class="access-table__footer">
      <span class="numeric">Showing {{ pageStart }}-{{ pageEnd }} of {{ totalCount }}</span>
      <div class="access-table__pager">
        <Button variant="secondary" :disabled="page <= 1" @click="emit('page', page - 1)">
          Previous
        </Button>
        <span class="numeric">Page {{ page }} / {{ pageCount }}</span>
        <Button variant="secondary" :disabled="page >= pageCount" @click="emit('page', page + 1)">
          Next
        </Button>
      </div>
    </footer>
  </div>
</template>

<style scoped>
.access-table {
  overflow: hidden;
}

.primary-cell {
  display: inline-block;
  max-width: 18rem;
  overflow: hidden;
  color: var(--color-text);
  font-weight: 650;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.code-cell {
  color: var(--color-text-muted);
  font-family: var(--font-mono);
  font-size: 0.75rem;
  white-space: nowrap;
}

.status-badge {
  max-width: 14rem;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.access-table__footer {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-3);
  border-top: 1px solid var(--color-border);
  color: var(--color-text-muted);
  padding: var(--space-3);
  font-size: 0.8125rem;
}

.access-table__pager {
  display: flex;
  align-items: center;
  gap: var(--space-3);
}
</style>

