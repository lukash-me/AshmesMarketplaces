<script setup lang="ts">
defineProps<{
  text: string;
}>();
</script>

<template>
  <span class="help-tooltip">
    <button class="help-tooltip__trigger" type="button" :aria-label="text">
      <span aria-hidden="true">?</span>
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
  display: inline-grid;
  width: 1.05rem;
  height: 1.05rem;
  align-items: center;
  justify-content: center;
  border: 1px solid var(--accent-ember-border);
  border-radius: 999px;
  background: var(--surface-control-raised);
  color: var(--accent-ember-text-strong);
  cursor: help;
  font-size: 0.72rem;
  font-weight: 860;
  line-height: 1;
  padding: 0;
  transition: border-color 140ms ease, background-color 140ms ease, color 140ms ease;
}

.help-tooltip__trigger:hover,
.help-tooltip__trigger:focus-visible {
  border-color: var(--accent-primary-hover-border);
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
  top: calc(100% + 0.45rem);
  left: calc(100% + 0.35rem);
  width: max-content;
  max-width: min(34rem, calc(100vw - 2rem));
  transform: translate(-0.15rem, -0.15rem);
  transform-origin: top left;
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
  white-space: pre-line;
}

.help-tooltip:hover .help-tooltip__bubble,
.help-tooltip:focus-within .help-tooltip__bubble {
  opacity: 1;
  transform: translate(0, 0);
}
</style>
