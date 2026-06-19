<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';

import PageHeader from '@/widgets/PageHeader.vue';
import Badge from '@/shared/ui/Badge.vue';
import Button from '@/shared/ui/Button.vue';
import EmptyState from '@/shared/ui/EmptyState.vue';
import HelpTooltip from '@/shared/ui/HelpTooltip.vue';
import LoadingState from '@/shared/ui/LoadingState.vue';
import { getProblemMessage } from '@/shared/api/problemDetails';
import MarketProductDetailDrawer from '@/features/parser-products/ParserProductDetailDrawer.vue';
import MarketProductImage from '@/features/parser-products/MarketProductImage.vue';
import type { ParserProductListItem } from '@/features/parser-products/parserProducts.types';

import { getPublicMarketIntelligence } from './marketIntelligence.api';
import type {
  DeliveryBucket,
  PriceQualityPoint,
  PublicMarketIntelligence,
  PublicMarketIntelligenceParams,
  QualityBucket
} from './marketIntelligence.types';

type ChartPoint = PriceQualityPoint & {
  x: number;
  y: number;
  radius: number;
  hasRating: boolean;
};

type CorridorProductDot = {
  key: string;
  left: number;
};

type CorridorDistributionPoint = {
  x: number;
  y: number;
};

type CorridorMarkerKey = 'p25' | 'median' | 'p75';

type CorridorMarkerLayout = {
  key: CorridorMarkerKey;
  label: string;
  value: number | null | undefined;
  left: number;
  level: number;
  tone: 'boundary' | 'median';
};

const defaultRegionDest = '12354108';
const defaultSort = 'popular';
const legacyTopN = 1000;
const chartWidth = 1000;
const chartHeight = 560;
const plot = {
  left: 74,
  right: 958,
  top: 40,
  bottom: 404,
  noRatingY: 462
};

const demoContexts = [
  {
    label: 'Светильники бра',
    sourceCategory: 'Товары для дома',
    sourceSubcategory: 'Светильники бра',
    query: 'Светильники бра'
  },
  {
    label: 'Коврики для ванной',
    sourceCategory: 'Товары для дома',
    sourceSubcategory: 'Коврики для ванной',
    query: 'Коврики для ванной'
  },
  {
    label: 'Органайзеры для хранения вещей',
    sourceCategory: 'Товары для дома',
    sourceSubcategory: 'Органайзеры для хранения вещей',
    query: 'Органайзеры для хранения вещей'
  }
] as const;

const route = useRoute();
const router = useRouter();

const selectedSubcategory = ref(readInitialSubcategory());
const intelligence = ref<PublicMarketIntelligence | null>(null);
const loading = ref(false);
const error = ref<string | null>(null);
const activePoint = ref<ChartPoint | null>(null);
const selectedProduct = ref<ParserProductListItem | null>(null);
const chartCanvas = ref<HTMLCanvasElement | null>(null);
let pointerFrame: number | null = null;

const selectedContext = computed(
  () => demoContexts.find((context) => context.sourceSubcategory === selectedSubcategory.value) ?? demoContexts[0]
);

const requestParams = computed<PublicMarketIntelligenceParams>(() => ({
  sourceCategory: selectedContext.value.sourceCategory,
  sourceSubcategory: selectedContext.value.sourceSubcategory,
  query: selectedContext.value.query,
  sourceRegionDest: readStringQuery('sourceRegionDest') ?? defaultRegionDest,
  sort: readStringQuery('sort') ?? defaultSort,
  topN: legacyTopN,
  latestRankRunId: readStringQuery('latestRankRunId'),
  baselineRankRunId: readStringQuery('baselineRankRunId'),
  latestProductRunId: readStringQuery('latestProductRunId'),
  baselineProductRunId: readStringQuery('baselineProductRunId')
}));

const points = computed(() => intelligence.value?.priceQualityMap.points ?? []);
const summary = computed(() => intelligence.value?.priceQualityMap.summary ?? null);
const priceCorridors = computed(() => intelligence.value?.priceCorridors ?? null);
const marketConcentration = computed(() => intelligence.value?.marketConcentration ?? null);
const chartPoints = computed<ChartPoint[]>(() => buildChartPoints(points.value));
const corridorProductDots = computed<CorridorProductDot[]>(() => buildCorridorProductDots(points.value));
const corridorDistributionPoints = computed<CorridorDistributionPoint[]>(() => buildCorridorDistributionPoints(points.value));
const corridorDistributionPath = computed(() => buildSmoothDistributionPath(corridorDistributionPoints.value));
const corridorMarkerLayouts = computed<CorridorMarkerLayout[]>(() => buildCorridorMarkerLayouts());
const priceRange = computed(() => range(points.value.map((point) => point.price)));
const chartLegend = computed(() => [
  { bucket: 'strong' as const, label: 'Сильная карточка', count: summary.value?.strongCount ?? 0 },
  { bucket: 'medium' as const, label: 'Средняя', count: summary.value?.mediumCount ?? 0 },
  { bucket: 'weak' as const, label: 'Слабая', count: summary.value?.weakCount ?? 0 },
  { bucket: 'unknown' as const, label: 'Недостаточно данных', count: summary.value?.unknownCount ?? 0 }
]);
const corridorTopMarkers = computed(() => {
  const data = priceCorridors.value;
  if (!data) {
    return [];
  }

  return [
    { key: 'top10', label: 'Топ-10', value: data.top10Median },
    { key: 'top50', label: 'Топ-50', value: data.top50Median },
    { key: 'top100', label: 'Топ-100', value: data.top100Median }
  ].filter((marker) => marker.value !== null && marker.value !== undefined);
});
const concentrationMetrics = computed(() => {
  const data = marketConcentration.value;
  if (!data) {
    return [];
  }

  return [
    { label: 'Продавцов', value: formatNumber(data.uniqueSellersCount), detail: 'уникальных в topN' },
    { label: 'Брендов', value: formatNumber(data.uniqueBrandsCount), detail: 'уникальных в topN' },
    { label: 'Доля top-5', value: formatPercent(data.top5SellersSharePercent), detail: 'пять крупнейших продавцов' },
    { label: 'Индекс', value: formatPercent(data.normalizedConcentrationScore), detail: '0% фрагментирован, 100% занят лидерами' }
  ];
});

onMounted(() => {
  window.addEventListener('resize', handleWindowResize);
  void refresh();
});

onBeforeUnmount(() => {
  window.removeEventListener('resize', handleWindowResize);
  if (pointerFrame !== null) {
    window.cancelAnimationFrame(pointerFrame);
  }
});

watch(
  chartPoints,
  () => {
    activePoint.value = null;
    void nextTick(drawChart);
  },
  { flush: 'post' }
);

watch(
  activePoint,
  () => {
    void nextTick(drawChart);
  },
  { flush: 'post' }
);

watch(
  () => route.hash,
  () => {
    void nextTick(scrollToCurrentHash);
  },
  { flush: 'post' }
);

function handleWindowResize(): void {
  void nextTick(drawChart);
}

