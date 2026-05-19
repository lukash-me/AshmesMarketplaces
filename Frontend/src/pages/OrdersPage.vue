<script setup lang="ts">
import EmptyState from '@/shared/ui/EmptyState.vue';
import PageHeader from '@/widgets/PageHeader.vue';
</script>

<template>
  <PageHeader title="Orders" description="Order analytics and fulfillment status." />
  <EmptyState class="app-surface" title="Orders page placeholder" description="This route is reserved for the next vertical slice." />
</template>
