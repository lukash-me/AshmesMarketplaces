<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { ChevronsUpDown, Search, X } from 'lucide-vue-next';

const props = withDefaults(
  defineProps<{
    label: string;
    modelValue: string;
    options: string[];
    placeholder: string;
    searchPlaceholder: string;
    clearable?: boolean;
  }>(),
  {
    clearable: true
  }
);

const emit = defineEmits<{
  'update:modelValue': [value: string];
}>();

const root = ref<HTMLElement | null>(null);
const searchInput = ref<HTMLInputElement | null>(null);
const open = ref(false);
const search = ref('');
let closeTimer: ReturnType<typeof setTimeout> | null = null;

watch(
  () => props.modelValue,
  (value) => {
    if (!open.value) {
      search.value = value;
    }
  },
  { immediate: true }
);

const filteredOptions = computed(() => {
  const query = search.value.trim().toLocaleLowerCase('ru-RU');
  return props.options
    .filter((option) => !query || option.toLocaleLowerCase('ru-RU').includes(query))
    .slice(0, 50);
});

onMounted(() => {
  document.addEventListener('pointerdown', onDocumentPointerDown);
  document.addEventListener('keydown', onDocumentKeydown);
});

onBeforeUnmount(() => {
  document.removeEventListener('pointerdown', onDocumentPointerDown);
  document.removeEventListener('keydown', onDocumentKeydown);
  cancelClose();
});

async function openMenu() {
  cancelClose();
  search.value = '';
  open.value = true;
  await nextTick();
  searchInput.value?.focus();
}

function closeMenu() {
  cancelClose();
  open.value = false;
  search.value = props.modelValue;
}

function toggleMenu() {
  if (open.value) {
    closeMenu();
    return;
  }

  void openMenu();
}

function select(value: string) {
  emit('update:modelValue', value);
  search.value = value;
  closeMenu();
}

function clear() {
  emit('update:modelValue', '');
  search.value = '';
  closeMenu();
}

function scheduleClose() {
  cancelClose();
  closeTimer = setTimeout(() => {
    closeMenu();
  }, 200);
}

function cancelClose() {
  if (closeTimer) {
    clearTimeout(closeTimer);
    closeTimer = null;
  }
}

function onFocusOut(event: FocusEvent) {
  const nextTarget = event.relatedTarget as Node | null;
  if (!root.value?.contains(nextTarget)) {
    scheduleClose();
  }
}

function onDocumentPointerDown(event: PointerEvent) {
  if (!root.value?.contains(event.target as Node)) {
    closeMenu();
  }
}

function onDocumentKeydown(event: KeyboardEvent) {
  if (open.value && event.key === 'Escape') {
    event.preventDefault();
    event.stopPropagation();
    closeMenu();
  }
}
</script>

<template>
  <div
    ref="root"
    class="filter-select"
    :class="{ 'filter-select--open': open }"
    @pointerenter="cancelClose"
    @pointerleave="scheduleClose"
    @focusin="cancelClose"
    @focusout="onFocusOut"
  >
    <div class="filter-select__control">
      <button
        class="filter-select__trigger"
        type="button"
        :aria-expanded="open"
        aria-haspopup="listbox"
        @click="toggleMenu"
      >
        <span class="filter-select__label">{{ label }}</span>
        <strong>{{ modelValue || placeholder }}</strong>
        <ChevronsUpDown class="filter-select__chevrons" :size="14" />
      </button>
      <button
        v-if="clearable && modelValue"
        class="filter-select__clear"
        type="button"
        :aria-label="`Сбросить ${label}`"
        @click="clear"
      >
        <X :size="13" />
      </button>
    </div>

    <section v-if="open" class="filter-select__menu">
      <label class="filter-select__search">
        <Search :size="15" />
        <input ref="searchInput" v-model="search" type="search" :placeholder="searchPlaceholder" />
      </label>
      <button
        v-if="clearable && modelValue"
        class="filter-select__option filter-select__option--clear"
        type="button"
        @click="select('')"
      >
        Все значения
      </button>
      <div v-if="filteredOptions.length" class="filter-select__options" role="listbox">
        <button
          v-for="option in filteredOptions"
          :key="option"
          class="filter-select__option"
          :class="{ 'filter-select__option--active': option === modelValue }"
          type="button"
          role="option"
          :aria-selected="option === modelValue"
          @click="select(option)"
        >
          {{ option }}
        </button>
      </div>
      <p v-else>В текущей выборке совпадений нет.</p>
    </section>
  </div>