async function refresh(): Promise<void> {
  loading.value = true;
  error.value = null;
  activePoint.value = null;

  try {
    intelligence.value = await getPublicMarketIntelligence(requestParams.value);
  } catch (requestError) {
    intelligence.value = null;
    error.value = getProblemMessage(requestError, 'Не удалось загрузить карту ниши.');
  } finally {
    loading.value = false;
  }

  await nextTick();
  scrollToCurrentHash();
}

function applySubcategorySelection(): void {
  void router.replace({
    query: {
      ...route.query,
      sourceSubcategory: selectedSubcategory.value
    }
  });
  void refresh();
}

function scrollToCurrentHash(): void {
  const hash = route.hash;
  if (!hash) {
    return;
  }

  window.requestAnimationFrame(() => {
    const target = document.querySelector<HTMLElement>(hash);
    target?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  });
}

function buildChartPoints(source: PriceQualityPoint[]): ChartPoint[] {
  const prices = range(source.map((point) => point.price));
  const minPrice = prices.min ?? 0;
  const maxPrice = prices.max ?? 1;
  const priceSpan = Math.max(1, maxPrice - minPrice);

  return source.map((point) => {
    const hasRating = point.rating !== null && point.rating > 0;
    const normalizedPrice = point.price === null ? 0 : (point.price - minPrice) / priceSpan;
    const clampedRating = hasRating ? Math.min(5, Math.max(0, point.rating ?? 0)) : 0;

    return {
      ...point,
      x: plot.left + normalizedPrice * (plot.right - plot.left),
      y: hasRating
        ? plot.bottom - (clampedRating / 5) * (plot.bottom - plot.top)
        : plot.noRatingY,
      radius: pointRadius(),
      hasRating
    };
  });
}

function buildCorridorProductDots(source: PriceQualityPoint[]): CorridorProductDot[] {
  const pricedPoints = source.filter((point) => typeof point.price === 'number');
  const maxDots = 900;
  const step = Math.max(1, Math.ceil(pricedPoints.length / maxDots));
  const dots: CorridorProductDot[] = [];

  for (let index = 0; index < pricedPoints.length; index += step) {
    const point = pricedPoints[index];
    dots.push({
      key: `${point.wbProductId}-${index}`,
      left: corridorPosition(point.price)
    });
  }

  return dots;
}

function buildCorridorDistributionPoints(source: PriceQualityPoint[]): CorridorDistributionPoint[] {
  const prices = source
    .map((point) => point.price)
    .filter((price): price is number => typeof price === 'number');

  if (prices.length === 0) {
    return [];
  }

  const min = Math.min(...prices);
  const max = Math.max(...prices);
  const span = Math.max(1, max - min);
  const bucketCount = 28;
  const counts = Array.from({ length: bucketCount }, () => 0);

  for (const price of prices) {
    const index = Math.min(bucketCount - 1, Math.max(0, Math.floor(((price - min) / span) * bucketCount)));
    counts[index] += 1;
  }

  const smoothed = counts.map((count, index) => {
    const previous = counts[index - 1] ?? count;
    const next = counts[index + 1] ?? count;
    return previous * 0.25 + count * 0.5 + next * 0.25;
  });
  const maxCount = Math.max(...smoothed, 1);

  return smoothed.map((count, index) => ({
    x: (index / (bucketCount - 1)) * 100,
    y: 88 - (count / maxCount) * 72
  }));
}

function buildSmoothDistributionPath(points: CorridorDistributionPoint[]): string {
  if (points.length === 0) {
    return '';
  }

  const [first, ...rest] = points;
  let path = `M ${first.x.toFixed(2)} ${first.y.toFixed(2)}`;

  for (let index = 0; index < rest.length; index += 1) {
    const current = rest[index];
    const next = rest[index + 1];

    if (!next) {
      path += ` L ${current.x.toFixed(2)} ${current.y.toFixed(2)}`;
      continue;
    }

    const midX = (current.x + next.x) / 2;
    const midY = (current.y + next.y) / 2;
    path += ` Q ${current.x.toFixed(2)} ${current.y.toFixed(2)} ${midX.toFixed(2)} ${midY.toFixed(2)}`;
  }

  return path;
}

function pointRadius(): number {
  return 2.35;
}

function openProduct(point: PriceQualityPoint): void {
  if (!point.productRowId) {
    return;
  }

  selectedProduct.value = toParserProductListItem(point);
}

function closeProduct(): void {
  selectedProduct.value = null;
}

function clearActivePoint(): void {
  activePoint.value = null;
}

function openActiveProduct(): void {
  if (activePoint.value) {
    openProduct(activePoint.value);
  }
}

function handleChartPointerMove(event: MouseEvent): void {
  const canvas = chartCanvas.value;
  if (!canvas) {
    return;
  }

  if (pointerFrame !== null) {
    window.cancelAnimationFrame(pointerFrame);
  }

  const rect = canvas.getBoundingClientRect();
  const pointerX = ((event.clientX - rect.left) / rect.width) * chartWidth;
  const pointerY = ((event.clientY - rect.top) / rect.height) * chartHeight;

  pointerFrame = window.requestAnimationFrame(() => {
    pointerFrame = null;
    activePoint.value = findNearestPoint(pointerX, pointerY);
  });
}

function findNearestPoint(pointerX: number, pointerY: number): ChartPoint | null {
  const hitRadius = 8;
  const hitRadiusSquared = hitRadius * hitRadius;
  let nearest: ChartPoint | null = null;
  let nearestDistance = Number.POSITIVE_INFINITY;

  for (const point of chartPoints.value) {
    const distance = (point.x - pointerX) ** 2 + (point.y - pointerY) ** 2;
    if (distance <= hitRadiusSquared && distance < nearestDistance) {
      nearest = point;
      nearestDistance = distance;
    }
  }

  return nearest;
}

function drawChart(): void {
  const canvas = chartCanvas.value;
  if (!canvas) {
    return;
  }

  const context = canvas.getContext('2d');
  if (!context) {
    return;
  }

  const pixelRatio = window.devicePixelRatio || 1;
  const containerWidth = canvas.parentElement?.clientWidth ?? chartWidth;
  const cssWidth = Math.max(720, Math.round(containerWidth));
  const cssHeight = Math.round((cssWidth / chartWidth) * chartHeight);
  canvas.width = cssWidth * pixelRatio;
  canvas.height = cssHeight * pixelRatio;
  canvas.style.width = `${cssWidth}px`;
  canvas.style.height = `${cssHeight}px`;

  context.setTransform(
    pixelRatio * (cssWidth / chartWidth),
    0,
    0,
    pixelRatio * (cssHeight / chartHeight),
    0,
    0
  );
  context.clearRect(0, 0, chartWidth, chartHeight);
  drawChartFrame(context);

  for (const point of chartPoints.value) {
    drawChartPoint(context, point, activePoint.value?.wbProductId === point.wbProductId);
  }
}

