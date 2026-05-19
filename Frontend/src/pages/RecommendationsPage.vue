<script setup lang="ts">
import EmptyState from '@/shared/ui/EmptyState.vue';
import PageHeader from '@/widgets/PageHeader.vue';
</script>

<template>
  <PageHeader title="Recommendations" description="Recommendation records and model output review." />
  <EmptyState class="app-surface" title="Recommendations page placeholder" description="ML execution UI is out of scope for this stage." />
</template>
