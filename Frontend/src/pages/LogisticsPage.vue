<script setup lang="ts">
import EmptyState from '@/shared/ui/EmptyState.vue';
import PageHeader from '@/widgets/PageHeader.vue';
</script>

<template>
  <PageHeader title="Logistics" description="Stock, warehouses, storage and delivery costs." />
  <EmptyState class="app-surface" title="Logistics page placeholder" description="This route is reserved for the next vertical slice." />
</template>
