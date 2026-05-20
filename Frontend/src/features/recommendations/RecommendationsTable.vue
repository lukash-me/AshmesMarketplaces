<script setup lang="ts">
import { computed } from 'vue';

import Badge from '@/shared/ui/Badge.vue';
import Button from '@/shared/ui/Button.vue';
import DataTable from '@/shared/ui/DataTable.vue';

import {
  compactId,
  formatDateShort,
  formatScore,
  getRecommendationNeutralTone,
  getRecommendationObjectTypeLabel,
  getRecommendationTypeLabel
} from './recommendationDisplay';
import type { RecommendationListItem } from './recommendations.types';

const props = defineProps<{
  rows: RecommendationListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  sort: string;
  selectedId?: string | null;
}>();

const emit = defineEmits<{
  sort: [value: string];
  page: [value: number];
  open: [row: RecommendationListItem];
}>();

const columns = [
  { key: 'score', label: 'Score', sortable: true, align: 'right' },
  { key: 'type', label: 'Type' },
  { key: 'typeObject', label: 'Object type' },
  { key: 'idModel', label: 'Model ID' },
  { key: 'dateCreate', label: 'Created', sortable: true },
  { key: 'dateUpdate', label: 'Updated', sortable: true },
  { key: 'id', label: 'Recommendation ID' }
];

const pageCount = computed(() => Math.max(1, Math.ceil(props.totalCount / props.pageSize)));
const pageStart = computed(() => (props.totalCount === 0 ? 0 : (props.page - 1) * props.pageSize + 1));
const pageEnd = computed(() => Math.min(props.totalCount, props.page * props.pageSize));
</script>

<template>
  <div class="recommendations-table app-surface">
    <DataTable
      :rows="rows"
      :columns="columns"
      :sort="sort"
      :row-key="(row) => row.id"
      :row-interactive="true"
      :selected-row-key="selectedId"
      :row-aria-label="(row) => `Open recommendation ${compactId(row.id)}`"
      @sort="emit('sort', $event)"
      @row-click="emit('open', $event)"
    >
      <template #cell-score="{ value }">
        <span class="numeric">{{ formatScore(Number(value)) }}</span>
      </template>

      <template #cell-type="{ value }">
        <Badge class="status-badge" :tone="getRecommendationNeutralTone()">
          {{ getRecommendationTypeLabel(Number(value)) }}
        </Badge>
      </template>

      <template #cell-typeObject="{ value }">
        <Badge class="status-badge" :tone="getRecommendationNeutralTone()">
          {{ getRecommendationObjectTypeLabel(Number(value)) }}
        </Badge>
      </template>

      <template #cell-idModel="{ value }">
        <code class="code-cell" :title="String(value)">{{ compactId(String(value)) }}</code>
      </template>

      <template #cell-dateCreate="{ value }">
        <span class="date-cell">
          <span class="numeric">{{ formatDateShort(String(value)) }}</span>
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

    <footer class="recommendations-table__footer">
      <span class="numeric">Showing {{ pageStart }}-{{ pageEnd }} of {{ totalCount }}</span>
      <div class="recommendations-table__pager">
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
.recommendations-table {
  overflow: hidden;
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

.status-badge {
  white-space: nowrap;
}

.recommendations-table__footer {
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

.recommendations-table__pager {
  display: flex;
  align-items: center;
  gap: var(--space-3);
}
</style>
