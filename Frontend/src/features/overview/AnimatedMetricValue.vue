<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue';

const props = withDefaults(defineProps<{
  value: number | null | undefined;
  suffix?: string;
  fractionDigits?: number;
  fallback?: string;
  durationMs?: number;
}>(), {
  suffix: '',
  fractionDigits: 0,
  fallback: 'Нет данных',
  durationMs: 750
});

const displayedValue = ref<number | null>(numericOrNull(props.value));
const hasMounted = ref(false);
const prefersReducedMotion = ref(false);

let frameId: number | null = null;
let reduceMotionQuery: MediaQueryList | null = null;

if (typeof window !== 'undefined' && typeof window.matchMedia === 'function') {
  reduceMotionQuery = window.matchMedia('(prefers-reduced-motion: reduce)');
  prefersReducedMotion.value = reduceMotionQuery.matches;
  reduceMotionQuery.addEventListener?.('change', handleReducedMotionChange);
}

const formattedValue = computed(() => {
  if (displayedValue.value === null) {
    return props.fallback;
  }

  const formatted = new Intl.NumberFormat('ru-RU', {
    minimumFractionDigits: props.fractionDigits,
    maximumFractionDigits: props.fractionDigits
  }).format(displayedValue.value);

  return `${formatted}${props.suffix}`;
});

watch(
  () => props.value,
  (value) => {
    const nextValue = numericOrNull(value);
    const currentValue = displayedValue.value;

    if (!hasMounted.value || prefersReducedMotion.value || currentValue === null || nextValue === null) {
      cancelAnimation();
      displayedValue.value = nextValue;
      hasMounted.value = true;
      return;
    }

    animateValue(currentValue, nextValue);
    hasMounted.value = true;
  },
  { immediate: true }
);

onBeforeUnmount(() => {
  cancelAnimation();
  reduceMotionQuery?.removeEventListener?.('change', handleReducedMotionChange);
});

function animateValue(from: number, to: number): void {
  cancelAnimation();

  if (from === to) {
    displayedValue.value = to;
    return;
  }

  const startedAt = performance.now();
  const duration = Math.max(1, props.durationMs);

  const step = (timestamp: number): void => {
    const progress = Math.min((timestamp - startedAt) / duration, 1);
    const eased = 1 - Math.pow(1 - progress, 3);

    displayedValue.value = from + (to - from) * eased;

    if (progress < 1) {
      frameId = requestAnimationFrame(step);
      return;
    }

    frameId = null;
    displayedValue.value = to;
  };

  frameId = requestAnimationFrame(step);
}

function cancelAnimation(): void {
  if (frameId !== null) {
    cancelAnimationFrame(frameId);
    frameId = null;
  }
}

function handleReducedMotionChange(event: MediaQueryListEvent): void {
  prefersReducedMotion.value = event.matches;
  if (event.matches) {
    cancelAnimation();
    displayedValue.value = numericOrNull(props.value);
  }
}

function numericOrNull(value: number | null | undefined): number | null {
  return typeof value === 'number' && Number.isFinite(value) ? value : null;
}
</script>

<template>
  <strong>{{ formattedValue }}</strong>
</template>
