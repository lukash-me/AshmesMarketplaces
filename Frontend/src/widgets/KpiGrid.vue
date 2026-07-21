<script setup lang="ts">
const props = withDefaults(defineProps<{
  items: Array<{
    label: string;
    value: string | number;
    caption?: string;
  }>;
  variant?: 'default' | 'market';
}>(), {
  variant: 'default'
});
</script>

<template>
  <div class="kpi-grid" :class="`kpi-grid--${props.variant}`">
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
  gap: var(--space-2);
}

.kpi-card {
  display: grid;
  gap: var(--space-1);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--background-panel-highlight);
  min-width: 0;
  padding: var(--space-2) var(--space-3);
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
  font-size: 1.12rem;
  font-weight: 760;
}

.kpi-grid--market {
  gap: var(--space-3);
}

.kpi-grid--market .kpi-card {
  position: relative;
  overflow: hidden;
  gap: var(--space-2);
  padding: var(--space-4);
  border-color: rgb(249 115 22 / 0.26);
  background:
    linear-gradient(135deg, rgb(249 115 22 / 0.1), transparent 42%),
    var(--color-surface);
}

.kpi-grid--market .kpi-card::before {
  content: '';
  position: absolute;
  inset: 0 auto 0 0;
  width: 3px;
  background: var(--accent-ember);
}

.kpi-grid--market .kpi-card span,
.kpi-grid--market .kpi-card small {
  font-weight: 700;
}

.kpi-grid--market .kpi-card strong {
  margin-top: var(--space-1);
  font-size: 1.55rem;
  line-height: 1.05;
  font-variant-numeric: tabular-nums;
}

.kpi-grid--market .kpi-card small {
  margin-top: 0;
  font-size: 0.8125rem;
  line-height: 1.4;
}

@media (min-width: 720px) {
  .kpi-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (min-width: 1180px) {
  .kpi-grid {
    grid-template-columns: repeat(auto-fit, minmax(9.75rem, 1fr));
  }
}
</style>