function drawChartFrame(context: CanvasRenderingContext2D): void {
  const ratingTicks = [0, 1, 2, 3, 4, 5];
  const priceTicks = [0, 0.25, 0.5, 0.75, 1];
  const minPrice = priceRange.value.min ?? 0;
  const maxPrice = priceRange.value.max ?? minPrice;
  const priceSpan = maxPrice - minPrice;

  context.fillStyle = '#f8fafc';
  context.strokeStyle = '#d7dee8';
  context.lineWidth = 1;
  context.fillRect(plot.left, plot.top, plot.right - plot.left, plot.bottom - plot.top);
  context.strokeRect(plot.left, plot.top, plot.right - plot.left, plot.bottom - plot.top);

  context.save();
  context.globalAlpha = 0.72;
  context.fillStyle = '#f1f5f9';
  roundedRect(context, plot.left, 424, plot.right - plot.left, 66, 8);
  context.fill();
  context.restore();

  context.strokeStyle = '#d7dee8';
  context.lineWidth = 1;
  for (const rating of ratingTicks.slice(1)) {
    const y = plot.bottom - (rating / 5) * (plot.bottom - plot.top);
    line(context, plot.left, y, plot.right, y);
  }
  for (const step of priceTicks) {
    const x = plot.left + step * (plot.right - plot.left);
    line(context, x, plot.top, x, 490);
  }

  context.strokeStyle = '#64748b';
  context.lineWidth = 1.25;
  line(context, plot.left, plot.bottom, plot.right, plot.bottom);
  line(context, plot.left, plot.top, plot.left, 490);

  context.fillStyle = '#475569';
  context.font = '700 11px system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif';
  context.textBaseline = 'middle';
  context.textAlign = 'right';
  for (const rating of ratingTicks) {
    const y = plot.bottom - (rating / 5) * (plot.bottom - plot.top);
    context.fillText(rating === 0 ? '0' : formatRating(rating), plot.left - 12, y);
  }
  context.textAlign = 'left';
  context.fillText('Нет рейтинга', plot.left - 58, 466);

  context.textAlign = 'center';
  for (const step of priceTicks) {
    const x = plot.left + step * (plot.right - plot.left);
    const price = minPrice + priceSpan * step;
    context.fillText(formatMoney(price), x, 502);
  }

  context.font = '800 11px system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif';
  context.textAlign = 'center';
  context.fillText('Цена', (plot.left + plot.right) / 2, 526);
  context.save();
  context.translate(24, (plot.top + plot.bottom) / 2);
  context.rotate(-Math.PI / 2);
  context.fillText('Рейтинг WB', 0, 0);
  context.restore();
}

function drawChartPoint(context: CanvasRenderingContext2D, point: ChartPoint, active: boolean): void {
  context.beginPath();
  context.arc(point.x, point.y, active ? point.radius + 1.8 : point.radius, 0, Math.PI * 2);
  context.fillStyle = colorWithAlpha(pointColor(point.qualityBucket), active ? 0.82 : 0.46);
  context.fill();
  context.strokeStyle = active ? 'rgba(15, 23, 42, 0.55)' : 'rgba(255, 255, 255, 0.72)';
  context.lineWidth = active ? 1.4 : 0.6;
  context.stroke();
}

function line(context: CanvasRenderingContext2D, x1: number, y1: number, x2: number, y2: number): void {
  context.beginPath();
  context.moveTo(x1, y1);
  context.lineTo(x2, y2);
  context.stroke();
}

function roundedRect(
  context: CanvasRenderingContext2D,
  x: number,
  y: number,
  width: number,
  height: number,
  radius: number
): void {
  context.beginPath();
  context.moveTo(x + radius, y);
  context.lineTo(x + width - radius, y);
  context.quadraticCurveTo(x + width, y, x + width, y + radius);
  context.lineTo(x + width, y + height - radius);
  context.quadraticCurveTo(x + width, y + height, x + width - radius, y + height);
  context.lineTo(x + radius, y + height);
  context.quadraticCurveTo(x, y + height, x, y + height - radius);
  context.lineTo(x, y + radius);
  context.quadraticCurveTo(x, y, x + radius, y);
  context.closePath();
}

function colorWithAlpha(hex: string, alpha: number): string {
  const normalized = hex.replace('#', '');
  const red = Number.parseInt(normalized.slice(0, 2), 16);
  const green = Number.parseInt(normalized.slice(2, 4), 16);
  const blue = Number.parseInt(normalized.slice(4, 6), 16);
  return `rgba(${red}, ${green}, ${blue}, ${alpha})`;
}

function toParserProductListItem(point: PriceQualityPoint): ParserProductListItem {
  const current = intelligence.value;
  const observedAt = current?.observationWindow.latestObservedAtUtc ?? new Date().toISOString();

  return {
    id: point.productRowId!,
    parserRunId: current?.observationWindow.latestProductRunId ?? '',
    parsedAtUtc: observedAt,
    wbProductId: point.wbProductId,
    wbRootId: point.wbRootId,
    name: productTitle(point),
    brandName: point.brandName,
    sellerName: point.sellerName,
    priceRegular: null,
    priceDiscounted: point.price,
    priceWbWallet: null,
    discountPercent: null,
    totalQuantity: point.stock,
    ratingRounded: null,
    reviewRating: point.rating,
    feedbackCount: point.feedbackCount,
    sourceCategory: current?.context.sourceCategory ?? selectedContext.value.sourceCategory,
    sourceSubcategory: current?.context.sourceSubcategory ?? selectedContext.value.sourceSubcategory,
    sourceQuery: current?.context.query ?? selectedContext.value.query,
    thumbnailUrl: point.thumbnailUrl,
    rank: point.position === null ? null : {
      absolutePosition: point.position,
      page: Math.max(1, Math.ceil(point.position / 100)),
      positionOnPage: ((point.position - 1) % 100) + 1,
      query: current?.context.query ?? selectedContext.value.query,
      sourceCategory: current?.context.sourceCategory ?? selectedContext.value.sourceCategory,
      sourceSubcategory: current?.context.sourceSubcategory ?? selectedContext.value.sourceSubcategory,
      sourceRegionDest: current?.context.sourceRegionDest ?? defaultRegionDest,
      sort: current?.context.sort ?? defaultSort,
      observedAtUtc: observedAt,
      parserRunId: current?.observationWindow.latestRankRunId ?? '',
      rankContextId: current?.observationWindow.latestRankRunId ?? '',
      contextsCount: 1
    },
    position: {
      state: point.position === null ? 'unknown' : 'observed',
      absolutePosition: point.position,
      observedRangeLimit: current?.context.topN ?? legacyTopN,
      query: current?.context.query ?? selectedContext.value.query,
      sourceCategory: current?.context.sourceCategory ?? selectedContext.value.sourceCategory,
      sourceSubcategory: current?.context.sourceSubcategory ?? selectedContext.value.sourceSubcategory,
      observedAtUtc: point.position === null ? null : observedAt
    }
  };
}

function readInitialSubcategory(): string {
  const raw = readStringQuery('sourceSubcategory');
  return demoContexts.some((context) => context.sourceSubcategory === raw)
    ? raw!
    : demoContexts[0].sourceSubcategory;
}

function readStringQuery(key: string): string | null {
  const value = route.query[key];
  if (Array.isArray(value)) {
    return value[0] ?? null;
  }

  return typeof value === 'string' && value.trim() ? value : null;
}

