<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue';
import { RotateCcw, SlidersHorizontal } from 'lucide-vue-next';

import ParserProductDetailDrawer from '@/features/parser-products/ParserProductDetailDrawer.vue';
import MarketProductImage from '@/features/parser-products/MarketProductImage.vue';
import { getParserProductFilterOptions } from '@/features/parser-products/parserProducts.api';
import type {
  ParserProductListItem,
  ParserProductPositionState
} from '@/features/parser-products/parserProducts.types';
import {
  getRuleConstructorCounts,
  getRuleConstructorFilters,
  searchRuleConstructor
} from '@/features/rule-constructor/ruleConstructor.api';
import type {
  RuleConstructorFilter,
  RuleConstructorRuleCount,
  RuleConstructorSearchItem,
  RuleGroupOperator
} from '@/features/rule-constructor/ruleConstructor.types';
import PageHeader from '@/widgets/PageHeader.vue';
import { getProblemMessage } from '@/shared/api/problemDetails';
import EmptyState from '@/shared/ui/EmptyState.vue';
import LoadingState from '@/shared/ui/LoadingState.vue';
import RuleFormulaBuilder from './RuleFormulaBuilder.vue';
import {
  collectRuleIds,
  compileRuleFormula,
  insertBracketPair,
  removeBracketPair,
  type RuleFormulaToken
} from './ruleFormulaCompiler';

type FilterGroup = {
  name: string;
  filters: RuleConstructorFilter[];
};

const filters = ref<RuleConstructorFilter[]>([]);
const filterOptions = ref({
  categories: [] as string[],
  subcategories: [] as string[],
  brands: [] as string[],
  sellers: [] as string[]
});
const tokens = ref<RuleFormulaToken[]>([]);
const pendingRule = ref<RuleConstructorFilter | null>(null);
const bracketDraftBoundary = ref<number | null>(null);
const formulaNotice = ref('');
const rows = ref<RuleConstructorSearchItem[]>([]);
const total = ref(0);
const constructorTotal = ref(0);
const ruleCounts = ref<Record<string, RuleConstructorRuleCount>>({});
const page = ref(1);
const pageSize = 5;
const loading = ref(false);
const loadingFilters = ref(false);
const error = ref('');
const filtersError = ref('');
const selectedProduct = ref<ParserProductListItem | null>(null);
let refreshTimer: number | undefined;
let refreshVersion = 0;
let tokenCounter = 0;

const criteria = reactive({
  sourceCategory: '',
  sourceSubcategory: ''
});

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
const filtersById = computed<Record<string, RuleConstructorFilter>>(() =>
  Object.fromEntries(filters.value.map((filter) => [filter.id, filter]))
);
const compileResult = computed(() => compileRuleFormula(tokens.value));
const expressionSignature = computed(() =>
  compileResult.value.valid && compileResult.value.expression
    ? JSON.stringify(compileResult.value.expression)
    : ''
);
const totalPages = computed(() => Math.max(1, Math.ceil(total.value / pageSize)));
const paginationItems = computed(() => buildPaginationItems(page.value, totalPages.value));
const usedRuleIds = computed(() => new Set(collectRuleIds(tokens.value)));
const ruleTokenCount = computed(() => tokens.value.filter((token) => token.kind === 'rule').length);
const builderStatus = computed(() => {
  if (pendingRule.value) {
    return 'Выберите И или ИЛИ, чтобы добавить следующее правило.';
  }

  if (bracketDraftBoundary.value !== null) {
    return 'Выберите место закрывающей скобки правее выбранной границы.';
  }

  if (!compileResult.value.valid) {
    return compileResult.value.error;
  }

  return formulaNotice.value || null;
});

onMounted(async () => {
  await Promise.all([loadFilters(), loadFilterOptions()]);
  await refreshData();
});

watch(
  [
    expressionSignature,
    () => criteria.sourceCategory,
    () => criteria.sourceSubcategory
  ],
  () => {
    page.value = 1;
    scheduleRefresh();
  }
);

