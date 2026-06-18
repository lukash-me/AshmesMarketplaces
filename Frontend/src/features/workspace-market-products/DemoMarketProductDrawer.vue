<script setup lang="ts">
import { X } from 'lucide-vue-next';

import Button from '@/shared/ui/Button.vue';

interface DemoMarketProductDrawerSignal {
  code: string;
  title: string;
  description: string;
  metricFacts: string[];
}

interface DemoMarketProductDrawerProduct {
  name: string;
  sourceCategory: string | null;
  sourceSubcategory: string | null;
  currentPrice?: number | null;
  priceWbWallet?: number | null;
  priceDiscounted?: number | null;
  priceRegular?: number | null;
  costPrice: number | null;
  description: string | null;
  characteristicsJson?: string | null;
  supplierName: string | null;
  supplierUrl: string | null;
  thumbnailUrl: string | null;
  media?: Array<{ id: string; url: string; fileName: string }>;
  signals?: DemoMarketProductDrawerSignal[];
}

const props = defineProps<{
  open: boolean;
  product: DemoMarketProductDrawerProduct | null;
}>();

const emit = defineEmits<{
  close: [];
}>();

function formatMoney(value: number | null | undefined): string {
  return value === null || value === undefined
    ? 'Нет данных'
    : `${new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 2 }).format(value)} ₽`;
}

function price(): number | null {
  const product = props.product;
  return product?.currentPrice ?? product?.priceWbWallet ?? product?.priceDiscounted ?? product?.priceRegular ?? null;
}

function parsedCharacteristics(): Array<{ key: string; value: string }> {
  const raw = props.product?.characteristicsJson;
  if (!raw) {
    return [];
  }

  try {
    const parsed = JSON.parse(raw) as unknown;
    if (Array.isArray(parsed)) {
      return parsed
        .map((item) => {
          if (!item || typeof item !== 'object') {
            return null;
          }
          const record = item as Record<string, unknown>;
          const key = String(record.name ?? record.key ?? record.title ?? '').trim();
          const value = String(record.value ?? '').trim();
          return key && value ? { key, value } : null;
        })
        .filter((item): item is { key: string; value: string } => Boolean(item));
    }

    if (parsed && typeof parsed === 'object') {
      return Object.entries(parsed as Record<string, unknown>)
        .map(([key, value]) => ({ key, value: String(value) }))
        .filter((item) => item.key.trim() && item.value.trim());
    }
  } catch {
    return [];
  }

  return [];
}
</script>

<template>
  <Teleport to="body">
    <div v-if="open && product" class="demo-drawer" role="presentation">
      <button class="demo-drawer__backdrop" type="button" aria-label="Закрыть карточку" @click="emit('close')" />
      <aside class="demo-drawer__panel app-surface" role="dialog" aria-modal="true">
        <header class="demo-drawer__header">
          <div>
            <span>Демо карточка</span>
            <h2>{{ product.name }}</h2>
            <p>{{ product.sourceSubcategory || product.sourceCategory || 'Ниша не указана' }}</p>
          </div>
          <Button variant="ghost" @click="emit('close')">
            <X :size="16" />
          </Button>
        </header>

        <section class="demo-drawer__metrics">
          <div class="app-operator-metric">
            <span>Цена</span>
            <strong>{{ formatMoney(price()) }}</strong>
          </div>
          <div class="app-operator-metric">
            <span>Себестоимость</span>
            <strong>{{ formatMoney(product.costPrice) }}</strong>
          </div>
          <div class="app-operator-metric">
            <span>Поставщик</span>
            <strong>{{ product.supplierName || 'Не указан' }}</strong>
          </div>
        </section>

        <section v-if="product.media?.length" class="demo-drawer__media">
          <img v-for="item in product.media" :key="item.id" :src="item.url" :alt="item.fileName" />
        </section>

        <section class="demo-drawer__section">
          <h3>Описание</h3>
          <p>{{ product.description || 'Описание не добавлено.' }}</p>
        </section>

        <section class="demo-drawer__section">
          <h3>Характеристики</h3>
          <dl v-if="parsedCharacteristics().length" class="demo-drawer__chars">
            <template v-for="item in parsedCharacteristics()" :key="item.key">
              <dt>{{ item.key }}</dt>
              <dd>{{ item.value }}</dd>
            </template>
          </dl>
          <p v-else>Характеристики не добавлены.</p>
        </section>

        <section v-if="product.supplierUrl" class="demo-drawer__section">
          <h3>Поставщик</h3>
          <a class="app-operator-link" :href="product.supplierUrl" target="_blank" rel="noreferrer">Открыть ссылку</a>
        </section>

        <section v-if="product.signals?.length" class="demo-drawer__section">
          <h3>Рекомендации ИИ</h3>
          <article v-for="signal in product.signals" :key="signal.code" class="demo-signal">
            <strong>{{ signal.title }}</strong>
            <p>{{ signal.description }}</p>
            <ul>
              <li v-for="fact in signal.metricFacts" :key="fact">{{ fact }}</li>
            </ul>
          </article>
        </section>
      </aside>
    </div>
  </Teleport>
</template>

<style scoped>
.demo-drawer {
  position: fixed;
  z-index: 90;
  inset: 0;
}

.demo-drawer__backdrop {
  position: absolute;
  inset: 0;
  border: 0;
  background: rgb(2 6 23 / 0.58);
}

.demo-drawer__panel {
  position: absolute;
  top: 0;
  right: 0;
  display: grid;
  gap: var(--space-4);
  width: min(46rem, 100%);
  height: 100%;
  overflow: auto;
  border-left: 1px solid var(--operator-border-muted);
  padding: var(--space-4);
}

.demo-drawer__header {
  display: flex;
  justify-content: space-between;
  gap: var(--space-3);
}

.demo-drawer__header h2,
.demo-drawer__header p,
.demo-drawer__section h3,
.demo-drawer__section p,
.demo-signal p {
  margin: 0;
}

.demo-drawer__header span,
.demo-drawer__header p,
.demo-drawer__section p {
  color: var(--color-text-muted);
}

.demo-drawer__metrics {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: var(--space-2);
}

.demo-drawer__media {
  display: flex;
  gap: var(--space-2);
  overflow-x: auto;
}

.demo-drawer__media img {
  width: 7rem;
  aspect-ratio: 3 / 4;
  object-fit: cover;
  border: 1px solid var(--operator-border-muted);
  border-radius: var(--radius-md);
}

.demo-drawer__section,
.demo-signal {
  display: grid;
  gap: var(--space-2);
  border: 1px solid var(--operator-border-muted);
  border-radius: var(--radius-md);
  background: var(--operator-card-bg);
  padding: var(--space-3);
}

.demo-drawer__chars {
  display: grid;
  grid-template-columns: minmax(8rem, 0.35fr) minmax(0, 1fr);
  gap: var(--space-2);
  margin: 0;
}

.demo-drawer__chars dt {
  color: var(--color-text-muted);
  font-weight: 760;
}

.demo-drawer__chars dd {
  margin: 0;
}

.demo-signal ul {
  display: grid;
  gap: var(--space-1);
  margin: 0;
  padding-left: 1.1rem;
}

@media (max-width: 720px) {
  .demo-drawer__metrics,
  .demo-drawer__chars {
    grid-template-columns: 1fr;
  }
}
</style>
