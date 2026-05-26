<script setup lang="ts">
export type SectionSelectorItem = {
  key: string;
  label: string;
};

withDefaults(defineProps<{
  items: SectionSelectorItem[];
  modelValue: string;
  ariaLabel?: string;
  label?: string;
}>(), {
  ariaLabel: 'Разделы',
  label: 'Выберите раздел'
});

const emit = defineEmits<{
  'update:modelValue': [value: string];
}>();
</script>

<template>
  <nav class="section-selector app-surface" :aria-label="ariaLabel">
    <div v-if="label" class="section-selector__label">{{ label }}</div>
    <button
      v-for="item in items"
      :key="item.key"
      type="button"
      class="section-selector__item"
      :class="{ 'section-selector__item--active': modelValue === item.key }"
      @click="emit('update:modelValue', item.key)"
    >
      {{ item.label }}
    </button>
  </nav>
</template>

<style scoped>
.section-selector {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--space-3);
  padding: var(--space-3);
  border-color: rgb(249 115 22 / 0.34);
  background:
    linear-gradient(180deg, rgb(249 115 22 / 0.08), transparent),
    var(--color-surface);
  box-shadow: inset 0 1px 0 rgb(255 255 255 / 0.035), 0 12px 34px rgb(0 0 0 / 0.18);
}

.section-selector__label {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
  color: var(--accent-ember-text-strong);
  font-size: 0.78rem;
  font-weight: 820;
  text-transform: uppercase;
}

.section-selector__label::before {
  display: inline-block;
  width: 0.55rem;
  height: 0.55rem;
  border-radius: 999px;
  background: var(--accent-ember);
  box-shadow: 0 0 14px rgb(249 115 22 / 0.52);
  content: '';
}

.section-selector__item {
  min-height: 2.35rem;
  border: 1px solid rgb(249 115 22 / 0.14);
  border-radius: var(--radius-md);
  background: var(--surface-control);
  color: var(--color-text);
  padding: 0 var(--space-4);
  font-size: 0.8125rem;
  font-weight: 820;
}

.section-selector__item:hover,
.section-selector__item:focus-visible,
.section-selector__item--active {
  border-color: var(--accent-primary-border);
  background: var(--button-primary-bg);
  color: var(--text-on-fire);
}

.section-selector__item:focus-visible {
  outline: none;
  box-shadow: 0 0 0 2px rgb(249 115 22 / 0.24);
}
</style>