watch(page, () => {
  scheduleRefresh();
});

async function loadFilters() {
  loadingFilters.value = true;
  filtersError.value = '';

  try {
    filters.value = await getRuleConstructorFilters();
  } catch (err) {
    filtersError.value = getProblemMessage(err, 'Не удалось загрузить фильтры конструктора.');
  } finally {
    loadingFilters.value = false;
  }
}

async function loadFilterOptions() {
  try {
    filterOptions.value = await getParserProductFilterOptions({});
  } catch (err) {
    filtersError.value = getProblemMessage(err, 'Не удалось загрузить справочник ниш.');
  }
}

function scheduleRefresh() {
  window.clearTimeout(refreshTimer);
  refreshTimer = window.setTimeout(() => {
    void refreshData();
  }, 320);
}

async function refreshData() {
  if (!compileResult.value.valid || !compileResult.value.expression || pendingRule.value || bracketDraftBoundary.value !== null) {
    return;
  }

  const version = ++refreshVersion;
  loading.value = true;
  error.value = '';

  try {
    const requestBase = {
      expression: compileResult.value.expression,
      sourceCategory: criteria.sourceCategory || undefined,
      sourceSubcategory: criteria.sourceSubcategory || undefined
    };
    const [countsResponse, searchResponse] = await Promise.all([
      getRuleConstructorCounts(requestBase),
      searchRuleConstructor({
        ...requestBase,
        ruleIds: [],
        combineMode: 'all',
        page: page.value,
        pageSize
      })
    ]);

    if (version !== refreshVersion) {
      return;
    }

    constructorTotal.value = countsResponse.total;
    ruleCounts.value = Object.fromEntries(countsResponse.ruleCounts.map((item) => [item.ruleId, item]));
    rows.value = searchResponse.items;
    total.value = searchResponse.total;
    formulaNotice.value = '';
  } catch (err) {
    if (version !== refreshVersion) {
      return;
    }

    rows.value = [];
    total.value = 0;
    constructorTotal.value = 0;
    ruleCounts.value = {};
    error.value = getProblemMessage(err, 'Не удалось пересчитать конструктор правил.');
  } finally {
    if (version === refreshVersion) {
      loading.value = false;
    }
  }
}

function addRule(filter: RuleConstructorFilter) {
  if (filter.status === 'disabled') {
    return;
  }

  if (usedRuleIds.value.has(filter.id)) {
    formulaNotice.value = 'Это правило уже есть в формуле.';
    return;
  }

  formulaNotice.value = '';

  if (!hasRuleTokens()) {
    tokens.value = [createRuleToken(filter.id)];
    pendingRule.value = null;
    bracketDraftBoundary.value = null;
    return;
  }

  pendingRule.value = filter;
}

function commitPendingRule(operator: RuleGroupOperator) {
  if (!pendingRule.value) {
    return;
  }

  tokens.value = [
    ...tokens.value,
    createOperatorToken(operator),
    createRuleToken(pendingRule.value.id)
  ];
  pendingRule.value = null;
  formulaNotice.value = '';
}

function cancelPendingRule() {
  pendingRule.value = null;
  formulaNotice.value = '';
}

function setOperator(tokenId: string, operator: RuleGroupOperator) {
  tokens.value = tokens.value.map((token) =>
    token.id === tokenId && token.kind === 'operator'
      ? { ...token, operator }
      : token
  );
}

function removeRule(tokenId: string) {
  const index = tokens.value.findIndex((token) => token.id === tokenId);
  if (index < 0) {
    return;
  }

  const next = [...tokens.value];
  next.splice(index, 1);

  if (next[index - 1]?.kind === 'operator') {
    next.splice(index - 1, 1);
  } else if (next[index]?.kind === 'operator') {
    next.splice(index, 1);
  }

  tokens.value = normalizeAfterRemoval(next);
  pendingRule.value = null;
  bracketDraftBoundary.value = null;
}

