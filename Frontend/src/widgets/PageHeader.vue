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
  gap: var(--space-4);
  margin-bottom: var(--space-5);
}

h1 {
  margin: 0;
  font-size: clamp(1.35rem, 1.2rem + 0.4vw, 1.75rem);
  font-weight: 750;
  letter-spacing: 0;
}

p {
  margin: var(--space-2) 0 0;
  color: var(--color-text-muted);
}

.page-header__actions {
  display: flex;
  align-items: center;
  gap: var(--space-2);
}
</style>
