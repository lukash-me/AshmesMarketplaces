<script setup lang="ts">
import { computed, ref, watch } from 'vue';

const props = withDefaults(
  defineProps<{
    src?: string | null;
    fallbackSrc?: string | null;
    alt: string;
    priority?: boolean;
  }>(),
  {
    src: null,
    fallbackSrc: null,
    priority: false
  }
);

const activeSrc = ref<string | null>(null);
const loaded = ref(false);
const failed = ref(false);

watch(
  () => [props.src, props.fallbackSrc] as const,
  ([src, fallbackSrc]) => {
    activeSrc.value = src || fallbackSrc || null;
    loaded.value = false;
    failed.value = !activeSrc.value;
  },
  { immediate: true }
);

const loadingMode = computed(() => (props.priority ? 'eager' : 'lazy'));
const fetchPriority = computed(() => (props.priority ? 'high' : 'auto'));

function onLoad() {
  loaded.value = true;
  failed.value = false;
}

function onError() {
  if (props.fallbackSrc && activeSrc.value !== props.fallbackSrc) {
    activeSrc.value = props.fallbackSrc;
    loaded.value = false;
    return;
  }

  failed.value = true;
}
</script>

<template>
  <span class="market-image" :class="{ 'market-image--ready': loaded, 'market-image--failed': failed }">
    <img
      v-if="fallbackSrc && activeSrc !== fallbackSrc && !loaded"
      class="market-image__preview"
      :src="fallbackSrc"
      alt=""
      loading="eager"
      decoding="async"
    />
    <span v-if="activeSrc && !loaded && !failed" class="market-image__skeleton" aria-hidden="true" />
    <img
      v-if="activeSrc && !failed"
      class="market-image__asset"
      :src="activeSrc"
      :alt="alt"
      :loading="loadingMode"
      :fetchpriority="fetchPriority"
      decoding="async"
      @load="onLoad"
      @error="onError"
    />
    <span v-if="failed" class="market-image__fallback">Нет фото</span>
  </span>
</template>

<style scoped>
.market-image {
  position: relative;
  display: grid;
  min-width: 0;
  height: 100%;
  width: 100%;
  place-items: center;
  overflow: hidden;
  background: var(--surface-control);
  color: var(--color-text-muted);
}

.market-image__skeleton {
  position: absolute;
  inset: 0;
  z-index: 1;
  background: var(--background-skeleton);
  background-size: 220% 100%;
  opacity: 0.82;
  animation: image-shimmer 1.2s ease-in-out infinite;
}

.market-image__preview,
.market-image__asset {
  display: block;
  height: 100%;
  width: 100%;
  object-fit: cover;
  opacity: 0;
  transition: opacity 180ms ease;
}

.market-image__preview {
  position: absolute;
  inset: 0;
  opacity: 0.72;
}

.market-image--ready .market-image__asset {
  opacity: 1;
}

.market-image__fallback {
  display: grid;
  height: 100%;
  width: 100%;
  place-items: center;
  padding: var(--space-2);
  color: var(--color-text-subtle);
  font-size: 0.72rem;
  text-align: center;
}

@keyframes image-shimmer {
  0% {
    background-position: 100% 0;
  }

  100% {
    background-position: -100% 0;
  }
}
</style>
