<script setup lang="ts">
import EmptyState from '@/shared/ui/EmptyState.vue';
import KpiGrid from '@/widgets/KpiGrid.vue';
import PageHeader from '@/widgets/PageHeader.vue';

const kpis = [
  { label: 'Revenue', value: '-', caption: 'Pending analytics API' },
  { label: 'Orders', value: '-', caption: 'Pending orders slice' },
  { label: 'Reviews', value: '-', caption: 'Pending reviews slice' },
  { label: 'Ad spend', value: '-', caption: 'Pending campaigns slice' }
];
</script>

<template>
  <div class="page">
    <PageHeader
      title="Overview"
      description="Workspace snapshot for marketplace sales, operations and recommendations."
    />
    <KpiGrid :items="kpis" />
    <EmptyState
      class="app-surface"
      title="Analytics modules are staged"
      description="Products are implemented first; the remaining analytical views will attach to their API slices in later stages."
    />
  </div>
</template>

<style scoped>
.page {
  display: grid;
  gap: var(--space-5);
}
</style>
