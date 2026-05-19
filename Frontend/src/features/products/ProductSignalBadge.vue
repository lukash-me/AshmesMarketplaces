<script setup lang="ts">
import Badge from '@/shared/ui/Badge.vue';

import {
  getProductHeatLabel,
  getProductHeatTier,
  getProductHeatTone,
  getProductSignalTitle
} from './productSignals';
import type { ProductListItem } from './products.types';

const props = defineProps<{
  product: Pick<ProductListItem, 'status' | 'dateUpdated'>;
  compact?: boolean;
}>();
</script>

<template>
  <span
    class="product-signal"
    :class="[`product-signal--${getProductHeatTier(props.product)}`, compact ? 'product-signal--compact' : '']"
    :title="getProductSignalTitle(props.product)"
  >
    <span class="product-signal__bar" aria-hidden="true">
      <span />
    </span>
    <Badge :tone="getProductHeatTone(props.product)">
      {{ getProductHeatLabel(props.product) }}
    </Badge>
  </span>
</template>

<style scoped>
.product-signal {
  display: inline-flex;
  min-width: 6.5rem;
  align-items: center;
  gap: var(--space-2);
}

.product-signal--compact {
  min-width: 0;
}

.product-signal__bar {
  display: inline-flex;
  height: 1.25rem;
  width: 0.25rem;
  overflow: hidden;
  border-radius: 999px;
  background: var(--heat-dormant-soft);
}

.product-signal__bar span {
  display: block;
  width: 100%;
  align-self: end;
  border-radius: inherit;
  background: var(--heat-dormant);
}

.product-signal--dormant .product-signal__bar span {
  height: 28%;
  background: var(--heat-dormant);
}

.product-signal--warm .product-signal__bar span {
  height: 52%;
  background: var(--heat-warm);
}

.product-signal--rising .product-signal__bar span {
  height: 76%;
  background: var(--heat-rising);
}

.product-signal--hot .product-signal__bar span {
  height: 100%;
  background: var(--heat-hot);
}
</style>