function onBracketBoundary(boundary: number) {
  if (!hasRuleTokens()) {
    return;
  }

  if (ruleTokenCount.value < 2) {
    formulaNotice.value = 'Скобки можно ставить только вокруг фрагмента из двух или более правил.';
    return;
  }

  if (bracketDraftBoundary.value === null || boundary <= bracketDraftBoundary.value) {
    bracketDraftBoundary.value = boundary;
    formulaNotice.value = 'Теперь выберите место закрывающей скобки правее.';
    return;
  }

  const pairId = createTokenId('paren');
  const candidate = insertBracketPair(tokens.value, bracketDraftBoundary.value, boundary, pairId);
  if (countRulesBetween(tokens.value, bracketDraftBoundary.value, boundary) < 2) {
    formulaNotice.value = 'Скобки должны охватывать минимум два правила и операцию между ними.';
    return;
  }

  const result = compileRuleFormula(candidate);
  if (!result.valid) {
    formulaNotice.value = result.error ?? 'Такую скобку нельзя поставить.';
    return;
  }

  tokens.value = candidate;
  bracketDraftBoundary.value = null;
  formulaNotice.value = '';
}

function removeBracket(pairId: string) {
  tokens.value = removeBracketPair(tokens.value, pairId);
  bracketDraftBoundary.value = null;
  formulaNotice.value = '';
}

function cancelBracketDraft() {
  bracketDraftBoundary.value = null;
  formulaNotice.value = '';
}

function resetBuilder() {
  tokens.value = [];
  pendingRule.value = null;
  bracketDraftBoundary.value = null;
  formulaNotice.value = '';
  criteria.sourceCategory = '';
  criteria.sourceSubcategory = '';
  page.value = 1;
  scheduleRefresh();
}

function createRuleToken(ruleId: string): RuleFormulaToken {
  return {
    id: createTokenId('rule'),
    kind: 'rule',
    ruleId
  };
}

function createOperatorToken(operator: RuleGroupOperator): RuleFormulaToken {
  return {
    id: createTokenId('operator'),
    kind: 'operator',
    operator
  };
}

function createTokenId(prefix: string): string {
  tokenCounter += 1;
  return `${prefix}-${Date.now()}-${tokenCounter}`;
}

function hasRuleTokens(): boolean {
  return tokens.value.some((token) => token.kind === 'rule');
}

function countRulesBetween(source: RuleFormulaToken[], startBoundary: number, endBoundary: number): number {
  return source
    .slice(startBoundary, endBoundary)
    .filter((token) => token.kind === 'rule')
    .length;
}

function normalizeAfterRemoval(next: RuleFormulaToken[]): RuleFormulaToken[] {
  let normalized = next.filter((token, index, list) => {
    if (token.kind !== 'operator') {
      return true;
    }

    const previous = list[index - 1];
    const following = list[index + 1];
    return previous?.kind === 'rule' || previous?.kind === 'paren'
      ? following?.kind === 'rule' || following?.kind === 'paren'
      : false;
  });

  if (!compileRuleFormula(normalized).valid) {
    normalized = normalized.filter((token) => token.kind !== 'paren');
  }

  return compileRuleFormula(normalized).valid ? normalized : normalized.filter((token) => token.kind === 'rule');
}

function isRuleUsed(filterId: string): boolean {
  return usedRuleIds.value.has(filterId) || pendingRule.value?.id === filterId;
}

function isZeroCountRule(filter: RuleConstructorFilter): boolean {
  return filter.status !== 'disabled' && ruleCounts.value[filter.id]?.count === 0;
}

function ruleCountText(filter: RuleConstructorFilter): string {
  if (filter.status === 'disabled') {
    return '-';
  }

  const count = ruleCounts.value[filter.id]?.count;
  return count === null || count === undefined ? '...' : formatNumber(count);
}

