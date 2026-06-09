<script setup lang="ts">
import HelpTooltip from '@/shared/ui/HelpTooltip.vue';

defineProps<{
  title: string;
  description?: string;
  subtitle?: string;
}>();
</script>

<template>
  <div class="page-header">
    <div>
      <h1>
        <span>{{ title }}</span>
        <HelpTooltip v-if="description || subtitle" :text="description || subtitle || ''" />
      </h1>
    </div>
    <div class="page-header__actions">
      <slot />
    </div>
  </div>
</template>

<style scoped>
.page-header {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-end;
  justify-content: space-between;
  gap: var(--space-3);
  margin-bottom: var(--space-4);
}

h1 {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
  margin: 0;
  font-size: 1.375rem;
  font-weight: 750;
  letter-spacing: 0;
}

.page-header__actions {
  display: flex;
  align-items: center;
  gap: var(--space-2);
}
</style>
