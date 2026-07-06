<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { RefreshCw, Search } from 'lucide-vue-next';

import { getRuleConstructorFilters } from '@/features/rule-constructor/ruleConstructor.api';
import type { RuleConstructorFilter } from '@/features/rule-constructor/ruleConstructor.types';
import { getProblemMessage } from '@/shared/api/problemDetails';
import EmptyState from '@/shared/ui/EmptyState.vue';
import LoadingState from '@/shared/ui/LoadingState.vue';
import PageHeader from '@/widgets/PageHeader.vue';
import { getWildberriesCategoryTree, type WildberriesCategoryCatalog, type WildberriesCategoryNode } from './marketplaceCategories.api';

type ReferenceMode = 'rule-constructor' | 'wb-catalog';

type FilterGroup = {
  name: string;
  filters: RuleConstructorFilter[];
};

const activeMode = ref<ReferenceMode>('rule-constructor');
const filters = ref<RuleConstructorFilter[]>([]);
const wbCatalog = ref<WildberriesCategoryCatalog | null>(null);
const loading = ref(false);
const error = ref('');
const wbSearch = ref('');

const groupedFilters = computed<FilterGroup[]>(() => {
  const groups = new Map<string, RuleConstructorFilter[]>();

  for (const filter of filters.value) {
    const list = groups.get(filter.group) ?? [];
    list.push(filter);
    groups.set(filter.group, list);
  }

  return Array.from(groups.entries()).map(([name, groupFilters]) => ({
    name,
    filters: groupFilters
  }));
});

const wbLeafNodes = computed(() => (wbCatalog.value?.nodes ?? []).filter((node) => node.isLeaf));

const filteredWbNodes = computed(() => {
  const query = wbSearch.value.trim().toLocaleLowerCase('ru-RU');
  const leaves = wbLeafNodes.value;

  if (!query) {
    return leaves;
  }

  return leaves.filter((node) =>
    [
      node.name,
      node.path,
      node.sourceCategory,
      node.sourceSubcategory,
      node.searchQuery ?? '',
      String(node.id)
    ].some((value) => value.toLocaleLowerCase('ru-RU').includes(query))
  );
});

const wbFetchedAtText = computed(() => {
  const value = wbCatalog.value?.fetchedAtUtc;

  if (!value) {
    return '-';
  }

  return new Intl.DateTimeFormat('ru-RU', {
    day: '2-digit',
    month: '2-digit',
    year: '2-digit',
    hour: '2-digit',
    minute: '2-digit'
  }).format(new Date(value));
});

onMounted(loadActiveReference);

async function setMode(mode: ReferenceMode) {
  activeMode.value = mode;
  await loadActiveReference();
}

async function loadActiveReference() {
  if (activeMode.value === 'rule-constructor') {
    await loadFilters();
    return;
  }

  await loadWbCatalog();
}

async function loadFilters() {
  loading.value = true;
  error.value = '';

  try {
    filters.value = await getRuleConstructorFilters();
  } catch (err) {
    error.value = getProblemMessage(err, 'Не удалось загрузить справочник конструктора правил.');
  } finally {
    loading.value = false;
  }
}

async function loadWbCatalog() {
  loading.value = true;
  error.value = '';

  try {
    wbCatalog.value = await getWildberriesCategoryTree();
  } catch (err) {
    error.value = getProblemMessage(err, 'Не удалось загрузить каталог WB.');
  } finally {
    loading.value = false;
  }
}

function statusLabel(status: RuleConstructorFilter['status']): string {
  switch (status) {
    case 'active':
      return 'Активен';
    case 'experimental':
      return 'Эксперимент';
    case 'disabled':
      return 'Недоступен';
  }
}

function toneLabel(tone: RuleConstructorFilter['tone']): string {
  if (tone === 'positive') {
    return 'Положительное';
  }

  if (tone === 'neutral') {
    return 'Нейтральное';
  }

  return 'Негативное';
}

function verificationStatusLabel(status: RuleConstructorFilter['verificationStatus']): string {
  switch (status) {
    case 'not_ready':
      return 'Не готово';
    case 'needs_data_export':
      return 'Нужна выгрузка';
    case 'ready':
      return 'Готово';
  }
}

function sourcesText(filter: RuleConstructorFilter): string {
  return filter.dataSources.length > 0 ? filter.dataSources.join(', ') : 'Источник не указан';
}

function parentText(node: WildberriesCategoryNode): string {
  return node.parentId === null ? '-' : String(node.parentId);
}
</script>

