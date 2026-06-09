<script setup lang="ts">
import { CircleHelp } from 'lucide-vue-next';

defineProps<{
  text: string;
}>();
</script>

<template>
  <span class="help-tooltip">
    <button class="help-tooltip__trigger" type="button" :aria-label="text">
      <CircleHelp :size="15" />
    </button>
    <span class="help-tooltip__bubble" role="tooltip">{{ text }}</span>
  </span>
</template>

<style scoped>
.help-tooltip {
  position: relative;
  display: inline-flex;
  align-items: center;
  vertical-align: middle;
}

.help-tooltip__trigger {
  display: inline-flex;
  width: 1.25rem;
  height: 1.25rem;
  align-items: center;
  justify-content: center;
  border: 1px solid var(--color-border);
  border-radius: 999px;
  background: var(--surface-control);
  color: var(--color-text-muted);
  cursor: help;
  padding: 0;
  transition: border-color 140ms ease, background-color 140ms ease, color 140ms ease;
}

.help-tooltip__trigger:hover,
.help-tooltip__trigger:focus-visible {
  border-color: var(--accent-ember-border);
  background: var(--accent-ember-hover-bg);
  color: var(--accent-ember-text-strong);
}

.help-tooltip__trigger:focus-visible {
  outline: none;
  box-shadow: var(--focus-ring);
}

.help-tooltip__bubble {
  position: absolute;
  z-index: 70;
  bottom: calc(100% + var(--space-2));
  left: 50%;
  width: max-content;
  max-width: min(22rem, calc(100vw - 2rem));
  transform: translateX(-50%) translateY(0.25rem);
  border: 1px solid var(--color-border-strong);
  border-radius: var(--radius-md);
  background: var(--surface-panel-raised);
  box-shadow: var(--shadow-panel);
  color: var(--color-text);
  font-size: 0.78rem;
  font-weight: 650;
  line-height: 1.45;
  opacity: 0;
  padding: var(--space-2) var(--space-3);
  pointer-events: none;
  text-align: left;
  transition: opacity 140ms ease, transform 140ms ease;
  white-space: normal;
}

.help-tooltip:hover .help-tooltip__bubble,
.help-tooltip:focus-within .help-tooltip__bubble {
  opacity: 1;
  transform: translateX(-50%) translateY(0);
}
</style>
