<script setup lang="ts">
defineProps<{
  title: string;
  description?: string;
}>();
</script>

<template>
  <div class="empty">
    <div class="empty__mark" />
    <h3 class="empty__title">{{ title }}</h3>
    <p v-if="description" class="empty__description">{{ description }}</p>
    <slot />
  </div>
</template>

<style scoped>
.empty {
  display: grid;
  min-height: 14rem;
  place-items: center;
  align-content: center;
  gap: var(--space-3);
  padding: var(--space-8);
  text-align: center;
}

.empty__mark {
  height: 2.25rem;
  width: 2.25rem;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: linear-gradient(135deg, var(--color-surface), var(--color-surface-muted));
}

.empty__title {
  margin: 0;
  font-size: 1rem;
  font-weight: 700;
}

.empty__description {
  margin: 0;
  max-width: 30rem;
  color: var(--color-text-muted);
}
</style>
