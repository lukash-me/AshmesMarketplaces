<script setup lang="ts">
withDefaults(
  defineProps<{
    tone?: 'neutral' | 'success' | 'warning' | 'danger' | 'info';
  }>(),
  {
    tone: 'neutral'
  }
);
</script>

<template>
  <span class="badge" :class="`badge--${tone}`">
    <slot />
  </span>
</template>

<style scoped>
.badge {
  display: inline-flex;
  align-items: center;
  border-radius: 999px;
  padding: 0.1875rem 0.5rem;
  font-size: 0.75rem;
  font-weight: 650;
  line-height: 1;
}

.badge--neutral {
  background: var(--color-surface-muted);
  color: var(--color-text-muted);
}

.badge--success {
  background: var(--color-success-soft);
  color: var(--color-success);
}

.badge--warning {
  background: var(--color-warning-soft);
  color: var(--color-warning);
}

.badge--danger {
  background: var(--color-danger-soft);
  color: var(--color-danger);
}

.badge--info {
  background: var(--color-info-soft);
  color: var(--color-info);
}
</style>
