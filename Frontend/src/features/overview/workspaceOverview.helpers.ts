import type { ParserProductListItem } from '@/features/parser-products/parserProducts.types';

import type {
  WorkspaceOverviewChange,
  WorkspaceOverviewProduct,
  WorkspaceOverviewSimilarProduct,
  WorkspaceOverviewSimilarProductGroup,
  WorkspaceOverviewSimilarProductGroupItem
} from './workspaceOverview.types';

export type ProductMetric = {
  label: string;
  value: string;
};

export type TagItem = {
  key: string;
  label: string;
  tone: 'positive' | 'negative' | 'neutral' | 'warning';
};

export function formatNumber(value: number | null | undefined): string {
  return value === null || value === undefined
    ? 'Нет данных'
    : new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 2 }).format(value);
}

export function formatMoney(value: number | null | undefined): string {
  return value === null || value === undefined
    ? 'Нет данных'
    : `${new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 2 }).format(value)} ₽`;
}

export function formatRating(value: number | null | undefined): string {
  return value === null || value === undefined
    ? 'Нет данных'
    : new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 1 }).format(value);
}

export function productMetrics(product: WorkspaceOverviewProduct): ProductMetric[] {
  return [
    { label: 'Цена', value: formatMoney(product.currentPrice) },
    { label: 'Позиция', value: product.currentPosition ? `#${formatNumber(product.currentPosition)}` : 'Нет данных' },
    { label: 'Остаток', value: formatNumber(product.currentStock) },
    { label: 'Отзывы', value: formatNumber(product.currentFeedbackCount) },
    { label: 'Оценка', value: formatRating(product.currentReviewRating) }
  ];
}

export function comparisonTags(
  product: WorkspaceOverviewProduct,
  similar: WorkspaceOverviewSimilarProduct
): TagItem[] {
  const tags: TagItem[] = [];

  if (product.currentPrice !== null && similar.price !== null) {
    const delta = similar.price - product.currentPrice;
    if (Math.abs(delta) >= 1) {
      tags.push({
        key: 'price',
        label: delta < 0 ? `Дешевле на ${formatMoney(Math.abs(delta))}` : `Дороже на ${formatMoney(delta)}`,
        tone: delta < 0 ? 'positive' : 'negative'
      });
    }
  }

  if (product.currentPosition !== null && similar.position !== null) {
    const delta = similar.position - product.currentPosition;
    if (delta !== 0) {
      tags.push({
        key: 'position',
        label: delta < 0
          ? `Выше на ${formatNumber(Math.abs(delta))} ${placeWord(Math.abs(delta))}`
          : `Ниже на ${formatNumber(delta)} ${placeWord(delta)}`,
        tone: delta < 0 ? 'positive' : 'negative'
      });
    }
  }

  if (product.currentFeedbackCount !== null && similar.feedbackCount !== null) {
    const delta = similar.feedbackCount - product.currentFeedbackCount;
    if (delta !== 0) {
      tags.push({
        key: 'feedbacks',
        label: delta > 0
          ? `Отзывов больше на ${formatNumber(delta)}`
          : `Отзывов меньше на ${formatNumber(Math.abs(delta))}`,
        tone: delta > 0 ? 'positive' : 'negative'
      });
    }
  }

  if (product.currentReviewRating !== null && similar.rating !== null) {
    const delta = similar.rating - product.currentReviewRating;
    if (Math.abs(delta) >= 0.05) {
      tags.push({
        key: 'reviewRating',
        label: delta > 0
          ? `Оценка выше на ${formatRatingDelta(Math.abs(delta))}`
          : `Оценка ниже на ${formatRatingDelta(Math.abs(delta))}`,
        tone: delta > 0 ? 'positive' : 'negative'
      });
    }
  }

  if (product.currentStock !== null && similar.totalQuantity !== null) {
    const delta = similar.totalQuantity - product.currentStock;
    if (delta !== 0) {
      tags.push({
        key: 'stock',
        label: delta > 0
          ? `Остаток выше на ${formatNumber(delta)}`
          : `Остаток ниже на ${formatNumber(Math.abs(delta))}`,
        tone: delta > 0 ? 'positive' : 'negative'
      });
    }
  }

  return tags.length > 0
    ? tags
    : [{ key: 'similar', label: 'Похожа по карточке', tone: 'neutral' }];
}

