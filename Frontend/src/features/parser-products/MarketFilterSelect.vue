<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { ChevronsUpDown, Search, X } from 'lucide-vue-next';

const props = defineProps<{
  label: string;
  modelValue: string;
  options: string[];
  placeholder: string;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: string];
}>();

const search = ref('');

watch(
  () => props.modelValue,
  (value) => {
    search.value = value;
  },
  { immediate: true }
);

const filteredOptions = computed(() => {
  const query = search.value.trim().toLocaleLowerCase('ru-RU');
  return props.options
    .filter((option) => !query || option.toLocaleLowerCase('ru-RU').includes(query))
    .slice(0, 30);
});

function select(value: string, event: MouseEvent) {
  emit('update:modelValue', value);
  search.value = value;
  (event.currentTarget as HTMLElement).closest('details')?.removeAttribute('open');
}

function clear(event: MouseEvent) {
  emit('update:modelValue', '');
  search.value = '';
  event.preventDefault();
}
</script>

<template>
  <details class="filter-select">
    <summary>
      <span class="filter-select__label">{{ label }}</span>
      <strong>{{ modelValue || placeholder }}</strong>
      <button
        v-if="modelValue"
        class="filter-select__clear"
        type="button"
        :aria-label="`Сбросить ${label}`"
        @click="clear"
      >
        <X :size="13" />
      </button>
      <ChevronsUpDown class="filter-select__chevrons" :size="14" />
    </summary>
    <section class="filter-select__menu">
      <label class="filter-select__search">
        <Search :size="15" />
        <input v-model="search" type="search" :placeholder="`Найти ${label.toLocaleLowerCase('ru-RU')}`" />
      </label>
      <button
        v-if="modelValue"
        class="filter-select__option filter-select__option--clear"
        type="button"
        @click="select('', $event)"
      >
        Все значения
      </button>
      <div v-if="filteredOptions.length" class="filter-select__options">
        <button
          v-for="option in filteredOptions"
          :key="option"
          class="filter-select__option"
          :class="{ 'filter-select__option--active': option === modelValue }"
          type="button"
          @click="select(option, $event)"
        >
          {{ option }}
        </button>
      </div>
      <p v-else>В текущей выборке совпадений нет.</p>
    </section>
  </details>
</template>

<style scoped>
.filter-select {
  position: relative;
  min-width: 0;
}

.filter-select summary {
  display: grid;
  min-height: 3.25rem;
  grid-template-columns: minmax(0, 1fr) auto auto;
  align-items: center;
  column-gap: var(--space-2);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--surface-control);
  cursor: pointer;
  list-style: none;
  padding: 0.45rem var(--space-3);
}

.filter-select summary::-webkit-details-marker {
  display: none;
}

.filter-select__label,
.filter-select strong {
  grid-column: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.filter-select__label {
  align-self: end;
  color: var(--color-text-muted);
  font-size: 0.68rem;
  font-weight: 700;
  text-transform: uppercase;
}

.filter-select strong {
  align-self: start;
  color: var(--color-text);
  font-size: 0.8125rem;
  font-weight: 650;
}

.filter-select__clear,
.filter-select__chevrons {
  grid-row: 1 / span 2;
  grid-column: 2;
}

.filter-select__chevrons {
  grid-column: 3;
  color: var(--color-text-muted);
}

.filter-select__clear {
  display: grid;
  height: 1.45rem;
  width: 1.45rem;
  place-items: center;
  border: 0;
  border-radius: 999px;
  background: var(--surface-control-raised);
  color: var(--color-text-muted);
}

.filter-select__menu {
  position: absolute;
  z-index: 5;
  top: calc(100% + var(--space-2));
  left: 0;
  display: grid;
  width: min(18rem, calc(100vw - 2rem));
  gap: var(--space-2);
  border: 1px solid var(--color-border-strong);
  border-radius: var(--radius-md);
  background: var(--background-panel-highlight);
  box-shadow: var(--shadow-panel);
  padding: var(--space-2);
}

.filter-select__search {
  display: grid;
  min-height: 2.125rem;
  grid-template-columns: auto minmax(0, 1fr);
  align-items: center;
  gap: var(--space-2);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  background: var(--surface-control);
  color: var(--color-text-muted);
  padding: 0 var(--space-2);
}

.filter-select__search input {
  min-width: 0;
  border: 0;
  background: transparent;
  color: var(--color-text);
  outline: 0;
}

.filter-select__options {
  display: grid;
  max-height: 13rem;
  gap: 0.15rem;
  overflow-y: auto;
}

.filter-select__option {
  border: 0;
  border-radius: var(--radius-sm);
  background: transparent;
  color: var(--color-text);
  padding: 0.45rem var(--space-2);
  text-align: left;
}

.filter-select__option:hover,
.filter-select__option--active {
  background: var(--color-surface-hover);
}

.filter-select__option--clear {
  color: var(--color-text-muted);
}

.filter-select p {
  margin: 0;
  color: var(--color-text-muted);
  font-size: 0.8125rem;
}
</style>
