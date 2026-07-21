<script setup lang="ts">
defineProps<{
  id?: string;
  modelValue: string | number | null;
  label?: string;
  type?: string;
  placeholder?: string;
  error?: string;
  autocomplete?: string;
}>();

defineEmits<{
  'update:modelValue': [value: string];
}>();
</script>

<template>
  <label class="field" :for="id">
    <span v-if="label" class="field__label">{{ label }}</span>
    <input
      :id="id"
      class="field__control"
      :type="type ?? 'text'"
      :value="modelValue ?? ''"
      :placeholder="placeholder"
      :autocomplete="autocomplete"
      @input="$emit('update:modelValue', ($event.target as HTMLInputElement).value)"
    />
    <span v-if="error" class="field__error">{{ error }}</span>
  </label>
</template>

<style scoped>
.field {
  display: grid;
  gap: var(--space-1);
}

.field__label {
  color: var(--color-text-muted);
  font-size: 0.72rem;
  font-weight: 680;
  letter-spacing: 0.02em;
  text-transform: uppercase;
}

.field__control {
  height: 2.25rem;
  width: 100%;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--surface-control);
  color: var(--color-text);
  padding: 0 var(--space-3);
  outline: none;
  transition: background-color 140ms ease, border-color 140ms ease, box-shadow 140ms ease;
}

.field__control::placeholder {
  color: var(--color-text-subtle);
}

.field__control:focus {
  border-color: var(--color-primary);
  background: var(--surface-control-focus);
  box-shadow: var(--focus-ring);
}

.field__error {
  color: var(--color-danger);
  font-size: 0.8125rem;
}
</style>