export function changeTags(product: WorkspaceOverviewProduct): TagItem[] {
  return [
    product.priceChange,
    product.positionChange,
    product.stockChange,
    product.feedbackChange,
    product.reviewRatingChange
  ]
    .filter((change) => change.delta !== null && change.delta !== 0)
    .map((change) => ({
      key: change.key,
      label: `${changeTagLabel(change)} ${changeDisplayValue(change)}`,
      tone: change.state === 'positive'
        ? 'positive'
        : change.state === 'negative'
          ? 'negative'
          : 'neutral'
    }));
}

export function signalTags(product: WorkspaceOverviewProduct): TagItem[] {
  const changeSignalCodes = new Set(['price_changed', 'position_changed', 'stock_changed', 'new_reviews']);
  return product.signals
    .filter((signal) => !changeSignalCodes.has(signal.code))
    .slice(0, 5)
    .map((signal) => ({
      key: signal.code,
      label: signal.title,
      tone: signal.severity === 'high'
        ? 'negative'
        : signal.severity === 'medium'
          ? 'warning'
          : 'neutral'
    }));
}

export function similarGroupTitle(group: WorkspaceOverviewSimilarProductGroup): string {
  const titles: Record<string, string> = {
    price_disadvantage: 'Дешевле',
    position_disadvantage: 'Выше в выдаче',
    review_count_disadvantage: 'Больше отзывов',
    rating_disadvantage: 'Оценка выше',
    stock_disadvantage: 'Остаток выше',
    weak_competitor_cards: 'Слабые похожие'
  };

  return titles[group.key] ?? group.title;
}

export function similarGroupItemTags(
  product: WorkspaceOverviewProduct,
  item: WorkspaceOverviewSimilarProductGroupItem,
  groupKey: string
): TagItem[] {
  if (groupKey === 'weak_competitor_cards') {
    const facts = item.tags.length > 0 ? item.tags : item.facts;
    return facts.length > 0
      ? facts.map((fact, index) => ({
          key: `weak-${index}`,
          label: compactWeakFact(fact),
          tone: 'negative'
        }))
      : [{ key: 'weak', label: 'Слабые параметры', tone: 'negative' }];
  }

  return comparisonTags(product, item.product);
}

export function similarGroupTabs(product: WorkspaceOverviewProduct): WorkspaceOverviewSimilarProductGroup[] {
  return (product.similarProductGroups ?? []).filter((group) => group.items.length > 0);
}

export function preferredSimilarGroup(groups: WorkspaceOverviewSimilarProductGroup[]): WorkspaceOverviewSimilarProductGroup {
  const priority = [
    'price_disadvantage',
    'position_disadvantage',
    'review_count_disadvantage',
    'rating_disadvantage',
    'stock_disadvantage',
    'weak_competitor_cards'
  ];
  for (const key of priority) {
    const group = groups.find((item) => item.key === key);
    if (group) {
      return group;
    }
  }

  return groups[0];
}

export function tagClass(tag: TagItem, prefix = 'overview-tag'): string {
  return `${prefix}--${tag.tone}`;
}

export function toParserProduct(product: WorkspaceOverviewProduct): ParserProductListItem {
  return {
    id: product.parserProductRowId,
    parserRunId: '',
    parsedAtUtc: product.latestObservedAtUtc ?? new Date().toISOString(),
    wbProductId: product.wbProductId,
    wbRootId: product.wbRootId,
    name: product.name,
    brandName: product.brandName,
    sellerName: product.sellerName,
    priceRegular: product.currentPrice,
    priceDiscounted: product.currentPrice,
    priceWbWallet: product.currentPrice,
    discountPercent: null,
    totalQuantity: product.currentStock,
    ratingRounded: product.currentReviewRating ? Math.round(product.currentReviewRating) : null,
    reviewRating: product.currentReviewRating,
    feedbackCount: product.currentFeedbackCount,
    sourceCategory: product.sourceCategory,
    sourceSubcategory: product.sourceSubcategory,
    sourceQuery: null,
    thumbnailUrl: product.thumbnailUrl,
    rank: product.currentPosition
      ? {
          absolutePosition: product.currentPosition,
          page: 1,
          positionOnPage: product.currentPosition,
          query: product.sourceSubcategory ?? '',
          sourceCategory: product.sourceCategory,
          sourceSubcategory: product.sourceSubcategory,
          sourceRegionDest: null,
          sort: null,
          observedAtUtc: product.latestObservedAtUtc ?? new Date().toISOString(),
          parserRunId: '',
          rankContextId: '',
          contextsCount: 1
        }
      : null,
    position: {
      state: product.currentPosition ? 'observed' : 'unknown',
      absolutePosition: product.currentPosition,
      observedRangeLimit: null,
      query: product.sourceSubcategory,
      sourceCategory: product.sourceCategory,
      sourceSubcategory: product.sourceSubcategory,
      observedAtUtc: product.latestObservedAtUtc
    },
    parsedReviewEvidence: undefined,
    logistics: null
  };
}

