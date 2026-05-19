<script setup lang="ts">
withDefaults(
  defineProps<{
    tone?: 'neutral' | 'success' | 'warning' | 'danger' | 'info' | 'ember' | 'hot';
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
  border: 1px solid transparent;
  border-radius: var(--radius-sm);
  padding: 0.1875rem 0.45rem;
  font-size: 0.75rem;
  font-weight: 650;
  line-height: 1;
}

.badge--neutral {
  border-color: var(--heat-dormant-border);
  background: var(--heat-dormant-soft);
  color: var(--heat-dormant-text);
}

.badge--success {
  border-color: var(--state-success-border);
  background: var(--state-success-soft);
  color: var(--state-success);
}

.badge--warning {
  border-color: var(--state-warning-border);
  background: var(--state-warning-soft);
  color: var(--state-warning);
}

.badge--danger {
  border-color: var(--state-danger-border);
  background: var(--state-danger-soft);
  color: var(--state-danger);
}

.badge--info {
  border-color: var(--state-info-border);
  background: var(--state-info-soft);
  color: var(--state-info);
}

.badge--ember {
  border-color: var(--heat-rising-border);
  background: var(--heat-rising-soft);
  color: var(--heat-rising-text);
}

.badge--hot {
  border-color: var(--heat-hot-border);
  background: var(--heat-hot-soft);
  color: var(--heat-hot-text);
}
</style>