function onRuleDragStart(event: DragEvent, filter: RuleConstructorFilter) {
  if (filter.status === 'disabled') {
    event.preventDefault();
    return;
  }

  event.dataTransfer?.setData('text/plain', filter.id);
  event.dataTransfer?.setData('application/x-rule-id', filter.id);
  if (event.dataTransfer) {
    event.dataTransfer.effectAllowed = 'copy';
  }
}

function addRuleById(ruleId: string) {
  const filter = filtersById.value[ruleId];
  if (filter) {
    addRule(filter);
  }
}

function openProduct(item: RuleConstructorSearchItem) {
  selectedProduct.value = toParserProductListItem(item);
}

function toParserProductListItem(item: RuleConstructorSearchItem): ParserProductListItem {
  return {
    id: item.id,
    parserRunId: '',
    parsedAtUtc: '',
    wbProductId: item.wbProductId,
    wbRootId: item.wbRootId,
    name: item.name,
    brandName: item.brandName,
    sellerName: item.sellerName,
    priceRegular: null,
    priceDiscounted: item.priceDiscounted,
    priceWbWallet: null,
    discountPercent: null,
    totalQuantity: item.totalQuantity,
    ratingRounded: null,
    reviewRating: item.reviewRating,
    feedbackCount: item.feedbackCount,
    sourceCategory: item.sourceCategory,
    sourceSubcategory: item.sourceSubcategory,
    sourceQuery: item.sourceQuery,
    thumbnailUrl: item.thumbnailUrl,
    rank: null,
    position: {
      state: normalizePositionState(item.positionState),
      absolutePosition: item.positionAbsolute,
      observedRangeLimit: item.positionObservedRangeLimit,
      query: item.sourceQuery,
      sourceCategory: item.sourceCategory,
      sourceSubcategory: item.sourceSubcategory,
      observedAtUtc: null
    },
    logistics: null
  };
}

function normalizePositionState(value: string | null): ParserProductPositionState {
  return value === 'observed' || value === 'beyondObservedRange' ? value : 'unknown';
}

function formatMoney(value: number | null): string {
  if (value === null) {
    return '-';
  }

  return new Intl.NumberFormat('ru-RU', {
    style: 'currency',
    currency: 'RUB',
    maximumFractionDigits: 0
  }).format(value);
}

function formatNumber(value: number | null): string {
  if (value === null) {
    return '-';
  }

  return new Intl.NumberFormat('ru-RU').format(value);
}

function positionText(item: RuleConstructorSearchItem): string {
  if (item.positionAbsolute !== null) {
    return `#${item.positionAbsolute}`;
  }

  if (item.positionState === 'beyondObservedRange' && item.positionObservedRangeLimit !== null) {
    return `>${item.positionObservedRangeLimit}`;
  }

  return 'Нет данных';
}

function setPage(nextPage: number): void {
  page.value = Math.min(Math.max(nextPage, 1), totalPages.value);
}

function buildPaginationItems(currentPage: number, total: number): Array<number | string> {
  if (total <= 7) {
    return Array.from({ length: total }, (_, index) => index + 1);
  }

  if (currentPage <= 4) {
    return [1, 2, 3, 4, 5, 'end-ellipsis', total];
  }

  if (currentPage >= total - 3) {
    return [1, 'start-ellipsis', total - 4, total - 3, total - 2, total - 1, total];
  }

  return [1, 'start-ellipsis', currentPage - 1, currentPage, currentPage + 1, 'end-ellipsis', total];
}

function onCardKeydown(event: KeyboardEvent, item: RuleConstructorSearchItem) {
  if (event.key !== 'Enter' && event.key !== ' ') {
    return;
  }

  event.preventDefault();
  openProduct(item);
}
</script>