export function toParserSimilarProduct(similar: WorkspaceOverviewSimilarProduct): ParserProductListItem | null {
  if (!similar.parserProductRowId || !similar.wbProductId) {
    return null;
  }

  return {
    id: similar.parserProductRowId,
    parserRunId: '',
    parsedAtUtc: new Date().toISOString(),
    wbProductId: similar.wbProductId,
    wbRootId: similar.wbRootId,
    name: similar.name,
    brandName: similar.brandName,
    sellerName: similar.sellerName,
    priceRegular: similar.price,
    priceDiscounted: similar.price,
    priceWbWallet: similar.price,
    discountPercent: null,
    totalQuantity: similar.totalQuantity,
    ratingRounded: similar.rating ? Math.round(similar.rating) : null,
    reviewRating: similar.rating,
    feedbackCount: similar.feedbackCount,
    sourceCategory: null,
    sourceSubcategory: similar.sourceSubcategory,
    sourceQuery: null,
    thumbnailUrl: similar.thumbnailUrl,
    rank: similar.position
      ? {
          absolutePosition: similar.position,
          page: 1,
          positionOnPage: similar.position,
          query: similar.sourceSubcategory ?? '',
          sourceCategory: null,
          sourceSubcategory: similar.sourceSubcategory,
          sourceRegionDest: null,
          sort: null,
          observedAtUtc: new Date().toISOString(),
          parserRunId: '',
          rankContextId: '',
          contextsCount: 1
        }
      : null,
    position: {
      state: similar.position ? 'observed' : 'unknown',
      absolutePosition: similar.position,
      observedRangeLimit: null,
      query: similar.sourceSubcategory,
      sourceCategory: null,
      sourceSubcategory: similar.sourceSubcategory,
      observedAtUtc: null
    },
    parsedReviewEvidence: undefined,
    logistics: null
  };
}

function compactWeakFact(value: string): string {
  return value
    .replace(/^У похожей\s+/i, '')
    .replace(/:\s*/g, ' ')
    .replace(/^низкая/i, 'Низкая')
    .replace(/^мало/i, 'Мало')
    .replace(/^низкий/i, 'Низкий');
}

function formatRatingDelta(value: number): string {
  return new Intl.NumberFormat('ru-RU', {
    minimumFractionDigits: 1,
    maximumFractionDigits: 1
  }).format(value);
}

function changeTagLabel(change: WorkspaceOverviewChange): string {
  if (change.key === 'price') {
    return 'Новая цена';
  }

  if (change.key === 'position') {
    return 'Новая позиция';
  }

  if (change.key === 'feedbacks') {
    return 'Новые отзывы';
  }

  if (change.key === 'reviewRating') {
    return 'Новая оценка';
  }

  if (change.key === 'stock') {
    return 'Новые остатки';
  }

  return change.label;
}

function changeDisplayValue(change: WorkspaceOverviewChange): string {
  if (change.key === 'position' && change.delta !== null) {
    if (change.delta === 0) {
      return 'без изменений';
    }

    return change.delta < 0
      ? `↑ ${formatNumber(Math.abs(change.delta))} ${placeWord(Math.abs(change.delta))}`
      : `↓ ${formatNumber(Math.abs(change.delta))} ${placeWord(Math.abs(change.delta))}`;
  }

  if (change.key === 'price' && change.delta !== null) {
    const prefix = change.delta > 0 ? '+' : '-';
    return `${prefix}${formatMoney(Math.abs(change.delta))}`;
  }

  if (change.key === 'reviewRating' && change.delta !== null) {
    const prefix = change.delta > 0 ? '+' : '-';
    return `${prefix}${formatRating(Math.abs(change.delta))}`;
  }

  if (change.delta !== null) {
    const prefix = change.delta > 0 ? '+' : '-';
    return `${prefix}${formatNumber(Math.abs(change.delta))}`;
  }

  return change.displayValue;
}

function placeWord(value: number): string {
  const normalized = Math.abs(Math.trunc(value));
  const lastTwo = normalized % 100;
  const last = normalized % 10;

  if (lastTwo >= 11 && lastTwo <= 14) {
    return 'мест';
  }

  if (last === 1) {
    return 'место';
  }

  if (last >= 2 && last <= 4) {
    return 'места';
  }

  return 'мест';
}