function range(values: Array<number | null | undefined>): { min: number | null; max: number | null } {
  const numeric = values.filter((value): value is number => typeof value === 'number' && Number.isFinite(value));
  if (numeric.length === 0) {
    return { min: null, max: null };
  }

  return {
    min: Math.min(...numeric),
    max: Math.max(...numeric)
  };
}

function productTitle(point: PriceQualityPoint): string {
  return point.productName?.trim() || `WB ${point.wbProductId}`;
}

function brandSeller(point: PriceQualityPoint): string {
  return [point.brandName || 'Бренд не указан', point.sellerName || 'Продавец не указан'].join(' · ');
}

function qualityLabel(bucket: QualityBucket): string {
  return {
    strong: 'Сильная',
    medium: 'Средняя',
    weak: 'Слабая',
    unknown: 'Недостаточно данных'
  }[bucket];
}

function deliveryLabel(bucket: DeliveryBucket): string {
  return {
    fast: 'быстрая доставка',
    medium: 'средняя доставка',
    slow: 'долгая доставка',
    unknown: 'доставка не рассчитана'
  }[bucket];
}

function pointColor(bucket: QualityBucket): string {
  return {
    strong: '#008060',
    medium: '#CC8B08',
    weak: '#AC0E28',
    unknown: '#64748B'
  }[bucket];
}

function formatMoney(value: number | null | undefined): string {
  return typeof value === 'number'
    ? `${new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 0 }).format(value)} ₽`
    : '—';
}

function formatNumber(value: number | null | undefined): string {
  return typeof value === 'number'
    ? new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 0 }).format(value)
    : '—';
}

function formatRating(value: number | null | undefined): string {
  return typeof value === 'number'
    ? new Intl.NumberFormat('ru-RU', { minimumFractionDigits: 1, maximumFractionDigits: 1 }).format(value)
    : '—';
}

function formatPercent(value: number | null | undefined): string {
  return typeof value === 'number'
    ? `${new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 1 }).format(value)}%`
    : '—';
}

function formatConcentrationIndex(value: number | null | undefined): string {
  return typeof value === 'number'
    ? new Intl.NumberFormat('ru-RU', { minimumFractionDigits: 3, maximumFractionDigits: 3 }).format(value)
    : '—';
}

function concentrationBarStyle(value: number | null | undefined): string {
  const width = typeof value === 'number' ? Math.min(100, Math.max(0, value)) : 0;
  return `width: ${width}%`;
}

function corridorPosition(value: number | null | undefined): number {
  const data = priceCorridors.value;
  if (!data || typeof value !== 'number' || typeof data.min !== 'number' || typeof data.max !== 'number') {
    return 0;
  }

  const span = data.max - data.min;
  if (span <= 0) {
    return 0;
  }

  return Math.min(100, Math.max(0, ((value - data.min) / span) * 100));
}

function corridorMarkerLayoutStyle(marker: CorridorMarkerLayout): string {
  return `left: ${marker.left}%`;
}

function buildCorridorMarkerLayouts(): CorridorMarkerLayout[] {
  const data = priceCorridors.value;
  if (!data) {
    return [];
  }

  const markers: CorridorMarkerLayout[] = [
    {
      key: 'p25',
      label: 'P25',
      value: data.p25,
      left: corridorPosition(data.p25),
      level: 0,
      tone: 'boundary'
    },
    {
      key: 'median',
      label: 'Медиана',
      value: data.median,
      left: corridorPosition(data.median),
      level: 0,
      tone: 'median'
    },
    {
      key: 'p75',
      label: 'P75',
      value: data.p75,
      left: corridorPosition(data.p75),
      level: 0,
      tone: 'boundary'
    }
  ];

  const [p25, median, p75] = markers;
  const labelGapPercent = 20;
  const narrowMassSegment = Math.abs(p75.left - p25.left) < labelGapPercent * 1.6;
  const p25NearMedian = Math.abs(median.left - p25.left) < labelGapPercent;
  const p75NearMedian = Math.abs(p75.left - median.left) < labelGapPercent;

  if (p25NearMedian || narrowMassSegment) {
    p25.level = 1;
  }

  if (p75NearMedian || narrowMassSegment) {
    p75.level = narrowMassSegment && p25.level === 1 ? 2 : 1;
  }

  return markers;
}

function corridorDotStyle(dot: CorridorProductDot): string {
  return `left: ${dot.left}%`;
}

function corridorBoxStyle(): string {
  const left = corridorPosition(priceCorridors.value?.p25);
  const right = corridorPosition(priceCorridors.value?.p75);
  const width = Math.max(0, right - left);
  return `left: ${left}%; width: ${width}%; --corridor-box-center: ${left + width / 2}%`;
}

function corridorSegmentRange(fromPrice: number | null, toPrice: number | null): string {
  if (fromPrice === null && toPrice === null) {
    return '—';
  }

  if (fromPrice === null) {
    return `до ${formatMoney(toPrice)}`;
  }

  if (toPrice === null) {
    return `от ${formatMoney(fromPrice)}`;
  }

  return `${formatMoney(fromPrice)} – ${formatMoney(toPrice)}`;
}

function pointStyle(point: ChartPoint): string {
  return [
    `left: ${(point.x / chartWidth) * 100}%`,
    `top: ${(point.y / chartHeight) * 100}%`
  ].join(';');
}
</script>

