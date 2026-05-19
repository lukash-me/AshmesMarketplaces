<script setup lang="ts">
import EmptyState from '@/shared/ui/EmptyState.vue';
import PageHeader from '@/widgets/PageHeader.vue';
</script>

<template>
  <PageHeader title="Expenses" description="Operational expenses and cost categories." />
  <EmptyState class="app-surface" title="Expenses page placeholder" description="This route is reserved for the next vertical slice." />
</template>