<template>
  <section class="admin-catalogs">
    <PageHeader
      title="Справочники"
      description="Служебные справочники и иерархии, которые использует система."
    >
      <button class="admin-catalogs__button" type="button" :disabled="loading" @click="loadActiveReference">
        <RefreshCw :size="16" />
        Обновить
      </button>
    </PageHeader>

    <div class="admin-catalogs__tabs" role="tablist" aria-label="Справочники">
      <button
        class="admin-catalogs__tab"
        :class="{ 'admin-catalogs__tab--active': activeMode === 'rule-constructor' }"
        type="button"
        role="tab"
        :aria-selected="activeMode === 'rule-constructor'"
        @click="setMode('rule-constructor')"
      >
        Конструктор правил
      </button>
      <button
        class="admin-catalogs__tab"
        :class="{ 'admin-catalogs__tab--active': activeMode === 'wb-catalog' }"
        type="button"
        role="tab"
        :aria-selected="activeMode === 'wb-catalog'"
        @click="setMode('wb-catalog')"
      >
        Каталог WB
      </button>
    </div>

    <div v-if="error" class="admin-catalogs__notice admin-catalogs__notice--error">
      {{ error }}
    </div>

    <LoadingState v-if="loading" class="admin-catalogs__surface" :rows="5" />

    <template v-else-if="activeMode === 'rule-constructor'">
      <EmptyState
        v-if="filters.length === 0"
        title="Справочник конструктора правил пуст"
        description="После добавления правил они появятся здесь."
      />

      <div v-else class="admin-catalogs__surface">
        <header class="admin-catalogs__intro">
          <div>
            <h2>Фильтры конструктора правил</h2>
            <p>
              Эти правила доступны пользователю на странице «Конструктор правил». Недоступные фильтры показываются
              для прозрачности дорожной карты, но не применяются в поиске.
            </p>
          </div>
          <strong>{{ filters.length }} фильтров</strong>
        </header>

        <section v-for="group in groupedFilters" :key="group.name" class="admin-catalogs__group">
          <h3>{{ group.name }}</h3>
          <div class="admin-catalogs__table" role="table" :aria-label="group.name">
            <div class="admin-catalogs__row admin-catalogs__row--head" role="row">
              <span role="columnheader">Фильтр</span>
              <span role="columnheader">Описание</span>
              <span role="columnheader">Источники</span>
              <span role="columnheader">Статус</span>
              <span role="columnheader">Проверка</span>
              <span role="columnheader">Окрас</span>
            </div>
            <div v-for="filter in group.filters" :key="filter.id" class="admin-catalogs__row" role="row">
              <div role="cell">
                <strong>{{ filter.name }}</strong>
                <small>{{ filter.id }}</small>
              </div>
              <div role="cell">
                <span>{{ filter.description }}</span>
                <small v-if="filter.requiresReviewByUser">Новые логические правила требуют личной проверки пользователем.</small>
                <small v-if="filter.unavailableReason">{{ filter.unavailableReason }}</small>
              </div>
              <div role="cell">
                {{ sourcesText(filter) }}
              </div>
              <div role="cell">
                <span class="admin-catalogs__status" :class="`admin-catalogs__status--${filter.status}`">
                  {{ statusLabel(filter.status) }}
                </span>
              </div>
              <div role="cell">
                <span
                  class="admin-catalogs__verification"
                  :class="`admin-catalogs__verification--${filter.verificationStatus}`"
                >
                  {{ verificationStatusLabel(filter.verificationStatus) }}
                </span>
              </div>
              <div role="cell">
                <span class="admin-catalogs__tone" :class="`admin-catalogs__tone--${filter.tone}`">
                  {{ toneLabel(filter.tone) }}
                </span>
              </div>
            </div>
          </div>
        </section>
      </div>
    </template>

    <template v-else>
      <EmptyState
        v-if="!wbCatalog || wbCatalog.nodes.length === 0"
        title="Каталог WB не загружен"
        description="Запустите расчет «Справочник ниш WB» во вкладке «Расчеты», чтобы заполнить этот справочник."
      />

      <div v-else class="admin-catalogs__surface">
        <header class="admin-catalogs__intro admin-catalogs__intro--stacked">
          <div>
            <h2>Каталог WB</h2>
            <p>
              Иерархия ниш Wildberries, полученная расчетом «Справочник ниш WB». Справочник используется для выбора
              категорий и ниш в parser-настройках и аналитике.
            </p>
          </div>
          <div class="admin-catalogs__stats">
            <span>{{ wbLeafNodes.length }} leaf-ниш</span>
            <span>Загружено: {{ wbFetchedAtText }}</span>
          </div>
        </header>

        <label class="admin-catalogs__search">
          <Search :size="16" />
          <input v-model="wbSearch" type="search" placeholder="Найти нишу, категорию, path или WB id" />
        </label>

        <div class="admin-catalogs__table admin-catalogs__table--wb" role="table" aria-label="Каталог WB">
          <div class="admin-catalogs__wb-row admin-catalogs__row--head" role="row">
            <span role="columnheader">Ниша</span>
            <span role="columnheader">Категория</span>
            <span role="columnheader">Путь</span>
            <span role="columnheader">WB id</span>
            <span role="columnheader">Parent</span>
            <span role="columnheader">Поисковый запрос</span>
          </div>
          <div v-for="node in filteredWbNodes" :key="node.id" class="admin-catalogs__wb-row" role="row">
            <div role="cell">
              <strong>{{ node.sourceSubcategory || node.name }}</strong>
              <small>level {{ node.level }}</small>
            </div>
            <div role="cell">
              {{ node.sourceCategory }}
            </div>
            <div role="cell">
              {{ node.path }}
            </div>
            <div role="cell">
              {{ node.id }}
            </div>
            <div role="cell">
              {{ parentText(node) }}
            </div>
            <div role="cell">
              {{ node.searchQuery || '-' }}
            </div>
          </div>
        </div>

        <EmptyState
          v-if="filteredWbNodes.length === 0"
          title="Ниши не найдены"
          description="Измените поисковый запрос или очистите поле поиска."
        />
      </div>
    </template>
  </section>
