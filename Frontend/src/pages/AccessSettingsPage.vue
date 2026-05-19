<script setup lang="ts">
import EmptyState from '@/shared/ui/EmptyState.vue';
import PageHeader from '@/widgets/PageHeader.vue';
</script>

<template>
  <PageHeader title="Access" description="Users, roles, permissions and workspace access." />
  <EmptyState class="app-surface" title="Access settings placeholder" description="Full RBAC management will be implemented as a dedicated stage." />
</template>