<template>
  <section class="rule-constructor">
    <PageHeader
      title="Конструктор правил"
      description="Выберите факты, соберите логическое выражение и проверьте, какие карточки базы подходят под выбранные условия."
    />

    <div class="rule-constructor__filters">
      <div class="rule-constructor__field">
        <label for="rule-category">Категория</label>
        <select id="rule-category" v-model="criteria.sourceCategory">
          <option value="">Все категории</option>
          <option v-for="category in filterOptions.categories" :key="category" :value="category">
            {{ category }}
          </option>
        </select>
      </div>
      <div class="rule-constructor__field">
        <label for="rule-subcategory">Ниша</label>
        <select id="rule-subcategory" v-model="criteria.sourceSubcategory">
          <option value="">Все ниши</option>
          <option v-for="subcategory in filterOptions.subcategories" :key="subcategory" :value="subcategory">
            {{ subcategory }}
          </option>
        </select>
      </div>
      <button class="rule-constructor__secondary" type="button" @click="resetBuilder">
        <RotateCcw :size="16" />
        Сбросить
      </button>
    </div>

    <section class="rule-constructor__surface">
      <header class="rule-constructor__surface-head">
        <div>
          <h2>Набор правил по категориям</h2>
          <p>Счетчик рядом с правилом показывает, сколько карточек соответствует этому факту в текущей базе.</p>
        </div>
        <SlidersHorizontal :size="18" />
      </header>

      <div v-if="filtersError" class="rule-constructor__notice rule-constructor__notice--error">
        {{ filtersError }}
      </div>
      <LoadingState v-if="loadingFilters" :rows="4" />
      <div v-else class="rule-constructor__library">
        <section v-for="group in groupedFilters" :key="group.name" class="rule-constructor__library-group">
          <h3>{{ group.name }}</h3>
          <button
            v-for="filter in group.filters"
            :key="filter.id"
            class="rule-constructor__rule"
            :class="{
              'rule-constructor__rule--used': isRuleUsed(filter.id),
              'rule-constructor__rule--zero': isZeroCountRule(filter),
              'rule-constructor__rule--disabled': filter.status === 'disabled'
            }"
            type="button"
            :disabled="filter.status === 'disabled'"
            :draggable="filter.status !== 'disabled'"
            @click="addRule(filter)"
            @dragstart="onRuleDragStart($event, filter)"
          >
            <span class="rule-constructor__rule-name">{{ filter.name }}</span>
            <span class="rule-constructor__rule-count" aria-label="Количество карточек">
              {{ ruleCountText(filter) }}
            </span>
          </button>
        </section>
      </div>
    </section>

    <section class="rule-constructor__surface">
      <RuleFormulaBuilder
        :tokens="tokens"
        :filters-by-id="filtersById"
        :total="constructorTotal"
        :invalid-reason="builderStatus"
        :pending-rule="pendingRule"
        :bracket-draft-boundary="bracketDraftBoundary"
        @set-operator="setOperator"
        @remove-rule="removeRule"
        @remove-bracket="removeBracket"
        @bracket-boundary="onBracketBoundary"
        @cancel-bracket-draft="cancelBracketDraft"
        @commit-pending="commitPendingRule"
        @cancel-pending="cancelPendingRule"
        @drop-rule="addRuleById"
      />
    </section>

    <section class="rule-constructor__surface">
      <header class="rule-constructor__surface-head">
        <div>
          <h2>Результаты</h2>
          <p>{{ formatNumber(total) }} карточек совпали с текущим выражением.</p>
        </div>
      </header>

      <div v-if="error" class="rule-constructor__notice rule-constructor__notice--error">
        {{ error }}
      </div>
      <LoadingState v-if="loading" :rows="6" />
      <EmptyState
        v-else-if="rows.length === 0"
        title="Подходящих карточек нет"
        description="Измените выражение, нишу или поисковый фильтр."
      />

      <div v-else class="rule-constructor__cards">
        <article
          v-for="item in rows"
          :key="item.id"
          class="rule-constructor__card"
          role="button"
          tabindex="0"
          @click="openProduct(item)"
          @keydown="onCardKeydown($event, item)"
        >
          <MarketProductImage :src="item.thumbnailUrl" :alt="item.name" />
          <div class="rule-constructor__card-main">
            <span class="rule-constructor__niche">{{ item.sourceSubcategory || item.sourceCategory || 'Без ниши' }}</span>
            <h3>{{ item.name }}</h3>
            <p>{{ item.brandName || 'Бренд не указан' }} · {{ item.sellerName || 'Продавец не указан' }}</p>
            <small>WB {{ item.wbProductId }}</small>
            <div class="rule-constructor__facts">
              <span
                v-for="fact in item.matchedFacts"
                :key="fact.id"
                :class="`rule-constructor__fact--${fact.tone}`"
              >
                {{ fact.name }}<template v-if="fact.value">: {{ fact.value }}</template>
              </span>
            </div>
          </div>
          <dl class="rule-constructor__metrics">
            <div>
              <dt>Цена</dt>
              <dd>{{ formatMoney(item.priceDiscounted) }}</dd>
            </div>
            <div>
              <dt>Остаток</dt>
              <dd>{{ formatNumber(item.totalQuantity) }}</dd>
            </div>
            <div>
              <dt>Отзывы</dt>
              <dd>{{ formatNumber(item.feedbackCount) }}</dd>
            </div>
            <div>
              <dt>Позиция</dt>
              <dd>{{ positionText(item) }}</dd>
            </div>
          </dl>
        </article>
      </div>

      <div v-if="rows.length" class="rule-constructor__pagination">
        <button
          class="rule-constructor__secondary rule-constructor__pagination-nav"
          type="button"
          :disabled="page <= 1 || loading"
          @click="setPage(page - 1)"
        >
          Назад
        </button>
        <div class="rule-constructor__pagination-pages">
          <template v-for="item in paginationItems" :key="item">
            <span v-if="typeof item === 'string'" class="rule-constructor__pagination-ellipsis">...</span>
            <button
              v-else
              class="rule-constructor__pagination-page"
              :class="{ 'rule-constructor__pagination-page--active': item === page }"
              type="button"
              :disabled="loading"
              @click="setPage(item)"
            >
              {{ item }}
            </button>
          </template>
        </div>
        <button
          class="rule-constructor__secondary rule-constructor__pagination-nav"
          type="button"
          :disabled="page >= totalPages || loading"
          @click="setPage(page + 1)"
        >
          Вперед
        </button>
      </div>
    </section>

    <ParserProductDetailDrawer
      :open="Boolean(selectedProduct)"
      :product="selectedProduct"
      @close="selectedProduct = null"
    />
  </section>
