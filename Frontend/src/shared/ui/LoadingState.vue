<script setup lang="ts">
withDefaults(
  defineProps<{
    rows?: number;
  }>(),
  {
    rows: 5
  }
);
</script>

<template>
  <div class="loading">
    <div v-for="row in rows" :key="row" class="loading__row">
      <span class="loading__cell loading__cell--wide" />
      <span class="loading__cell" />
      <span class="loading__cell" />
      <span class="loading__cell loading__cell--short" />
    </div>
  </div>
</template>

<style scoped>
.loading {
  display: grid;
  gap: var(--space-2);
  padding: var(--space-4);
}

.loading__row {
  display: grid;
  grid-template-columns: minmax(12rem, 1fr) 8rem 8rem 5rem;
  gap: var(--space-4);
}

.loading__cell {
  height: 0.875rem;
  border-radius: 999px;
  background: var(--background-skeleton);
  background-size: 220% 100%;
  animation: shimmer 1.2s ease-in-out infinite;
}

.loading__cell--wide {
  height: 1rem;
}

.loading__cell--short {
  width: 4rem;
}

@keyframes shimmer {
  to {
    background-position-x: -220%;
  }
}

@media (max-width: 720px) {
  .loading__row {
    grid-template-columns: 1fr;
  }
}
</style>