</template>

<style scoped>
.filter-select {
  position: relative;
  min-width: 0;
}

.filter-select--open {
  z-index: 30;
}

.filter-select__control {
  position: relative;
}

.filter-select__trigger {
  display: grid;
  width: 100%;
  min-height: 3.25rem;
  grid-template-columns: minmax(0, 1fr) auto;
  align-items: center;
  column-gap: var(--space-2);
  border: 1px solid var(--select-control-border);
  border-radius: var(--radius-md);
  background: var(--select-control-bg);
  color: var(--select-control-text);
  cursor: pointer;
  padding: 0.45rem var(--space-3);
  text-align: left;
}

.filter-select__trigger:hover,
.filter-select--open .filter-select__trigger {
  border-color: var(--select-control-border-hover);
  background: var(--select-control-bg-hover);
}

.filter-select__trigger:focus-visible {
  outline: none;
  border-color: var(--select-control-border-focus);
  background: var(--select-control-bg-focus);
  box-shadow: var(--focus-ring);
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
  color: var(--select-label-text);
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

.filter-select__chevrons {
  grid-row: 1 / span 2;
  grid-column: 2;
  color: var(--color-text-muted);
}

.filter-select__clear {
  position: absolute;
  top: 50%;
  right: 2.35rem;
  display: grid;
  height: 1.45rem;
  width: 1.45rem;
  place-items: center;
  border: 0;
  border-radius: 999px;
  background: var(--select-control-bg);
  color: var(--color-text-muted);
  transform: translateY(-50%);
}

.filter-select__clear:hover {
  color: var(--color-text);
}

.filter-select__menu {
  position: absolute;
  z-index: 40;
  top: calc(100% + var(--space-2));
  left: 0;
  display: grid;
  width: min(19rem, calc(100vw - 2rem));
  gap: var(--space-2);
  border: 1px solid var(--select-menu-border);
  border-radius: var(--radius-md);
  background: var(--select-menu-bg);
  box-shadow: var(--select-menu-shadow);
  backdrop-filter: blur(18px);
  padding: var(--space-2);
}

.filter-select__menu::before {
  position: absolute;
  inset: 0 0 auto;
  height: 1px;
  background: linear-gradient(90deg, transparent, var(--accent-primary-border), transparent);
  content: '';
  pointer-events: none;
}

.filter-select__search {
  display: grid;
  min-height: 2.125rem;
  grid-template-columns: auto minmax(0, 1fr);
  align-items: center;
  gap: var(--space-2);
  border: 1px solid var(--select-control-border);
  border-radius: var(--radius-sm);
  background: var(--select-search-bg);
  color: var(--color-text-muted);
  padding: 0 var(--space-2);
}

.filter-select__search:focus-within {
  border-color: var(--select-control-border-focus);
  box-shadow: var(--focus-ring);
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
  padding-right: 0.15rem;
}

.filter-select__option {
  border: 0;
  border-radius: var(--radius-sm);
  background: transparent;
  color: var(--select-option-text);
  padding: 0.45rem var(--space-2);
  text-align: left;
}

.filter-select__option:hover,
.filter-select__option--active {
  background: var(--select-option-hover-bg);
}

.filter-select__option--active {
  background: var(--select-option-active-bg);
  color: var(--select-option-active-text);
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