</template>

<style scoped>
.admin-catalogs {
  display: grid;
  gap: var(--space-4);
  width: min(82rem, 100%);
  margin: 0 auto;
}

.admin-catalogs__surface {
  display: grid;
  gap: var(--space-4);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--surface-card);
  padding: var(--space-4);
}

.admin-catalogs__tabs {
  display: inline-flex;
  width: max-content;
  gap: 0.35rem;
  border: 1px solid color-mix(in srgb, var(--color-border) 78%, white 12%);
  border-radius: var(--radius-sm);
  background:
    linear-gradient(135deg, rgb(255 255 255 / 0.24), rgb(255 255 255 / 0.06)),
    color-mix(in srgb, var(--surface-card) 82%, white 8%);
  box-shadow:
    inset 0 1px 0 rgb(255 255 255 / 0.22),
    0 10px 24px rgb(15 23 42 / 0.07);
  backdrop-filter: blur(18px) saturate(1.12);
  padding: 0.28rem;
}

button.admin-catalogs__tab {
  appearance: none;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 8.4rem;
  min-height: 2.1rem;
  border: 1px solid transparent;
  border-radius: var(--radius-sm);
  background: transparent;
  color: var(--color-text);
  padding: 0 var(--space-3);
  font: inherit;
  font-size: 0.86rem;
  font-weight: 750;
  line-height: 1;
  white-space: nowrap;
  transition:
    background-color 0.16s ease,
    border-color 0.16s ease,
    color 0.16s ease,
    box-shadow 0.16s ease;
}

button.admin-catalogs__tab:hover {
  border-color: color-mix(in srgb, var(--color-ember) 52%, transparent);
  background: color-mix(in srgb, var(--accent-ember-soft) 42%, transparent);
}

button.admin-catalogs__tab:focus-visible {
  outline: 2px solid color-mix(in srgb, var(--color-ember) 70%, white 10%);
  outline-offset: 2px;
}

button.admin-catalogs__tab--active {
  border-color: var(--color-ember);
  background:
    linear-gradient(180deg, rgb(255 255 255 / 0.18), rgb(255 255 255 / 0.02)),
    var(--accent-ember-soft);
  color: var(--accent-ember-text-strong);
  box-shadow:
    inset 0 1px 0 rgb(255 255 255 / 0.28),
    0 8px 18px rgb(249 115 22 / 0.10);
}

.admin-catalogs__intro {
  display: flex;
  justify-content: space-between;
  gap: var(--space-4);
}

.admin-catalogs__intro--stacked {
  align-items: start;
}

.admin-catalogs__intro h2,
.admin-catalogs__group h3,
.admin-catalogs__intro p {
  margin: 0;
}

.admin-catalogs__intro p {
  max-width: 54rem;
  color: var(--color-text-muted);
  font-size: 0.88rem;
}

.admin-catalogs__stats {
  display: flex;
  flex-wrap: wrap;
  justify-content: flex-end;
  gap: var(--space-2);
  color: var(--color-text-muted);
  font-size: 0.82rem;
  font-weight: 750;
}