</template>

<style scoped>
.rule-constructor {
  display: grid;
  gap: var(--space-4);
}

.rule-constructor__filters,
.rule-constructor__surface,
.rule-constructor__card {
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: color-mix(in srgb, var(--surface-card) 88%, transparent);
  box-shadow: 0 18px 48px color-mix(in srgb, var(--color-text) 7%, transparent);
  backdrop-filter: blur(12px) saturate(1.08);
}

.rule-constructor__filters {
  display: grid;
  grid-template-columns: minmax(14rem, 1fr) minmax(14rem, 1fr) auto;
  gap: var(--space-3);
  align-items: end;
  padding: var(--space-3);
}

.rule-constructor__surface {
  display: grid;
  gap: var(--space-4);
  padding: var(--space-4);
}

.rule-constructor__surface-head,
.rule-constructor__pagination {
  display: flex;
  align-items: center;
  gap: var(--space-3);
}

.rule-constructor__surface-head {
  justify-content: space-between;
}

.rule-constructor__pagination {
  justify-content: flex-end;
  margin-top: var(--space-2);
}

.rule-constructor__pagination-pages {
  display: flex;
  align-items: center;
  gap: 0.25rem;
}

.rule-constructor__pagination-page {
  display: inline-flex;
  min-width: 2rem;
  height: 2rem;
  align-items: center;
  justify-content: center;
  border: 1px solid rgb(249 115 22 / 0.18);
  border-radius: var(--radius-sm);
  background: var(--surface-control);
  color: var(--color-text-muted);
  cursor: pointer;
  font-size: 0.8125rem;
  font-weight: 720;
  transition: border-color 120ms ease, background 120ms ease, color 120ms ease, box-shadow 120ms ease;
}

