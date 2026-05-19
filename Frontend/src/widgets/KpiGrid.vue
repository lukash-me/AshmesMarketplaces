<script setup lang="ts">
defineProps<{
  items: Array<{
    label: string;
    value: string | number;
    caption?: string;
  }>;
}>();
</script>

<template>
  <div class="kpi-grid">
    <section v-for="item in items" :key="item.label" class="kpi-card">
      <span>{{ item.label }}</span>
      <strong class="numeric">{{ item.value }}</strong>
      <small v-if="item.caption">{{ item.caption }}</small>
    </section>
  </div>
</template>

<style scoped>
.kpi-grid {
  display: grid;
  grid-template-columns: repeat(1, minmax(0, 1fr));
  gap: var(--space-3);
}

.kpi-card {
  display: grid;
  gap: var(--space-1);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--background-panel-highlight);
  padding: var(--space-3);
  box-shadow: var(--shadow-panel);
  backdrop-filter: blur(18px);
}

.kpi-card span,
.kpi-card small {
  color: var(--color-text-muted);
  font-size: 0.75rem;
  font-weight: 650;
}

.kpi-card strong {
  color: var(--color-text);
  font-size: 1.35rem;
  font-weight: 760;
}

@media (min-width: 720px) {
  .kpi-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (min-width: 1180px) {
  .kpi-grid {
    grid-template-columns: repeat(4, minmax(0, 1fr));
  }
}
</style>