.admin-catalogs__stats span {
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  padding: 0.35rem 0.65rem;
}

.admin-catalogs__group {
  display: grid;
  gap: var(--space-2);
}

.admin-catalogs__search {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  min-height: 2.45rem;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  background: var(--surface-input);
  color: var(--color-text-muted);
  padding: 0 var(--space-3);
}

.admin-catalogs__search input {
  width: 100%;
  border: 0;
  outline: 0;
  background: transparent;
  color: var(--color-text);
  font: inherit;
}

.admin-catalogs__table {
  display: grid;
  overflow: hidden;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
}

.admin-catalogs__table--wb {
  max-height: 46rem;
  overflow: auto;
}

.admin-catalogs__row,
.admin-catalogs__wb-row {
  display: grid;
  gap: var(--space-3);
  align-items: start;
  border-top: 1px solid var(--color-border);
  padding: var(--space-3);
}

.admin-catalogs__row {
  grid-template-columns: minmax(13rem, 1fr) minmax(18rem, 2fr) minmax(14rem, 1.2fr) minmax(8rem, 0.6fr) minmax(8rem, 0.7fr) minmax(8rem, 0.6fr);
}

.admin-catalogs__wb-row {
  grid-template-columns: minmax(13rem, 1.2fr) minmax(10rem, 0.8fr) minmax(18rem, 1.5fr) minmax(7rem, 0.5fr) minmax(7rem, 0.5fr) minmax(12rem, 0.8fr);
}

.admin-catalogs__row:first-child,
.admin-catalogs__wb-row:first-child {
  border-top: 0;
}

.admin-catalogs__row--head {
  position: sticky;
  top: 0;
  z-index: 1;
  background: var(--surface-table-header-strong);
  color: var(--text-table-header);
  font-size: 0.76rem;
  font-weight: 750;
  text-transform: uppercase;
}

.admin-catalogs__row div,
.admin-catalogs__wb-row div {
  display: grid;
  gap: 0.25rem;
}

.admin-catalogs__row small,
.admin-catalogs__wb-row small {
  color: var(--color-text-muted);
  font-size: 0.75rem;
}

.admin-catalogs__status,
.admin-catalogs__tone,
.admin-catalogs__verification {
  width: max-content;
  border-radius: 999px;
  padding: 0.18rem 0.55rem;
  font-size: 0.75rem;
  font-weight: 750;
}

.admin-catalogs__verification--not_ready {
  background: var(--surface-active-overlay);
  color: var(--color-text-muted);
}

.admin-catalogs__verification--needs_data_export {
  background: var(--accent-ember-soft);
  color: var(--accent-ember-text-strong);
}

.admin-catalogs__verification--ready {
  background: var(--color-success-soft);
  color: var(--state-success-text);
}

.admin-catalogs__tone--positive {
  background: var(--color-success-soft);
  color: var(--state-success-text);
}

.admin-catalogs__tone--negative {
  background: var(--accent-ember-soft);
  color: var(--accent-ember-text-strong);
}

.admin-catalogs__tone--neutral {
  background: rgb(229 231 235 / 0.92);
  color: #374151;
}

.admin-catalogs__status--active {
  background: var(--color-success-soft);
  color: var(--state-success-text);
}

.admin-catalogs__status--experimental {
  background: var(--accent-ember-soft);
  color: var(--accent-ember-text-strong);
}

.admin-catalogs__status--disabled {
  background: var(--color-danger-soft);
  color: var(--state-danger-text);
}

.admin-catalogs__button {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
  min-height: 2.25rem;
  border: 1px solid var(--color-ember);
  border-radius: var(--radius-sm);
  background: transparent;
  color: var(--accent-ember-text-strong);
  padding: 0 var(--space-3);
  font-weight: 750;
}

.admin-catalogs__button:disabled {
  cursor: not-allowed;
  opacity: 0.55;
}

.admin-catalogs__notice {
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  padding: var(--space-3);
  font-size: 0.85rem;
  font-weight: 650;
}

.admin-catalogs__notice--error {
  border-color: var(--state-danger-border);
  background: var(--color-danger-soft);
  color: var(--state-danger-text);
}

@media (max-width: 980px) {
  .admin-catalogs__intro,
  .admin-catalogs__stats {
    justify-content: flex-start;
  }

  .admin-catalogs__tabs {
    width: 100%;
  }

  button.admin-catalogs__tab {
    flex: 1;
    min-width: 0;
  }

  .admin-catalogs__row,
  .admin-catalogs__wb-row {
    grid-template-columns: 1fr;
  }
}
</style>