.rule-constructor__pagination-page:not(:disabled):hover {
  border-color: var(--accent-ember-border);
  background:
    linear-gradient(180deg, rgb(249 115 22 / 0.09), transparent),
    var(--color-surface-hover);
  color: var(--accent-ember-text-strong);
}

.rule-constructor__pagination-page--active {
  border-color: var(--accent-primary-border);
  background:
    linear-gradient(180deg, rgb(249 115 22 / 0.22), rgb(249 115 22 / 0.08)),
    var(--surface-control-raised);
  color: var(--accent-ember-text-strong);
  box-shadow: inset 0 1px 0 rgb(255 255 255 / 0.045), 0 8px 20px rgb(249 115 22 / 0.08);
}

.rule-constructor__pagination-ellipsis {
  color: var(--color-text-muted);
}

.rule-constructor__pagination-nav {
  border-color: var(--accent-primary-border);
  background:
    linear-gradient(180deg, rgb(249 115 22 / 0.14), rgb(249 115 22 / 0.05)),
    var(--surface-control-raised);
  color: var(--accent-ember-text-strong);
  font-weight: 720;
}

.rule-constructor__pagination-nav:hover:not(:disabled) {
  border-color: var(--accent-primary-hover-border);
  background:
    linear-gradient(180deg, rgb(251 146 60 / 0.2), rgb(249 115 22 / 0.08)),
    var(--color-surface-hover);
}

.rule-constructor__pagination-nav:disabled {
  border-color: var(--color-border);
  background: var(--surface-control);
  color: var(--color-text-muted);
  opacity: 0.58;
}

h2,
h3,
p {
  margin: 0;
}

h2 {
  font-size: 1rem;
}

h3 {
  font-size: 0.92rem;
}

p,
small {
  color: var(--color-text-muted);
}

label {
  color: var(--color-text-muted);
  font-size: 0.78rem;
  font-weight: 800;
}

select,
input {
  width: 100%;
  min-height: 2.4rem;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  background: color-mix(in srgb, var(--surface-card) 88%, transparent);
  color: var(--color-text);
  padding: 0 var(--space-2);
  font: inherit;
}

.rule-constructor__field {
  display: grid;
  gap: var(--space-1);
}

.rule-constructor__secondary {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: var(--space-2);
  min-height: 2.45rem;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  background: color-mix(in srgb, var(--surface-card) 88%, transparent);
  color: var(--color-text);
  padding: 0 var(--space-3);
  font-weight: 800;
}

.rule-constructor__secondary:not(:disabled):hover {
  border-color: var(--color-ember);
  color: var(--accent-ember-text-strong);
}

.rule-constructor__notice {
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  padding: var(--space-3);
  font-weight: 700;
}

.rule-constructor__notice--error {
  border-color: var(--color-danger);
  background: var(--color-danger-soft);
  color: var(--color-danger-text);
}

.rule-constructor__library {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(15rem, 1fr));
  column-gap: var(--space-4);
  row-gap: var(--space-5);
}

.rule-constructor__library-group {
  display: grid;
  align-content: start;
  gap: var(--space-2);
  min-width: 0;
  padding-inline-start: var(--space-3);
  border-inline-start: 1px solid color-mix(in srgb, var(--color-border) 58%, transparent);
}

.rule-constructor__library-group:first-child {
  padding-inline-start: 0;
  border-inline-start: 0;
}

.rule-constructor__library-group h3 {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 1.6rem;
  margin: 0 0 var(--space-2);
  color: var(--color-text);
  font-size: 1rem;
  font-weight: 900;
  line-height: 1.25;
  letter-spacing: 0;
  text-align: center;
}

.rule-constructor__rule {
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(3.5rem, auto);
  gap: var(--space-2);
  align-items: center;
  min-height: 2.5rem;
  border: 1px solid transparent;
  border-radius: var(--radius-sm);
  background: transparent;
  color: var(--color-text);
  padding: 0.25rem var(--space-2);
  text-align: left;
}

