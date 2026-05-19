<script setup lang="ts">
withDefaults(
  defineProps<{
    type?: 'button' | 'submit';
    variant?: 'primary' | 'secondary' | 'ghost' | 'danger';
    disabled?: boolean;
    loading?: boolean;
  }>(),
  {
    type: 'button',
    variant: 'secondary',
    disabled: false,
    loading: false
  }
);
</script>

<template>
  <button
    :type="type"
    class="button"
    :class="`button--${variant}`"
    :disabled="disabled || loading"
  >
    <span v-if="loading" class="button__spinner" />
    <slot />
  </button>
</template>

<style scoped>
.button {
  display: inline-flex;
  min-height: 2.25rem;
  align-items: center;
  justify-content: center;
  gap: var(--space-2);
  border: 1px solid transparent;
  border-radius: var(--radius-md);
  padding: 0 var(--space-4);
  font-weight: 600;
  line-height: 1;
  transition: background-color 140ms ease, border-color 140ms ease, color 140ms ease;
}

.button:focus-visible {
  outline: none;
  box-shadow: var(--focus-ring);
}

.button:disabled {
  cursor: not-allowed;
  opacity: 0.6;
}

.button--primary {
  background: var(--color-primary);
  color: white;
}

.button--primary:hover:not(:disabled) {
  background: var(--color-primary-hover);
}

.button--secondary {
  border-color: var(--color-border);
  background: var(--color-surface);
  color: var(--color-text);
}

.button--secondary:hover:not(:disabled),
.button--ghost:hover:not(:disabled) {
  background: var(--color-surface-muted);
}

.button--ghost {
  background: transparent;
  color: var(--color-text-muted);
}

.button--danger {
  background: var(--color-danger);
  color: white;
}

.button__spinner {
  height: 0.875rem;
  width: 0.875rem;
  border: 2px solid currentColor;
  border-right-color: transparent;
  border-radius: 999px;
  animation: spin 700ms linear infinite;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}
</style>