<template>
  <div class="market-intelligence">
    <PageHeader
      title="Маркетинговая разведка"
      subtitle="Карта ниши показывает, как актуальные товары распределены по цене, рейтингу и качеству карточки."
    />

    <section class="mi-controls app-surface">
      <div class="mi-controls__fields">
        <label class="mi-field app-select-field">
          <span>Ниша</span>
          <select v-model="selectedSubcategory" class="app-select" @change="applySubcategorySelection">
            <option v-for="context in demoContexts" :key="context.sourceSubcategory" :value="context.sourceSubcategory">
              {{ context.label }}
            </option>
          </select>
        </label>

        <Button class="mi-refresh" variant="primary" :loading="loading" @click="refresh">Обновить карту</Button>
      </div>
    </section>

    <LoadingState v-if="loading && !intelligence" label="Строим карту ниши..." />

    <EmptyState v-else-if="error" title="Карта не загружена" :description="error" />

    <template v-else-if="intelligence">
      <section
        id="price-corridors"
        class="price-corridors app-surface app-operator-panel"
        :class="{ 'price-corridors--loading': loading }"
      >
        <div v-if="loading" class="price-corridors__loading" aria-live="polite">
          <span class="quality-map__loader" />
          <strong>Обновляем ценовые коридоры...</strong>
        </div>

        <header class="price-corridors__header">
          <div>
            <h2>
              <span>Ценовые коридоры</span>
              <HelpTooltip text="Коридоры показывают структуру цен всей выбранной ниши: нижний сегмент, массовый рынок, премиум и положение топа выдачи." />
            </h2>
            <p>
              {{
                priceCorridors?.insight
                  ?? 'Показываем структуру цен выбранной ниши, когда расчет доступен в ответе аналитики.'
              }}
            </p>
          </div>
          <div v-if="priceCorridors" class="price-corridors__stats">
            <span>Выборка: <b>{{ formatNumber(priceCorridors.sampleSize) }}</b></span>
          </div>
        </header>

        <EmptyState
          v-if="!priceCorridors"
          title="Ценовые коридоры пока недоступны"
          description="Обновите backend API до версии с расчетом ценовых коридоров или дождитесь ответа аналитики с этим блоком."
        />

        <EmptyState
          v-else-if="priceCorridors.sampleSize === 0"
          title="Ценовые коридоры не рассчитаны"
          :description="priceCorridors.limitations[0] ?? 'В выбранной нише нет товаров с валидной текущей ценой.'"
        />

        <div v-else class="price-corridors__body">
          <div class="price-corridors__plot" aria-label="Ценовой коридор ниши">
            <div class="corridor-axis">
              <svg
                v-if="corridorDistributionPath"
                class="corridor-distribution"
                viewBox="0 0 100 100"
                preserveAspectRatio="none"
                aria-hidden="true"
              >
                <path :d="corridorDistributionPath" />
              </svg>
              <span class="corridor-rug" aria-hidden="true">
                <span
                  v-for="dot in corridorProductDots"
                  :key="dot.key"
                  class="corridor-rug__dot"
                  :style="corridorDotStyle(dot)"
                />
              </span>
              <span class="corridor-axis__line" />
              <span class="corridor-axis__box" :style="corridorBoxStyle()">
                <span>Массовый сегмент</span>
              </span>
              <span
                v-for="marker in corridorMarkerLayouts"
                :key="marker.key"
                class="corridor-marker"
                :class="[
                  marker.tone === 'median' ? 'corridor-marker--median' : 'corridor-marker--boundary',
                  `corridor-marker--level-${marker.level}`
                ]"
                :style="corridorMarkerLayoutStyle(marker)"
              >
                <b>{{ marker.label }}</b>
                <em>{{ formatMoney(marker.value) }}</em>
              </span>
            </div>
            <div class="corridor-scale">
              <span>{{ formatMoney(priceCorridors.min) }}</span>
              <span />
              <span />
              <span>{{ formatMoney(priceCorridors.max) }}</span>
            </div>
            <div class="corridor-top-row" aria-label="Положение топа выдачи">
              <span v-for="marker in corridorTopMarkers" :key="marker.key">
                <b>{{ marker.label }}</b>
                {{ formatMoney(marker.value) }}
              </span>
            </div>
          </div>

          <table class="price-corridors__table">
            <thead>
              <tr>
                <th>Сегмент</th>
                <th>Диапазон цены</th>
                <th>Товаров</th>
                <th>Медианный рейтинг</th>
                <th>Доля</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="segment in priceCorridors.segments" :key="segment.key">
                <td>{{ segment.title }}</td>
                <td>{{ corridorSegmentRange(segment.fromPrice, segment.toPrice) }}</td>
                <td>{{ formatNumber(segment.productsCount) }}</td>
                <td>{{ formatRating(segment.medianRating) }}</td>
                <td>{{ formatPercent(segment.sharePercent) }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>

      <section id="price-quality" class="quality-map app-surface app-operator-panel" :class="{ 'quality-map--loading': loading }">
        <div v-if="loading" class="quality-map__loading" aria-live="polite">
          <span class="quality-map__loader" />
          <strong>Обновляем карту ниши...</strong>
        </div>

        <header class="quality-map__header">
          <div>
            <h2>
              <span>Карта цены и качества</span>
              <HelpTooltip text="X — цена товара, Y — рейтинг WB. Все точки одинакового размера, цвет показывает качество карточки." />
            </h2>
            <p>{{ intelligence.priceQualityMap.summary.insight }}</p>
          </div>
          <div class="quality-map__legend" aria-label="Легенда качества">
            <span
              v-for="item in chartLegend"
              :key="item.bucket"
              class="legend-item"
              :class="`legend-item--${item.bucket}`"
            >
              {{ item.label }} · {{ formatNumber(item.count) }}
            </span>
          </div>
        </header>

        <EmptyState
          v-if="chartPoints.length === 0"
          title="Нет точек для карты"
          description="Для выбранной ниши пока нет подходящих товаров в проверенной выдаче."
        />

        <div v-else class="chart-shell">
          <canvas
            ref="chartCanvas"
            class="quality-chart-canvas"
            :width="chartWidth"
            :height="chartHeight"
            role="img"
            aria-label="Карта цены и качества"
            @mousemove="handleChartPointerMove"
            @mouseleave="clearActivePoint"
            @click="openActiveProduct"
          />

          <article v-if="activePoint" class="chart-tooltip app-operator-card" :style="pointStyle(activePoint)">
            <div class="chart-tooltip__media">
              <MarketProductImage :src="activePoint.thumbnailUrl" :alt="productTitle(activePoint)" />
            </div>
            <div class="chart-tooltip__body">
              <span>WB {{ activePoint.wbProductId }} · {{ activePoint.position !== null ? `#${activePoint.position}` : 'позиция не указана' }}</span>
              <strong>{{ productTitle(activePoint) }}</strong>
              <p>{{ brandSeller(activePoint) }}</p>
              <div class="chart-tooltip__metrics">
                <span>Цена: <b>{{ formatMoney(activePoint.price) }}</b></span>
                <span>Рейтинг: <b>{{ formatRating(activePoint.rating) }}</b></span>
                <span>Отзывы: <b>{{ formatNumber(activePoint.feedbackCount) }}</b></span>
                <span>Остаток: <b>{{ formatNumber(activePoint.stock) }}</b></span>
              </div>
              <div class="chart-tooltip__badges">
                <Badge :tone="activePoint.qualityBucket === 'strong' ? 'success' : activePoint.qualityBucket === 'weak' ? 'danger' : 'info'">
                  {{ qualityLabel(activePoint.qualityBucket) }}
                </Badge>
                <Badge tone="neutral">{{ deliveryLabel(activePoint.deliveryBucket) }}</Badge>
              </div>
              <ul v-if="activePoint.qualityReasons.length">
                <li v-for="reason in activePoint.qualityReasons.slice(0, 3)" :key="reason">{{ reason }}</li>
              </ul>
            </div>
          </article>
        </div>

        <div class="quality-map__notes">
          <p v-if="intelligence.priceQualityMap.limitations.length">
            {{ intelligence.priceQualityMap.limitations[0] }}
          </p>
          <p>
            Дорогие сильные точки показывают верхний ориентир ниши. Дешевые слабые точки помогают увидеть сегмент, где конкуренты выигрывают ценой, но проигрывают карточкой.
          </p>
        </div>
      </section>

      <section
        id="market-concentration"
        class="market-concentration app-surface app-operator-panel"
        :class="{ 'market-concentration--loading': loading }"
      >
        <div v-if="loading" class="market-concentration__loading" aria-live="polite">
          <span class="quality-map__loader" />
          <strong>Обновляем концентрацию рынка...</strong>
        </div>

        <header class="market-concentration__header">
          <div>
            <h2>
              <span>Концентрация рынка</span>
              <HelpTooltip text="Показывает, насколько topN выбранной ниши занят крупными продавцами, брендами и повторяющимися root-группами." />
            </h2>
            <p>
              {{
                marketConcentration?.insight
                  ?? 'Показываем, фрагментирована ли ниша или значительная часть топа занята несколькими игроками.'
              }}
            </p>
          </div>
          <div v-if="marketConcentration" class="market-concentration__stats">
            <span>Выборка: <b>{{ formatNumber(marketConcentration.sampleSize) }}</b></span>
            <span>HHI: <b>{{ formatConcentrationIndex(marketConcentration.hhi) }}</b></span>
          </div>
        </header>

        <EmptyState
          v-if="!marketConcentration"
          title="Концентрация рынка пока недоступна"
          description="Обновите backend API до версии с расчетом концентрации рынка."
        />

        <EmptyState
          v-else-if="marketConcentration.sampleSize === 0"
          title="Концентрация рынка не рассчитана"
          :description="marketConcentration.limitations[0] ?? 'В выбранном topN нет товаров для расчета концентрации.'"
        />

        <div v-else class="market-concentration__body">
          <div class="market-concentration__metrics">
            <article v-for="metric in concentrationMetrics" :key="metric.label" class="market-concentration__metric">
              <span>{{ metric.label }}</span>
              <strong>{{ metric.value }}</strong>
              <p>{{ metric.detail }}</p>
            </article>
          </div>

          <div class="market-concentration__grid">
            <section class="market-concentration__chart" aria-label="Доля топ продавцов">
              <h3>Топ продавцов</h3>
              <div v-if="marketConcentration.sellerLeaders.length" class="concentration-bars">
                <div
                  v-for="seller in marketConcentration.sellerLeaders.slice(0, 8)"
                  :key="seller.name"
                  class="concentration-bar"
                >
                  <div class="concentration-bar__label">
                    <strong>{{ seller.name }}</strong>
                    <span>{{ formatPercent(seller.sharePercent) }} · #{{ seller.bestPosition }}</span>
                  </div>
                  <div class="concentration-bar__track">
                    <span :style="concentrationBarStyle(seller.sharePercent)" />
                  </div>
                </div>
              </div>
              <EmptyState
                v-else
                title="Нет данных о продавцах"
                description="В выбранной выдаче нет seller-данных для диаграммы."
              />
            </section>

            <section class="market-concentration__tables">
              <table class="market-concentration__table">
                <caption>Топ продавцов</caption>
                <thead>
                  <tr>
                    <th>Продавец</th>
                    <th>Мест</th>
                    <th>Доля</th>
                    <th>Лучшая позиция</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="seller in marketConcentration.sellerLeaders" :key="seller.name">
                    <td>{{ seller.name }}</td>
                    <td>{{ formatNumber(seller.slotsCount) }}</td>
                    <td>{{ formatPercent(seller.sharePercent) }}</td>
                    <td>#{{ seller.bestPosition }}</td>
                  </tr>
                </tbody>
              </table>

              <table class="market-concentration__table">
                <caption>Топ брендов</caption>
                <thead>
                  <tr>
                    <th>Бренд</th>
                    <th>Мест</th>
                    <th>Доля</th>
                    <th>Лучшая позиция</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="brand in marketConcentration.brandLeaders" :key="brand.name">
                    <td>{{ brand.name }}</td>
                    <td>{{ formatNumber(brand.slotsCount) }}</td>
                    <td>{{ formatPercent(brand.sharePercent) }}</td>
                    <td>#{{ brand.bestPosition }}</td>
                  </tr>
                </tbody>
              </table>

              <table class="market-concentration__table">
                <caption>Повторяющиеся root-группы</caption>
                <thead>
                  <tr>
                    <th>WB root</th>
                    <th>Карточек</th>
                    <th>Доля</th>
                    <th>Лучшая позиция</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="cluster in marketConcentration.rootClusters" :key="cluster.wbRootId">
                    <td>{{ cluster.wbRootId }}</td>
                    <td>{{ formatNumber(cluster.productCount) }}</td>
                    <td>{{ formatPercent(cluster.sharePercent) }}</td>
                    <td>#{{ cluster.bestPosition }}</td>
                  </tr>
                </tbody>
              </table>
            </section>
          </div>
        </div>
      </section>
    </template>

    <MarketProductDetailDrawer
      :open="Boolean(selectedProduct)"
      :product="selectedProduct"
      @close="closeProduct"
    />
  </div>
</template>

<style scoped>
.market-intelligence {
  display: grid;
  gap: var(--space-4);
}

.mi-controls,
.quality-map,
.market-concentration {
  border-color: var(--accent-ember-border);
}

.mi-controls {
  padding: var(--space-4);
}

.mi-controls__fields {
  display: grid;
  grid-template-columns: minmax(16rem, 1fr) auto;
  gap: var(--space-3);
  align-items: start;
}

.mi-field {
  display: grid;
  gap: var(--space-1);
}

.mi-field small,
.quality-map__header p,
.quality-map__notes,
.chart-tooltip__body p,
.chart-tooltip__body span,
.chart-tooltip__body li {
  color: var(--text-muted);
}

.mi-refresh {
  align-self: end;
}

.price-corridors {
  position: relative;
  display: grid;
  gap: var(--space-4);
  border-color: var(--accent-ember-border);
  scroll-margin-top: var(--space-4);
  padding: var(--space-4);
  overflow: hidden;
}

.price-corridors--loading .price-corridors__body,
.price-corridors--loading .price-corridors__header {
  pointer-events: none;
  opacity: 0.28;
}

.price-corridors__loading {
  position: absolute;
  inset: 0;
  z-index: 5;
  display: grid;
  gap: var(--space-2);
  place-items: center;
  align-content: center;
  background: color-mix(in srgb, var(--surface-panel) 78%, transparent);
  backdrop-filter: blur(1px);
  color: var(--text-primary);
  font-size: 0.92rem;
  font-weight: 800;
}

.price-corridors__header {
  display: flex;
  justify-content: space-between;
  gap: var(--space-4);
  align-items: flex-start;
}

.price-corridors__header h2 {
  display: flex;
  align-items: center;
  gap: var(--space-1);
  margin: 0 0 var(--space-1);
  font-size: 1.1rem;
}

.price-corridors__header p {
  margin: 0;
  color: var(--text-muted);
}

.price-corridors__stats {
  display: flex;
  flex-wrap: wrap;
  justify-content: flex-end;
  gap: var(--space-2);
  color: var(--text-muted);
  font-size: 0.82rem;
  font-weight: 700;
}

.price-corridors__stats span {
  border: 1px solid var(--border-subtle);
  border-radius: 6px;
  padding: 0.35rem 0.55rem;
  background: var(--surface-raised);
}

.price-corridors__body {
  display: grid;
  grid-template-columns: minmax(23rem, 0.78fr) minmax(27rem, 1fr);
  gap: var(--space-4);
  align-items: start;
}

.price-corridors__plot {
  display: grid;
  gap: var(--space-2);
  min-height: 17rem;
  padding: var(--space-4);
  border: 1px solid var(--border-subtle);
  border-radius: 8px;
  background: color-mix(in srgb, var(--surface-panel) 88%, var(--surface-raised));
}

.corridor-axis {
  position: relative;
  height: 12.25rem;
  margin: 1rem 1.5rem 0;
}

.corridor-distribution {
  position: absolute;
  top: 0.35rem;
  right: 0;
  left: 0;
  z-index: 1;
  width: 100%;
  height: 3.25rem;
  overflow: visible;
}

.corridor-distribution path {
  fill: none;
  stroke: color-mix(in srgb, var(--accent-ember) 74%, var(--text-muted));
  stroke-linecap: round;
  stroke-linejoin: round;
  stroke-width: 2.2;
  vector-effect: non-scaling-stroke;
  opacity: 0.78;
}

.corridor-rug {
  position: absolute;
  top: 4.15rem;
  right: 0;
  left: 0;
  z-index: 2;
  height: 0.35rem;
  border-radius: 6px;
  background: transparent;
}

.corridor-rug__dot {
  position: absolute;
  width: 3px;
  height: 3px;
  border-radius: 999px;
  opacity: 0.5;
  background: color-mix(in srgb, var(--text-muted) 82%, transparent);
  top: 50%;
  transform: translate(-50%, -50%);
}

.corridor-axis__line {
  position: absolute;
  top: 4.15rem;
  right: 0;
  left: 0;
  z-index: 1;
  height: 0.35rem;
  border-radius: 999px;
  background: var(--border-subtle);
}

.corridor-axis__box {
  position: absolute;
  top: 3.4rem;
  z-index: 3;
  height: 1.85rem;
  border: 1px solid color-mix(in srgb, var(--accent-ember) 45%, var(--border-subtle));
  border-radius: 6px;
  background: color-mix(in srgb, var(--accent-ember) 16%, var(--surface-raised));
  display: grid;
  place-items: center;
  color: var(--accent-ember);
  font-size: 0.68rem;
  font-weight: 900;
  text-transform: uppercase;
  white-space: nowrap;
}

.corridor-axis__box span {
  position: absolute;
  top: -1.15rem;
  left: 50%;
  color: var(--accent-ember);
  transform: translateX(-50%);
}

.corridor-marker {
  position: absolute;
  top: 6.05rem;
  display: grid;
  gap: 0.1rem;
  min-width: 4.75rem;
  color: var(--text-muted);
  font-size: 0.72rem;
  font-weight: 800;
  text-align: center;
  transform: translateX(-50%);
}

.corridor-marker::after {
  content: '';
  position: absolute;
  top: -0.75rem;
  left: 50%;
  width: 2px;
  height: 0.7rem;
  border-radius: 999px;
  background: currentColor;
  transform: translateX(-50%);
}

.corridor-marker--level-0 {
  top: 6.05rem;
}

.corridor-marker--level-1 {
  top: 8.45rem;
}

.corridor-marker--level-2 {
  top: 10.85rem;
}

.corridor-marker--level-1::after {
  top: -4.25rem;
  height: 4.2rem;
}

.corridor-marker--level-2::after {
  top: -6.65rem;
  height: 6.6rem;
}

.corridor-marker em {
  color: var(--text-primary);
  font-style: normal;
}

.corridor-marker--median {
  color: var(--accent-ember);
}

.corridor-marker--boundary {
  color: var(--text-muted);
}

.corridor-marker--boundary em {
  color: var(--text-muted);
}

.corridor-scale {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: var(--space-2);
  color: var(--text-muted);
  font-size: 0.78rem;
  font-weight: 800;
}

.corridor-scale span:nth-child(2) {
  text-align: center;
}

.corridor-scale span:nth-child(3) {
  text-align: center;
}

.corridor-scale span:last-child {
  text-align: right;
}

.corridor-top-row {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
  color: var(--text-muted);
  font-size: 0.78rem;
  font-weight: 800;
}

.corridor-top-row span {
  display: inline-flex;
  gap: 0.35rem;
  align-items: center;
  border: 1px solid color-mix(in srgb, #008060 25%, var(--border-subtle));
  border-radius: 6px;
  padding: 0.3rem 0.45rem;
  background: color-mix(in srgb, #008060 7%, var(--surface-panel));
}

.corridor-top-row b {
  color: #008060;
}

.price-corridors__table {
  width: 100%;
  border-collapse: collapse;
  overflow: hidden;
  border: 1px solid var(--border-subtle);
  border-radius: 8px;
  background: var(--surface-panel);
  font-size: 0.84rem;
}

.price-corridors__table th,
.price-corridors__table td {
  border-bottom: 1px solid var(--border-subtle);
  padding: 0.68rem 0.72rem;
  text-align: left;
}

.price-corridors__table th {
  color: var(--text-muted);
  font-size: 0.72rem;
  font-weight: 900;
  text-transform: uppercase;
}

.price-corridors__table td {
  color: var(--text-primary);
  font-weight: 700;
}

.price-corridors__table tr:last-child td {
  border-bottom: 0;
}

.quality-map {
  position: relative;
  display: grid;
  gap: var(--space-4);
  scroll-margin-top: var(--space-4);
  padding: var(--space-4);
}

.quality-map--loading .chart-shell {
  pointer-events: none;
}

.quality-map__loading {
  position: absolute;
  inset: 0;
  z-index: 5;
  display: grid;
  gap: var(--space-2);
  place-items: center;
  align-content: start;
  border-radius: 8px;
  box-sizing: border-box;
  padding-top: min(9rem, 22vh);
  background: color-mix(in srgb, var(--surface-panel) 78%, transparent);
  backdrop-filter: blur(1px);
  color: var(--text-primary);
  font-size: 0.92rem;
  font-weight: 800;
}

.quality-map__loader {
  width: 2rem;
  height: 2rem;
  border: 3px solid color-mix(in srgb, var(--accent-ember) 24%, transparent);
  border-top-color: var(--accent-ember);
  border-radius: 999px;
  animation: quality-map-spin 0.75s linear infinite;
}

@keyframes quality-map-spin {
  to {
    transform: rotate(360deg);
  }
}

.quality-map__header {
  display: flex;
  justify-content: space-between;
  gap: var(--space-4);
  align-items: flex-start;
}

.quality-map__header h2 {
  display: flex;
  align-items: center;
  gap: var(--space-1);
  margin: 0 0 var(--space-1);
  font-size: 1.1rem;
}

.quality-map__header p,
.quality-map__notes p {
  margin: 0;
}

.quality-map__legend {
  display: flex;
  flex-wrap: wrap;
  justify-content: flex-end;
  gap: var(--space-2);
}

.legend-item {
  display: inline-flex;
  align-items: center;
  gap: var(--space-1);
  border: 1px solid var(--border-subtle);
  border-radius: 6px;
  padding: 0.35rem 0.55rem;
  font-size: 0.78rem;
  font-weight: 800;
  background: var(--surface-raised);
}

.legend-item::before {
  content: '';
  width: 0.6rem;
  height: 0.6rem;
  border-radius: 999px;
  background: var(--text-muted);
}

.legend-item--strong::before {
  background: #008060;
}

.legend-item--medium::before {
  background: #cc8b08;
}

.legend-item--weak::before {
  background: #ac0e28;
}

.legend-item--unknown::before {
  background: #64748b;
}

.chart-shell {
  position: relative;
  min-height: 28rem;
  overflow: visible;
}

.quality-chart-canvas {
  display: block;
  width: 100%;
  min-height: 28rem;
  border-radius: 8px;
  outline: none;
}

.chart-tooltip {
  position: absolute;
  z-index: 4;
  display: grid;
  grid-template-columns: 5rem minmax(12rem, 1fr);
  gap: var(--space-3);
  width: min(26rem, calc(100vw - 4rem));
  padding: var(--space-3);
  pointer-events: none;
  transform: translate(-50%, calc(-100% - 0.75rem));
  box-shadow: var(--shadow-lg);
}

.chart-tooltip__media {
  width: 5rem;
  aspect-ratio: 4 / 5;
  overflow: hidden;
  border: 1px solid var(--border-subtle);
  border-radius: 6px;
  background: var(--surface-raised);
}

.chart-tooltip__body {
  display: grid;
  gap: var(--space-2);
}

.chart-tooltip__body strong {
  color: var(--text-primary);
}

.chart-tooltip__body p,
.chart-tooltip__body ul {
  margin: 0;
}

.chart-tooltip__body ul {
  padding-left: 1rem;
}

.chart-tooltip__metrics {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0.35rem 0.75rem;
}

.chart-tooltip__badges {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-1);
}

.quality-map__notes {
  display: grid;
  gap: var(--space-2);
  border-top: 1px solid var(--border-subtle);
  padding-top: var(--space-3);
}

.market-concentration {
  position: relative;
  display: grid;
  gap: var(--space-4);
  scroll-margin-top: var(--space-4);
  padding: var(--space-4);
  overflow: hidden;
}

.market-concentration--loading .market-concentration__body,
.market-concentration--loading .market-concentration__header {
  pointer-events: none;
  opacity: 0.28;
}

.market-concentration__loading {
  position: absolute;
  inset: 0;
  z-index: 5;
  display: grid;
  gap: var(--space-2);
  place-items: center;
  align-content: center;
  background: color-mix(in srgb, var(--surface-panel) 78%, transparent);
  backdrop-filter: blur(1px);
  color: var(--text-primary);
  font-size: 0.92rem;
  font-weight: 800;
}

.market-concentration__header {
  display: flex;
  justify-content: space-between;
  gap: var(--space-4);
  align-items: flex-start;
}

.market-concentration__header h2 {
  display: flex;
  align-items: center;
  gap: var(--space-1);
  margin: 0 0 var(--space-1);
  font-size: 1.1rem;
}

.market-concentration__header p {
  margin: 0;
  color: var(--text-muted);
}

.market-concentration__stats {
  display: flex;
  flex-wrap: wrap;
  justify-content: flex-end;
  gap: var(--space-2);
  color: var(--text-muted);
  font-size: 0.82rem;
  font-weight: 700;
}

.market-concentration__stats span {
  border: 1px solid var(--border-subtle);
  border-radius: 6px;
  padding: 0.35rem 0.55rem;
  background: var(--surface-raised);
}

.market-concentration__body {
  display: grid;
  gap: var(--space-4);
}

.market-concentration__metrics {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: var(--space-3);
}

.market-concentration__metric {
  border: 1px solid var(--border-subtle);
  border-radius: 8px;
  background: var(--surface-panel);
  padding: var(--space-3);
}

.market-concentration__metric span {
  color: var(--text-muted);
  font-size: 0.72rem;
  font-weight: 900;
  text-transform: uppercase;
}

.market-concentration__metric strong {
  display: block;
  margin-top: var(--space-1);
  color: var(--text-primary);
  font-size: 1.25rem;
}

.market-concentration__metric p {
  margin: var(--space-1) 0 0;
  color: var(--text-muted);
}

.market-concentration__grid {
  display: grid;
  grid-template-columns: minmax(22rem, 0.65fr) minmax(30rem, 1fr);
  gap: var(--space-4);
  align-items: start;
}

.market-concentration__chart,
.market-concentration__tables {
  display: grid;
  gap: var(--space-3);
}

.market-concentration__chart {
  border: 1px solid var(--border-subtle);
  border-radius: 8px;
  background: var(--surface-panel);
  padding: var(--space-4);
}

.market-concentration__chart h3 {
  margin: 0;
  font-size: 1rem;
}

.concentration-bars {
  display: grid;
  gap: var(--space-3);
}

.concentration-bar {
  display: grid;
  gap: var(--space-1);
}

.concentration-bar__label {
  display: flex;
  justify-content: space-between;
  gap: var(--space-2);
  color: var(--text-muted);
  font-size: 0.78rem;
  font-weight: 800;
}

.concentration-bar__label strong {
  min-width: 0;
  overflow: hidden;
  color: var(--text-primary);
  text-overflow: ellipsis;
  white-space: nowrap;
}

.concentration-bar__track {
  height: 0.7rem;
  overflow: hidden;
  border-radius: 999px;
  background: color-mix(in srgb, var(--border-subtle) 70%, transparent);
}

.concentration-bar__track span {
  display: block;
  height: 100%;
  min-width: 0.2rem;
  border-radius: inherit;
  background: var(--accent-ember);
}

.market-concentration__table {
  width: 100%;
  border-collapse: collapse;
  overflow: hidden;
  border: 1px solid var(--border-subtle);
  border-radius: 8px;
  background: var(--surface-panel);
  font-size: 0.84rem;
}

.market-concentration__table caption {
  padding: 0 0 var(--space-2);
  color: var(--text-primary);
  font-weight: 900;
  text-align: left;
}

.market-concentration__table th,
.market-concentration__table td {
  border-bottom: 1px solid var(--border-subtle);
  padding: 0.68rem 0.72rem;
  text-align: left;
}

.market-concentration__table th {
  color: var(--text-muted);
  font-size: 0.72rem;
  font-weight: 900;
  text-transform: uppercase;
}

.market-concentration__table td {
  color: var(--text-primary);
  font-weight: 700;
}

.market-concentration__table tr:last-child td {
  border-bottom: 0;
}

@media (max-width: 960px) {
  .mi-controls__fields {
    grid-template-columns: 1fr;
  }

  .quality-map__header {
    display: grid;
  }

  .price-corridors__header,
  .price-corridors__body,
  .market-concentration__header,
  .market-concentration__grid {
    display: grid;
    grid-template-columns: 1fr;
  }

  .price-corridors__stats,
  .market-concentration__stats {
    justify-content: flex-start;
  }

  .market-concentration__metrics {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .quality-map__legend {
    justify-content: flex-start;
  }

  .chart-tooltip {
    grid-template-columns: 1fr;
    width: min(20rem, calc(100vw - 2rem));
  }
}

@media (max-width: 640px) {
  .market-concentration__metrics {
    grid-template-columns: 1fr;
  }
}
</style>