.rule-constructor__rule:not(:disabled):hover {
  border-color: var(--color-border);
  background: var(--surface-card);
}

.rule-constructor__rule--used {
  color: color-mix(in srgb, var(--accent-ember-text-strong) 72%, transparent);
}

.rule-constructor__rule--disabled {
  color: var(--color-text-muted);
  opacity: 0.62;
}

.rule-constructor__rule--zero {
  color: var(--color-text-muted);
  opacity: 0.68;
}

.rule-constructor__rule-name {
  min-width: 0;
  overflow: hidden;
  font-weight: 600;
  line-height: 1.2;
  overflow-wrap: anywhere;
  white-space: normal;
}

.rule-constructor__rule-count {
  justify-self: end;
  min-width: 3.25rem;
  border: 1px solid color-mix(in srgb, var(--color-ember) 40%, var(--color-border));
  border-radius: 999px;
  background: color-mix(in srgb, var(--accent-ember-soft) 78%, var(--surface-card));
  color: var(--accent-ember-text-strong);
  padding: 0.16rem 0.5rem;
  font-size: 0.76rem;
  font-variant-numeric: tabular-nums;
  font-weight: 900;
  line-height: 1.25;
  text-align: center;
}

.rule-constructor__rule--disabled .rule-constructor__rule-count {
  border-color: var(--color-border);
  background: color-mix(in srgb, var(--surface-card) 75%, transparent);
  color: var(--color-text-muted);
}

.rule-constructor__rule--zero .rule-constructor__rule-count {
  border-color: var(--color-border);
  background: color-mix(in srgb, var(--surface-card) 75%, transparent);
  color: var(--color-text-muted);
}

.rule-constructor__cards {
  display: grid;
  gap: var(--space-3);
}

.rule-constructor__card {
  display: grid;
  grid-template-columns: 5.25rem minmax(0, 1fr) minmax(9rem, 0.28fr);
  gap: var(--space-3);
  align-items: start;
  padding: var(--space-3);
  text-align: left;
  cursor: pointer;
}

.rule-constructor__card:hover,
.rule-constructor__card:focus-visible {
  border-color: var(--color-ember);
  outline: none;
  box-shadow: 0 0 0 2px var(--accent-ember-soft);
}

.rule-constructor__card-main {
  display: grid;
  gap: var(--space-1);
}

.rule-constructor__card-main h3 {
  font-size: 1rem;
}

.rule-constructor__niche {
  color: var(--color-text-muted);
  font-size: 0.78rem;
  font-weight: 800;
}

.rule-constructor__facts {
  display: flex;
  flex-direction: column;
  gap: var(--space-1);
  align-items: flex-start;
  margin-top: var(--space-2);
}

.rule-constructor__facts span {
  border-radius: 999px;
  background: var(--accent-ember-soft);
  color: var(--accent-ember-text-strong);
  padding: 0.28rem 0.48rem;
  font-size: 0.78rem;
  font-weight: 800;
}

.rule-constructor__facts .rule-constructor__fact--positive {
  background: var(--color-success-soft);
  color: var(--state-success-text);
}

.rule-constructor__facts .rule-constructor__fact--neutral {
  background: rgb(229 231 235 / 0.92);
  color: #374151;
}

.rule-constructor__metrics {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: var(--space-3);
  margin: 0;
}

.rule-constructor__metrics div {
  display: grid;
  gap: var(--space-1);
}

.rule-constructor__metrics dt {
  color: var(--color-text-muted);
  font-size: 0.76rem;
  font-weight: 800;
}

.rule-constructor__metrics dd {
  margin: 0;
  font-weight: 900;
}

@media (max-width: 960px) {
  .rule-constructor__filters,
  .rule-constructor__card {
    grid-template-columns: 1fr;
  }

  .rule-constructor__metrics {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}
</style>
