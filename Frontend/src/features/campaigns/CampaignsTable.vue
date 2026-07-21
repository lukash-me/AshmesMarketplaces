<script setup lang="ts">
import { computed } from 'vue';

import Badge from '@/shared/ui/Badge.vue';
import Button from '@/shared/ui/Button.vue';
import DataTable from '@/shared/ui/DataTable.vue';

import {
  compactId,
  fieldValue,
  formatDateShort,
  formatNumber,
  getCampaignNeutralTone,
  getCampaignStatusLabel,
  getCampaignTypeLabel
} from './campaignDisplay';
import type { CampaignListItem } from './campaigns.types';

const props = defineProps<{
  rows: CampaignListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  sort: string;
  selectedId?: string | null;
}>();

const emit = defineEmits<{
  sort: [value: string];
  page: [value: number];
  open: [row: CampaignListItem];
}>();

const columns = [
  { key: 'name', label: 'Name', sortable: true, className: 'table__cell--campaign-name' },
  { key: 'status', label: 'Status' },
  { key: 'type', label: 'Type' },
  { key: 'idProduct', label: 'Product ID' },
  { key: 'budget', label: 'Budget', sortable: true, align: 'right' },
  { key: 'region', label: 'Region' },
  { key: 'dateStart', label: 'Start', sortable: true },
  { key: 'dateEnd', label: 'End' },
  { key: 'dateUpdate', label: 'Updated', sortable: true },
  { key: 'id', label: 'Campaign ID' }
];

const pageCount = computed(() => Math.max(1, Math.ceil(props.totalCount / props.pageSize)));
const pageStart = computed(() => (props.totalCount === 0 ? 0 : (props.page - 1) * props.pageSize + 1));
const pageEnd = computed(() => Math.min(props.totalCount, props.page * props.pageSize));
</script>

<template>
  <div class="campaigns-table app-surface">
    <DataTable
      :rows="rows"
      :columns="columns"
      :sort="sort"
      :row-key="(row) => row.id"
      :row-interactive="true"
      :selected-row-key="selectedId"
      :row-aria-label="(row) => `Open campaign ${compactId(row.id)}`"
      @sort="emit('sort', $event)"
      @row-click="emit('open', $event)"
    >
      <template #cell-name="{ value }">
        <span class="campaign-name" :title="String(value)">
          {{ value }}
        </span>
      </template>

      <template #cell-status="{ value }">
        <Badge :tone="getCampaignNeutralTone()">
          {{ getCampaignStatusLabel(Number(value)) }}
        </Badge>
      </template>

      <template #cell-type="{ value }">
        <Badge :tone="getCampaignNeutralTone()">
          {{ getCampaignTypeLabel(Number(value)) }}
        </Badge>
      </template>

      <template #cell-idProduct="{ value }">
        <code class="code-cell" :title="String(value)">{{ compactId(String(value)) }}</code>
      </template>

      <template #cell-budget="{ value }">
        <span class="numeric">{{ formatNumber(value as number | null) }}</span>
      </template>

      <template #cell-region="{ value }">
        <span>{{ fieldValue(value as string | null) }}</span>
      </template>

      <template #cell-dateStart="{ value }">
        <span class="date-cell">
          <span class="numeric">{{ formatDateShort(value ? String(value) : null) }}</span>
        </span>
      </template>

      <template #cell-dateEnd="{ value }">
        <span class="date-cell">
          <span class="numeric">{{ formatDateShort(value ? String(value) : null) }}</span>
        </span>
      </template>

      <template #cell-dateUpdate="{ value }">
        <span class="date-cell">
          <span class="numeric">{{ formatDateShort(String(value)) }}</span>
        </span>
      </template>

      <template #cell-id="{ value }">
        <code class="code-cell" :title="String(value)">{{ compactId(String(value)) }}</code>
      </template>
    </DataTable>

    <footer class="campaigns-table__footer">
      <span class="numeric">Showing {{ pageStart }}-{{ pageEnd }} of {{ totalCount }}</span>
      <div class="campaigns-table__pager">
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
.campaigns-table {
  overflow: hidden;
}

.campaign-name {
  display: inline-block;
  max-width: 22rem;
  overflow: hidden;
  color: var(--color-text);
  font-weight: 680;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.code-cell {
  color: var(--color-text-muted);
  font-family: var(--font-mono);
  font-size: 0.75rem;
  white-space: nowrap;
}

.date-cell {
  color: var(--color-text-muted);
  font-size: 0.8125rem;
}

.campaigns-table__footer {
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

.campaigns-table__pager {
  display: flex;
  align-items: center;
  gap: var(--space-3);
}
</style>
