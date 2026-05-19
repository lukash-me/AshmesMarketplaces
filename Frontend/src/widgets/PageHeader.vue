<script setup lang="ts">
defineProps<{
  title: string;
  description?: string;
}>();
</script>

<template>
  <div class="page-header">
    <div>
      <h1>{{ title }}</h1>
      <p v-if="description">{{ description }}</p>
    </div>
    <div class="page-header__actions">
      <slot />
    </div>
  </div>
</template>

<style scoped>
.page-header {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-end;
  justify-content: space-between;
  gap: var(--space-3);
  margin-bottom: var(--space-4);
}

h1 {
  margin: 0;
  font-size: 1.375rem;
  font-weight: 750;
  letter-spacing: 0;
}

p {
  margin: var(--space-1) 0 0;
  color: var(--color-text-muted);
  font-size: 0.875rem;
}

.page-header__actions {
  display: flex;
  align-items: center;
  gap: var(--space-2);
}
</style>
