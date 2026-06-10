<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, ref, type ComponentPublicInstance } from 'vue';

import HelpTooltip from '@/shared/ui/HelpTooltip.vue';

import MarketProductImage from './MarketProductImage.vue';
import type {
  HotProductRecommendationFactor,
  HotProductRecommendationItem,
  HotProductsListResponse
} from './hotProductsRecommendations.types';

const props = defineProps<{
  response: HotProductsListResponse | null;
  loading: boolean;
  error: string;
}>();

const emit = defineEmits<{
  locate: [wbProductId: string];
}>();

const displayLimit = 5;
const fireStepMs = 100;
const fireLogicalRows = 24;
const fireBaseZoneRatio = 0.34;
const fireMiddleZoneRatio = 0.67;
const expandedId = ref<string | null>(null);
const activeFireId = ref<string | null>(null);
const activeFireText = ref('');
const activeFireDiagnostics = ref<FireDiagnostics | null>(null);
const items = computed(() => pickVisibleItems(props.response?.items ?? []));
const hasItems = computed(() => items.value.length > 0);

const cardSizes = new Map<string, { width: number; height: number }>();
const observedCards = new Map<string, HTMLElement>();
const observedCardIds = new WeakMap<Element, string>();
const reducedMotionQuery =
  typeof window === 'undefined' ? null : window.matchMedia('(prefers-reduced-motion: reduce)');
const fireDebugEnabled = isFireDebugEnabled();

let resizeObserver: ResizeObserver | null = null;
let fireAnimationFrameId: number | null = null;
let fireLastTime = 0;
let fireAccumulator = 0;
let fireSimulation: FireSimulation | null = null;

type FireTierParams = {
  bottomHeat: number;
  bottomRows: number;
  baseHeat: number;
  baseCoverage: number;
  baseConnectivity: number;
  sourceCountBonus: number;
  decay: number;
  sparkRate: number;
  maxOccupancy: number;
  spread: number;
};

type FireSpark = {
  x: number;
  y: number;
  ttl: number;
  speed: number;
};

type FireTierName = 'priority' | 'strong' | 'steady';

type FireSimulation = {
  id: string;
  wbProductId: string | null;
  score: number;
  tier: FireTierName;
  columns: number;
  rows: number;
  heat: Float32Array;
  nextHeat: Float32Array;
  sourceCenters: number[];
  sparks: FireSpark[];
  heatCenterHistory: number[];
  lastDiagnosticAt: number;
  frame: number;
  seed: number;
  params: FireTierParams;
};

type FireRect = {
  x: number;
  y: number;
  width: number;
  height: number;
};

type FireFrameStats = {
  density: number;
  bottomDensity: number;
  middleDensity: number;
  topDensity: number;
  bottomCoverageRatio: number;
  emptyRowCount: number;
  maxFrameWidth: number;
  topSparkCount: number;
  bottomConnectedRun: number;
  bottomSegments: number;
  heatCenterY: number | null;
  charCounts: {
    X: number;
    x: number;
    dash: number;
    dot: number;
    plus: number;
  };
};

type FireDiagnostics = FireFrameStats & {
  cardId: string;
  wbProductId: string | null;
  score: number;
  tier: FireTierName;
  columns: number;
  rows: number;
  frameRows: number;
  sparkCount: number;
  cardWidth: number;
  fireLayerWidth: number;
  mainRect: FireRect;
  layerRect: FireRect;
  layerInsideMain: boolean;
  layerCoversMain: boolean;
  heatCenterHistory: number[];
  isMovingUp: boolean;
  seededBottomRows: number;
  baseCoverage: number;
  logicalRows: number;
  logicalColumns: number;
  tierColor: string;
};

function pickVisibleItems(allItems: HotProductRecommendationItem[]): HotProductRecommendationItem[] {
  if (allItems.length <= displayLimit) {
    return allItems;
  }

  const picked: HotProductRecommendationItem[] = [];
  const pickedIds = new Set<string>();
  const seenGroups = new Set<string>();

  for (const item of allItems) {
    const group = `${item.sourceCategory ?? ''}|${item.sourceSubcategory ?? ''}`.toLocaleLowerCase('ru-RU');
    if (group.trim() && !seenGroups.has(group)) {
      picked.push(item);
      pickedIds.add(item.id);
      seenGroups.add(group);
    }

    if (picked.length === displayLimit) {
      return picked;
    }
  }

  for (const item of allItems) {
    if (!pickedIds.has(item.id)) {
      picked.push(item);
    }

    if (picked.length === displayLimit) {
      return picked;
    }
  }

  return picked;
}

function toggleExplanation(item: HotProductRecommendationItem) {
  expandedId.value = expandedId.value === item.id ? null : item.id;
}

function isFireDebugEnabled(): boolean {
  if (typeof window === 'undefined') {
    return false;
  }

  try {
    const params = new URLSearchParams(window.location.search);
    return params.get('debugFire') === '1' || window.localStorage.getItem('ashmesDebugFire') === '1';
  } catch {
    return false;
  }
}

function setCardMainRef(id: string, element: Element | ComponentPublicInstance | null) {
  const htmlElement = element instanceof HTMLElement ? element : null;
  const previous = observedCards.get(id);

  if (previous && previous !== htmlElement) {
    resizeObserver?.unobserve(previous);
    observedCardIds.delete(previous);
    observedCards.delete(id);
  }

  if (!htmlElement) {
    cardSizes.delete(id);
    return;
  }

  observedCards.set(id, htmlElement);
  observedCardIds.set(htmlElement, id);
  cardSizes.set(id, { width: htmlElement.clientWidth, height: htmlElement.clientHeight });

  if (typeof ResizeObserver !== 'undefined') {
    if (!resizeObserver) {
      resizeObserver = new ResizeObserver((entries) => {
        for (const entry of entries) {
          const entryId = observedCardIds.get(entry.target);
          if (!entryId) {
            continue;
          }

          cardSizes.set(entryId, {
            width: entry.contentRect.width,
            height: entry.contentRect.height
          });
        }
      });
    }

    resizeObserver.observe(htmlElement);
  }
}

function startFire(item: HotProductRecommendationItem) {
  if (activeFireId.value === item.id && fireSimulation?.id === item.id) {
    return;
  }

  stopFireAnimation();
  activeFireDiagnostics.value = null;
  activeFireId.value = item.id;
  fireSimulation = createFireSimulation(item);
  warmUpFireSimulation(fireSimulation);
  activeFireText.value = renderFireSimulation(fireSimulation);
  updateFireDiagnosticsAfterRender(fireSimulation, activeFireText.value);

  if (reducedMotionQuery?.matches) {
    return;
  }

  startFireAnimation();
}

function stopFire(item: HotProductRecommendationItem, event?: FocusEvent | PointerEvent | MouseEvent) {
  if (event?.currentTarget instanceof HTMLElement && event.relatedTarget instanceof Node) {
    if (event.currentTarget.contains(event.relatedTarget)) {
      return;
    }
  }

  if (activeFireId.value !== item.id) {
    return;
  }

  stopFireAnimation();
  activeFireId.value = null;
  activeFireText.value = '';
  activeFireDiagnostics.value = null;
  fireSimulation = null;
}

function startFireAnimation() {
  if (fireAnimationFrameId !== null || typeof window === 'undefined') {
    return;
  }

  fireLastTime = 0;
  fireAccumulator = 0;
  fireAnimationFrameId = window.requestAnimationFrame(runFireAnimation);
}

function stopFireAnimation() {
  if (fireAnimationFrameId !== null && typeof window !== 'undefined') {
    window.cancelAnimationFrame(fireAnimationFrameId);
  }

  fireAnimationFrameId = null;
  fireLastTime = 0;
  fireAccumulator = 0;
}

function runFireAnimation(timestamp: number) {
  if (!fireSimulation || !activeFireId.value) {
    stopFireAnimation();
    return;
  }

  if (!fireLastTime) {
    fireLastTime = timestamp;
  }

  const elapsed = Math.min(250, timestamp - fireLastTime);
  fireLastTime = timestamp;
  fireAccumulator += elapsed;

  let shouldRender = false;
  while (fireAccumulator >= fireStepMs) {
    stepFireSimulation(fireSimulation, true);
    fireAccumulator -= fireStepMs;
    shouldRender = true;
  }

  if (shouldRender) {
    const fireText = renderFireSimulation(fireSimulation);
    activeFireText.value = fireText;
    updateFireDiagnostics(fireSimulation, fireText);
  }

  fireAnimationFrameId = window.requestAnimationFrame(runFireAnimation);
}

function createFireSimulation(item: HotProductRecommendationItem): FireSimulation {
  const size = getFireLayerSize(item.id);
  const columns = clamp(Math.floor(size.width / 4.4), 96, 280);
  const rows = fireLogicalRows;
  const params = fireTier(item.score);
  const tier = scoreTier(item.score);
  const sourceCount = clamp(Math.round(columns / 42) + params.sourceCountBonus, 3, 7);
  const seed = hashString(item.id);
  const sourceCenters = Array.from({ length: sourceCount }, (_, index) => {
    const slot = (index + 0.6) / sourceCount;
    const offset = (noise(index * 19, seed, 0) - 0.5) * 0.16;
    return clamp((slot + offset) * columns, 0, columns - 1);
  });
  if (columns >= 120) {
    sourceCenters[sourceCenters.length - 1] = clamp(columns * (0.86 + (noise(seed, sourceCount, 1) - 0.5) * 0.04), 0, columns - 1);
  }

  return {
    id: item.id,
    wbProductId: item.wbProductId,
    score: item.score,
    tier,
    columns,
    rows,
    heat: new Float32Array(columns * rows),
    nextHeat: new Float32Array(columns * rows),
    sourceCenters,
    sparks: [],
    heatCenterHistory: [],
    lastDiagnosticAt: 0,
    frame: 0,
    seed,
    params
  };
}

function getFireLayerSize(id: string): { width: number; height: number } {
  const measured = cardSizes.get(id);
  const element = observedCards.get(id);
  const rect = element?.getBoundingClientRect();
  const width = Math.max(measured?.width ?? 0, element?.clientWidth ?? 0, rect?.width ?? 0, 900);
  const height = Math.max(measured?.height ?? 0, element?.clientHeight ?? 0, rect?.height ?? 0, 124);

  return { width, height };
}

function warmUpFireSimulation(simulation: FireSimulation) {
  const steps = reducedMotionQuery?.matches ? 16 : 12;

  for (let index = 0; index < steps; index += 1) {
    stepFireSimulation(simulation, false);
  }
}

function stepFireSimulation(simulation: FireSimulation, allowSparks: boolean) {
  seedBottomHeat(simulation);
  propagateHeat(simulation);
  updateSparks(simulation, allowSparks);
  simulation.frame += 1;
}

function seedBottomHeat(simulation: FireSimulation) {
  const { columns, rows, params, heat, frame } = simulation;
  const bottomRows = params.bottomRows;

  for (let y = rows - bottomRows; y < rows; y += 1) {
    const fromBottom = (rows - 1 - y) / Math.max(1, bottomRows - 1);
    for (let x = 0; x < columns; x += 1) {
      const source = sourceInfluence(simulation, x, y);
      const baseValue = baseLayerValue(simulation, x, y);
      const wave = 0.92 + Math.sin(x * 0.08 + frame * 0.16) * 0.06;
      const plumeValue = params.bottomHeat * source * wave * (1 - fromBottom * 0.12);
      const value = Math.max(plumeValue, baseValue * wave);
      const index = fireIndex(simulation, x, y);
      heat[index] = Math.max(heat[index] * 0.82, value);
    }
  }
}

function baseLayerValue(simulation: FireSimulation, x: number, y: number): number {
  const { columns, params, frame, seed } = simulation;
  const fromBottom = (simulation.rows - 1 - y) / Math.max(1, simulation.rows - 1);

  if (fromBottom > fireBaseZoneRatio) {
    return 0;
  }

  const source = sourceInfluence(simulation, x, y);
  const edgeMargin = (columns * (1 - params.baseCoverage)) / 2;
  const leftFade = clamp((x - edgeMargin) / Math.max(1, columns * 0.08), 0, 1);
  const rightFade = clamp((columns - edgeMargin - x) / Math.max(1, columns * 0.08), 0, 1);
  const edgeFade = clamp(Math.min(leftFade, rightFade) + 0.14, 0, 1);
  const baseProgress = fromBottom / fireBaseZoneRatio;
  const broadWave =
    0.7 +
    Math.sin(x * 0.032 + seed * 0.0009 + frame * 0.028) * 0.16 +
    Math.sin(x * 0.083 - seed * 0.0004 - frame * 0.019) * 0.08;
  const detailWave = 0.9 + Math.sin(x * 0.14 + y * 0.19 - frame * 0.024) * 0.07;
  const gapNoise = noise(Math.floor(x / 7), seed * 0.017 + y * 5, Math.floor(frame / 10) * 0.08);
  const gap = gapNoise < (1 - params.baseConnectivity) * 0.1 ? 0.52 : 1;
  const localHeight = clamp(0.42 + broadWave * 0.28 + source * 0.3, 0.28, 0.94);
  const heightMask =
    baseProgress <= localHeight ? 1 : clamp(1 - (baseProgress - localHeight) / 0.22, 0.24, 1);
  const plumeBoost = 0.74 + source * 0.36;
  const rowFalloff = 1 - baseProgress * 0.48;

  return clamp(
    params.baseHeat * edgeFade * broadWave * detailWave * gap * heightMask * plumeBoost * rowFalloff,
    0,
    1
  );
}

function tongueLayerValue(simulation: FireSimulation, x: number, y: number): number {
  const { params, frame, seed } = simulation;
  const fromBottom = (simulation.rows - 1 - y) / Math.max(1, simulation.rows - 1);

  if (fromBottom <= fireBaseZoneRatio) {
    return 0;
  }

  const source = sourceInfluence(simulation, x, y);
  const verticalProgress = (fromBottom - fireBaseZoneRatio) / (1 - fireBaseZoneRatio);
  const verticalFade = clamp(1 - verticalProgress * 0.72, 0.2, 1);
  const wave = 0.82 + Math.sin(x * 0.075 + y * 0.24 - frame * 0.04 + seed * 0.0006) * 0.13;
  const gapNoise = noise(Math.floor(x / 4), seed * 0.021 + y * 3, Math.floor(frame / 9) * 0.08);
  const gap = gapNoise < 0.08 ? 0.45 : 1;

  return clamp(params.bottomHeat * source * verticalFade * wave * gap * 0.72, 0, 1);
}

function sourceInfluence(simulation: FireSimulation, x: number, y: number): number {
  const { columns, rows, sourceCenters, frame } = simulation;
  const fromBottom = (rows - 1 - y) / Math.max(1, rows - 1);
  let strongest = 0;

  for (let index = 0; index < sourceCenters.length; index += 1) {
    const drift = Math.sin(frame * 0.045 + index * 1.9) * columns * 0.018;
    const center = sourceCenters[index] + drift;
    const width = columns * (0.084 - fromBottom * 0.04);
    const distance = Math.abs(x - center);
    const reach = clamp(1 - Math.max(0, fromBottom - 0.92) / 0.08, 0, 1);
    const influence = Math.max(0, 1 - distance / Math.max(1, width)) * reach;
    strongest = Math.max(strongest, influence);
  }

  return strongest;
}

function propagateHeat(simulation: FireSimulation) {
  const { columns, rows, heat, nextHeat, params, frame } = simulation;
  nextHeat.fill(0);

  for (let y = 0; y < rows - 1; y += 1) {
    const height = y / Math.max(1, rows - 1);
    const decay = params.decay + (1 - height) * 0.028;
    const bottomConnectivity = clamp((height - 0.58) / 0.42, 0, 1);
    const lateralSpread = params.spread + params.baseConnectivity * bottomConnectivity * 0.085;

    for (let x = 0; x < columns; x += 1) {
      const drift = Math.sin(frame * 0.05 + y * 0.4) > 0 ? 1 : -1;
      const below = sampleHeat(simulation, x, y + 1);
      const belowLeft = sampleHeat(simulation, x - 1, y + 1);
      const belowRight = sampleHeat(simulation, x + 1, y + 1);
      const twoBelow = sampleHeat(simulation, x + drift, y + 2);
      const turbulence = (noise(x * 0.7, y * 1.3 + simulation.seed, frame * 0.032) - 0.5) * 0.018;
      const value =
        below * (0.5 - bottomConnectivity * 0.04) +
        belowLeft * lateralSpread +
        belowRight * lateralSpread +
        twoBelow * 0.15 +
        turbulence -
        decay;
      nextHeat[fireIndex(simulation, x, y)] = clamp(value, 0, 1);
    }
  }

  for (let x = 0; x < columns; x += 1) {
    nextHeat[fireIndex(simulation, x, rows - 1)] = heat[fireIndex(simulation, x, rows - 1)] * 0.64;
  }

  simulation.heat = nextHeat;
  simulation.nextHeat = heat;
}

function sampleHeat(simulation: FireSimulation, x: number, y: number): number {
  if (x < 0 || x >= simulation.columns || y < 0 || y >= simulation.rows) {
    return 0;
  }

  return simulation.heat[fireIndex(simulation, x, y)] ?? 0;
}

function updateSparks(simulation: FireSimulation, allowSpawn: boolean) {
  const { rows, columns, params, frame, sparks, seed } = simulation;

  for (const spark of sparks) {
    spark.y -= spark.speed;
    spark.x += Math.sin((frame + spark.x) * 0.12) * 0.16;
    spark.ttl -= 1;
  }

  simulation.sparks = sparks.filter((spark) => spark.ttl > 0 && spark.y >= 0 && spark.y < rows);

  if (!allowSpawn || simulation.sparks.length > 26) {
    return;
  }

  const spawnCount = noise(frame * 7, seed, 0) < params.sparkRate ? 2 : 1;
  for (let index = 0; index < spawnCount; index += 1) {
    if (noise(index * 17 + frame, seed, frame * 0.03) > params.sparkRate * 2.4) {
      continue;
    }

    const source = simulation.sourceCenters[(frame + index) % simulation.sourceCenters.length] ?? columns * 0.5;
    const offset = (noise(index * 11, seed, frame * 0.09) - 0.5) * columns * 0.06;
    simulation.sparks.push({
      x: clamp(source + offset, 0, columns - 1),
      y: rows - 1,
      ttl: Math.round(rows * (1.25 + noise(index, seed, frame * 0.04) * 0.85)),
      speed: 0.62 + noise(index * 5, seed, frame * 0.07) * 0.42
    });
  }
}

function renderFireSimulation(simulation: FireSimulation): string {
  const { columns, rows, heat, params, frame, seed } = simulation;
  const sparkCells = new Set<string>();
  const renderedRows: string[] = [];

  for (const spark of simulation.sparks) {
    const x = Math.round(spark.x);
    const y = Math.round(spark.y);
    if (x >= 0 && x < columns && y >= 0 && y < rows) {
      sparkCells.add(`${x}:${y}`);
    }
  }

  for (let y = 0; y < rows; y += 1) {
    let line = '';
    const fromBottom = (rows - 1 - y) / Math.max(1, rows - 1);

    for (let x = 0; x < columns; x += 1) {
      if (sparkCells.has(`${x}:${y}`)) {
        line += '+';
        continue;
      }

      const heatValue = heat[fireIndex(simulation, x, y)];
      const baseValue = baseLayerValue(simulation, x, y);
      const tongueValue = tongueLayerValue(simulation, x, y);
      const rightTailValue = connectedRightTailValue(simulation, x, y);
      const value = Math.max(heatValue, baseValue, tongueValue, rightTailValue);
      const visibilityNoise = noise(x * 3.1, y * 5.7 + seed, frame * 0.018);
      const isBaseZone = fromBottom <= fireBaseZoneRatio;
      const threshold = isBaseZone ? 0.112 + fromBottom * 0.14 : 0.078 + fromBottom * 0.095;
      const baseVisibilityBoost = isBaseZone ? params.baseConnectivity * 0.24 + baseValue * 0.16 : 0;
      const tongueVisibilityBoost = !isBaseZone && fromBottom > fireMiddleZoneRatio ? 0.075 : !isBaseZone ? 0.025 : 0;
      const tailVisibilityBoost = rightTailValue > 0 ? 0.12 + rightTailValue * 0.56 : 0;
      if (
        value < threshold ||
        visibilityNoise >
          params.maxOccupancy * 1.08 +
            value * 0.52 +
            baseVisibilityBoost +
            tongueVisibilityBoost +
            tailVisibilityBoost
      ) {
        line += ' ';
        continue;
      }

      line += fireSymbol(value);
    }

    renderedRows.push(line);
  }

  return enforceOccupancy(renderedRows, columns, rows, params.maxOccupancy, seed, frame).join('\n');
}

function connectedRightTailValue(simulation: FireSimulation, x: number, y: number): number {
  const { columns, rows, params, frame, seed } = simulation;
  const fromBottom = (rows - 1 - y) / Math.max(1, rows - 1);

  if (fromBottom > fireMiddleZoneRatio + 0.08) {
    return 0;
  }

  const horizontalPosition = x / Math.max(1, columns - 1);
  const rightBlend = smoothstep(0.44, 0.74, horizontalPosition);

  if (rightBlend <= 0) {
    return 0;
  }

  const source = sourceInfluence(simulation, x, y);
  const baseRise =
    fireBaseZoneRatio +
    0.13 +
    Math.sin(x * 0.036 + seed * 0.0007 + frame * 0.026) * 0.06 +
    source * 0.065;
  const verticalMask =
    fromBottom <= baseRise ? 1 : clamp(1 - (fromBottom - baseRise) / 0.19, 0, 1);

  if (verticalMask <= 0) {
    return 0;
  }

  const gapNoise = noise(Math.floor(x / 8), seed * 0.029 + y * 4, Math.floor(frame / 11) * 0.07);
  const gap = gapNoise < 0.045 ? 0.46 : 1;
  const softScoreArea = horizontalPosition > 0.88 && fromBottom > 0.34 && fromBottom < 0.64 ? 0.82 : 1;
  const edgeLift = horizontalPosition > 0.76 && (fromBottom < 0.34 || fromBottom > 0.56) ? 1.24 : 1;
  const wave =
    0.76 +
    Math.sin(x * 0.052 - frame * 0.031 + seed * 0.0005) * 0.12 +
    Math.sin(x * 0.117 + y * 0.13 + frame * 0.018) * 0.055;

  return clamp(
    params.baseHeat *
      (0.24 + params.baseConnectivity * 0.17) *
      rightBlend *
      verticalMask *
      gap *
      softScoreArea *
      edgeLift *
      wave,
    0,
    1
  );
}

function smoothstep(edge0: number, edge1: number, value: number): number {
  const t = clamp((value - edge0) / Math.max(0.0001, edge1 - edge0), 0, 1);
  return t * t * (3 - 2 * t);
}

function fireSymbol(heat: number): string {
  if (heat >= 0.78) {
    return 'X';
  }

  if (heat >= 0.58) {
    return 'x';
  }

  if (heat >= 0.36) {
    return '-';
  }

  if (heat >= 0.16) {
    return '.';
  }

  return ' ';
}

function enforceOccupancy(
  rows: string[],
  columns: number,
  rowCount: number,
  maxOccupancy: number,
  seed: number,
  frame: number
): string[] {
  const total = columns * rowCount;
  const occupied = rows.reduce((sum, row) => sum + (row.match(/[^ ]/g)?.length ?? 0), 0);

  if (!total || occupied / total <= maxOccupancy) {
    return rows;
  }

  const zoneCaps = {
    top: maxOccupancy * 0.42,
    middle: maxOccupancy * 0.94,
    bottom: Math.min(0.4, maxOccupancy * 2.25)
  };
  const zoneCounts = {
    top: { total: 0, occupied: 0 },
    middle: { total: 0, occupied: 0 },
    bottom: { total: 0, occupied: 0 }
  };

  for (let rowIndex = 0; rowIndex < rowCount; rowIndex += 1) {
    const zone = fireRowZone(rowIndex, rowCount);
    const row = rows[rowIndex] ?? '';

    for (let column = 0; column < columns; column += 1) {
      zoneCounts[zone].total += 1;
      if ((row[column] ?? ' ') !== ' ') {
        zoneCounts[zone].occupied += 1;
      }
    }
  }

  return rows.map((row, rowIndex) =>
    Array.from(row)
      .map((char, column) => {
        if (char === ' ' || char === '+') {
          return char;
        }

        const zone = fireRowZone(rowIndex, rowCount);
        const zoneDensity = zoneCounts[zone].occupied / Math.max(1, zoneCounts[zone].total);
        const keepRatio = zoneDensity > zoneCaps[zone] ? zoneCaps[zone] / zoneDensity : 1;
        const bottomBias = rowIndex / Math.max(1, rowCount - 1);
        const effectiveKeepRatio = clamp(keepRatio * (0.78 + bottomBias * 0.16), 0, 1);
        return noise(column * 41, rowIndex * 17 + seed, frame * 0.02) <= effectiveKeepRatio ? char : ' ';
      })
      .join('')
  );
}

function fireRowZone(rowIndex: number, rowCount: number): 'top' | 'middle' | 'bottom' {
  const fromBottom = (rowCount - 1 - rowIndex) / Math.max(1, rowCount - 1);

  if (fromBottom <= fireBaseZoneRatio) {
    return 'bottom';
  }

  if (fromBottom <= fireMiddleZoneRatio) {
    return 'middle';
  }

  return 'top';
}

function fireTier(score: number): FireTierParams {
  if (score >= 90) {
    return {
      bottomHeat: 1,
      bottomRows: 4,
      baseHeat: 0.58,
      baseCoverage: 0.94,
      baseConnectivity: 0.9,
      sourceCountBonus: 2,
      decay: 0.026,
      sparkRate: 0.16,
      maxOccupancy: 0.165,
      spread: 0.17
    };
  }

  if (score >= 80) {
    return {
      bottomHeat: 0.94,
      bottomRows: 4,
      baseHeat: 0.56,
      baseCoverage: 0.92,
      baseConnectivity: 0.86,
      sourceCountBonus: 1,
      decay: 0.03,
      sparkRate: 0.145,
      maxOccupancy: 0.135,
      spread: 0.16
    };
  }

  return {
    bottomHeat: 0.82,
    bottomRows: 4,
    baseHeat: 0.48,
    baseCoverage: 0.9,
    baseConnectivity: 0.78,
    sourceCountBonus: 0,
    decay: 0.034,
    sparkRate: 0.095,
    maxOccupancy: 0.105,
    spread: 0.15
  };
}

function updateFireDiagnosticsAfterRender(simulation: FireSimulation, frameText: string) {
  if (!fireDebugEnabled) {
    return;
  }

  void nextTick(() => {
    if (fireSimulation?.id !== simulation.id || activeFireText.value !== frameText) {
      return;
    }

    updateFireDiagnostics(simulation, frameText);
  });
}

function updateFireDiagnostics(simulation: FireSimulation, frameText: string) {
  if (!fireDebugEnabled) {
    return;
  }

  const mainElement = observedCards.get(simulation.id);
  const layerElement = mainElement?.querySelector<HTMLElement>('.hot-card__ascii-fire') ?? null;

  if (!mainElement || !layerElement) {
    return;
  }

  const diagnostics = computeFireDiagnostics(
    simulation,
    frameText,
    rectFromDom(mainElement.getBoundingClientRect()),
    rectFromDom(layerElement.getBoundingClientRect()),
    getComputedStyle(layerElement).color
  );

  activeFireDiagnostics.value = diagnostics;

  const now = Date.now();
  if (now - simulation.lastDiagnosticAt >= 1000) {
    simulation.lastDiagnosticAt = now;
    console.debug('[hot-products-fire]', diagnostics);
  }
}

function computeFireDiagnostics(
  simulation: FireSimulation,
  frameText: string,
  mainRect: FireRect,
  layerRect: FireRect,
  tierColor: string
): FireDiagnostics {
  const stats = getFireFrameStats(frameText, simulation.columns, simulation.rows);

  if (stats.heatCenterY !== null) {
    simulation.heatCenterHistory.push(stats.heatCenterY);
    if (simulation.heatCenterHistory.length > 5) {
      simulation.heatCenterHistory.shift();
    }
  }

  const heatCenterHistory = [...simulation.heatCenterHistory];
  const firstCenter = heatCenterHistory[0];
  const lastCenter = heatCenterHistory[heatCenterHistory.length - 1];
  const hasRecentUpwardStep = heatCenterHistory.some((center, index) => {
    const previous = heatCenterHistory[index - 1];
    return previous !== undefined && center < previous - 0.08;
  });
  const isMovingUp =
    hasRecentUpwardStep ||
    (heatCenterHistory.length >= 3 &&
      firstCenter !== undefined &&
      lastCenter !== undefined &&
      lastCenter < firstCenter - 0.2);

  return {
    ...stats,
    cardId: simulation.id,
    wbProductId: simulation.wbProductId,
    score: simulation.score,
    tier: simulation.tier,
    columns: simulation.columns,
    rows: simulation.rows,
    frameRows: frameText.split('\n').length,
    sparkCount: simulation.sparks.length,
    cardWidth: mainRect.width,
    fireLayerWidth: layerRect.width,
    mainRect,
    layerRect,
    layerInsideMain: rectInside(layerRect, mainRect),
    layerCoversMain: rectCovers(layerRect, mainRect),
    heatCenterHistory,
    isMovingUp,
    seededBottomRows: simulation.params.bottomRows,
    baseCoverage: simulation.params.baseCoverage,
    logicalRows: simulation.rows,
    logicalColumns: simulation.columns,
    tierColor
  };
}

function getFireFrameStats(frameText: string, expectedColumns: number, expectedRows: number): FireFrameStats {
  const rows = frameText.split('\n');
  const safeRows = rows.length > 0 ? rows : [''];
  const topEnd = Math.max(1, Math.floor(expectedRows / 3));
  const middleEnd = Math.max(topEnd + 1, Math.floor((expectedRows * 2) / 3));
  const counts = { X: 0, x: 0, dash: 0, dot: 0, plus: 0 };
  const zoneTotals = { top: 0, middle: 0, bottom: 0 };
  const zoneOccupied = { top: 0, middle: 0, bottom: 0 };
  let occupied = 0;
  let emptyRowCount = 0;
  let maxFrameWidth = 0;
  let topSparkCount = 0;
  let bottomConnectedRun = 0;
  let bottomSegments = 0;
  let weightedY = 0;
  let weightTotal = 0;
  const bottomColumns = new Array<boolean>(expectedColumns).fill(false);

  for (let rowIndex = 0; rowIndex < expectedRows; rowIndex += 1) {
    const row = safeRows[rowIndex] ?? '';
    maxFrameWidth = Math.max(maxFrameWidth, row.length);
    const zone = rowIndex < topEnd ? 'top' : rowIndex < middleEnd ? 'middle' : 'bottom';
    let rowOccupied = 0;

    for (let column = 0; column < expectedColumns; column += 1) {
      const char = row[column] ?? ' ';
      zoneTotals[zone] += 1;

      if (char === ' ') {
        continue;
      }

      rowOccupied += 1;
      occupied += 1;
      zoneOccupied[zone] += 1;

      const fromBottom = (expectedRows - 1 - rowIndex) / Math.max(1, expectedRows - 1);
      if (fromBottom <= fireBaseZoneRatio) {
        bottomColumns[column] = true;
      }

      if (char === 'X') {
        counts.X += 1;
      } else if (char === 'x') {
        counts.x += 1;
      } else if (char === '-') {
        counts.dash += 1;
      } else if (char === '.') {
        counts.dot += 1;
      } else if (char === '+') {
        counts.plus += 1;
        if (rowIndex < topEnd) {
          topSparkCount += 1;
        }
      }

      const weight = fireCharacterWeight(char);
      weightedY += rowIndex * weight;
      weightTotal += weight;
    }

    if (rowOccupied === 0) {
      emptyRowCount += 1;
    }
  }

  let currentBottomRun = 0;
  for (const isOccupied of bottomColumns) {
    if (isOccupied) {
      currentBottomRun += 1;
      bottomConnectedRun = Math.max(bottomConnectedRun, currentBottomRun);
      continue;
    }

    if (currentBottomRun > 0) {
      bottomSegments += 1;
    }

    currentBottomRun = 0;
  }

  if (currentBottomRun > 0) {
    bottomSegments += 1;
  }

  return {
    density: occupied / Math.max(1, expectedColumns * expectedRows),
    bottomDensity: zoneOccupied.bottom / Math.max(1, zoneTotals.bottom),
    middleDensity: zoneOccupied.middle / Math.max(1, zoneTotals.middle),
    topDensity: zoneOccupied.top / Math.max(1, zoneTotals.top),
    bottomCoverageRatio: bottomColumns.filter(Boolean).length / Math.max(1, expectedColumns),
    emptyRowCount,
    maxFrameWidth,
    topSparkCount,
    bottomConnectedRun,
    bottomSegments,
    heatCenterY: weightTotal > 0 ? weightedY / weightTotal : null,
    charCounts: counts
  };
}

function fireCharacterWeight(char: string): number {
  if (char === 'X') {
    return 4;
  }

  if (char === 'x') {
    return 3;
  }

  if (char === '-') {
    return 2;
  }

  if (char === '.' || char === '+') {
    return 1;
  }

  return 0;
}

function rectFromDom(rect: DOMRect): FireRect {
  return {
    x: rect.x,
    y: rect.y,
    width: rect.width,
    height: rect.height
  };
}

function rectInside(inner: FireRect, outer: FireRect): boolean {
  const tolerance = 1;
  return (
    inner.x >= outer.x - tolerance &&
    inner.y >= outer.y - tolerance &&
    inner.x + inner.width <= outer.x + outer.width + tolerance &&
    inner.y + inner.height <= outer.y + outer.height + tolerance
  );
}

function rectCovers(layer: FireRect, target: FireRect): boolean {
  const tolerance = 1;
  return (
    Math.abs(layer.x - target.x) <= tolerance &&
    Math.abs(layer.y - target.y) <= tolerance &&
    Math.abs(layer.width - target.width) <= tolerance &&
    Math.abs(layer.height - target.height) <= tolerance
  );
}

function fireDebugAttributes(item: HotProductRecommendationItem): Record<string, string> {
  if (!fireDebugEnabled || activeFireId.value !== item.id || !activeFireDiagnostics.value) {
    return {};
  }

  const diagnostics = activeFireDiagnostics.value;
  return {
    'data-fire-tier': diagnostics.tier,
    'data-fire-columns': String(diagnostics.columns),
    'data-fire-rows': String(diagnostics.rows),
    'data-fire-logical-columns': String(diagnostics.logicalColumns),
    'data-fire-logical-rows': String(diagnostics.logicalRows),
    'data-fire-density': formatDiagnosticRatio(diagnostics.density),
    'data-fire-bottom-density': formatDiagnosticRatio(diagnostics.bottomDensity),
    'data-fire-middle-density': formatDiagnosticRatio(diagnostics.middleDensity),
    'data-fire-top-density': formatDiagnosticRatio(diagnostics.topDensity),
    'data-fire-card-width': formatDiagnosticRatio(diagnostics.cardWidth),
    'data-fire-layer-width': formatDiagnosticRatio(diagnostics.fireLayerWidth),
    'data-fire-bottom-coverage-ratio': formatDiagnosticRatio(diagnostics.bottomCoverageRatio),
    'data-fire-base-coverage': formatDiagnosticRatio(diagnostics.baseCoverage),
    'data-fire-spark-count': String(diagnostics.sparkCount),
    'data-fire-top-spark-count': String(diagnostics.topSparkCount),
    'data-fire-bottom-connected-run': String(diagnostics.bottomConnectedRun),
    'data-fire-bottom-segments': String(diagnostics.bottomSegments),
    'data-fire-seeded-bottom-rows': String(diagnostics.seededBottomRows),
    'data-fire-tier-color': diagnostics.tierColor,
    'data-fire-is-moving-up': String(diagnostics.isMovingUp),
    'data-fire-layer-covers-main': String(diagnostics.layerCoversMain)
  };
}

function formatDiagnosticRatio(value: number): string {
  return value.toFixed(4);
}

function fireIndex(simulation: FireSimulation, x: number, y: number): number {
  return y * simulation.columns + x;
}

function noise(x: number, y: number, t: number): number {
  const value = Math.sin(x * 12.9898 + y * 78.233 + t * 37.719) * 43758.5453;
  return value - Math.floor(value);
}

function hashString(value: string): number {
  let hash = 2166136261;

  for (let index = 0; index < value.length; index += 1) {
    hash ^= value.charCodeAt(index);
    hash = Math.imul(hash, 16777619);
  }

  return Math.abs(hash);
}

function clamp(value: number, min: number, max: number): number {
  return Math.max(min, Math.min(max, value));
}

function locate(item: HotProductRecommendationItem) {
  if (item.wbProductId) {
    emit('locate', item.wbProductId);
  }
}

function identityValue(value: string | null | undefined): string {
  return value?.trim() ? value : '—';
}

function textValue(value: string | null | undefined): string {
  return value?.trim() ? value : 'Нет данных';
}

function numberValue(value: number | null | undefined): string {
  return value === null || value === undefined ? 'Нет данных' : formatNumber(value);
}

function formatNumber(value: number): string {
  return new Intl.NumberFormat('ru-RU').format(value);
}

function formatMoney(value: number | null | undefined): string {
  if (value === null || value === undefined) {
    return 'Нет данных';
  }

  return `${new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 2 }).format(value)} ₽`;
}

function displayPrice(item: HotProductRecommendationItem): number | null {
  return item.walletPrice ?? item.price ?? item.priceWithoutDiscount;
}

function formatScore(value: number): string {
  const normalized = Math.max(0, Math.min(100, Math.round(value)));
  return `${new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 0 }).format(normalized)}/100`;
}

function scoreTier(value: number): 'priority' | 'strong' | 'steady' {
  if (value >= 90) {
    return 'priority';
  }

  if (value >= 80) {
    return 'strong';
  }

  return 'steady';
}

function formatConfidence(value: number): string {
  const percent = Math.max(0, Math.min(100, Math.round(value * 100)));
  return `${new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 0 }).format(percent)}%`;
}

function formatStock(value: number | null): string {
  if (value === null) {
    return 'Нет данных';
  }

  if (value >= 40) {
    return '≥40';
  }

  return formatNumber(value);
}

function formatPosition(item: HotProductRecommendationItem): string {
  if (item.positionState === 'observed' && item.position !== null) {
    return `#${formatNumber(item.position)}`;
  }

  if (item.positionState === 'beyondObservedRange' && item.observedRangeLimit !== null) {
    return `>${formatNumber(item.observedRangeLimit)}`;
  }

  return 'Нет данных';
}

function ratingTone(value: number | null): string {
  if (value === null || value <= 0) {
    return 'neutral';
  }

  if (value >= 4.7) {
    return 'positive';
  }

  if (value >= 4.2) {
    return 'warning';
  }

  return 'negative';
}

function factorValue(factor: HotProductRecommendationFactor): string {
  if (factor.value === null || factor.value === undefined || factor.value === '') {
    return 'Нет данных';
  }

  if (typeof factor.value === 'number') {
    return new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 2 }).format(factor.value);
  }

  if (typeof factor.value === 'boolean') {
    return factor.value ? 'Да' : 'Нет';
  }

  if (typeof factor.value === 'string') {
    return factor.value;
  }

  return 'Нет данных';
}

function factorsByDirection(
  item: HotProductRecommendationItem,
  direction: 'positive' | 'negative' | 'neutral'
): HotProductRecommendationFactor[] {
  return item.factors.filter((factor) => factor.direction === direction);
}

onBeforeUnmount(() => {
  stopFireAnimation();
  resizeObserver?.disconnect();
  resizeObserver = null;
  observedCards.clear();
  cardSizes.clear();
});
</script>

<template>
  <section class="hot-products app-surface" aria-labelledby="hot-products-title">
    <header class="hot-products__header">
      <div class="hot-products__heading">
        <div class="hot-products__title-row">
            <h2 id="hot-products-title">Перспективные товары</h2>
            <HelpTooltip text="Товары, которые стоит изучить в первую очередь. Оценка учитывает рыночные признаки, но перед запуском всё равно проверьте маржинальность, поставщика и конкуренцию." />
          </div>
      </div>
    </header>

    <div v-if="loading" class="hot-products__state">
      <span class="hot-products__pulse" />
      Загружаем перспективные товары...
    </div>

    <div v-else-if="error" class="hot-products__state hot-products__state--error">
      Не удалось загрузить рекомендации. Попробуйте обновить страницу.
    </div>

    <div v-else-if="!hasItems" class="hot-products__state hot-products__state--empty">
      <strong>Рекомендации ещё не рассчитаны.</strong>
      <span>Запустите пересчёт после обновления данных рынка.</span>
    </div>

    <div v-else class="hot-products__showcase" role="list">
      <article
        v-for="item in items"
        :key="item.id"
        class="hot-card"
        :class="[`hot-card--${scoreTier(item.score)}`, { 'hot-card--open': expandedId === item.id }]"
        role="listitem"
        @pointerenter="startFire(item)"
        @pointermove="startFire(item)"
        @pointerleave="stopFire(item)"
        @focusin="startFire(item)"
        @mousemove="startFire(item)"
        @focusout="stopFire(item, $event)"
      >
        <svg class="hot-card__flame" viewBox="0 0 96 150" aria-hidden="true" focusable="false">
          <path
            class="hot-card__flame-outer"
            d="M47 145C25 131 10 111 11 86c1-21 13-33 18-48 4-12 1-23-4-34 19 11 31 29 30 48 11-12 17-29 13-48 21 19 29 43 23 66 8-7 12-17 11-29 13 17 17 39 10 60-8 25-31 40-65 44Z"
          />
          <path
            class="hot-card__flame-middle"
            d="M49 132c-18-11-28-26-27-45 1-16 11-25 20-36 7-9 9-20 6-32 17 13 23 30 17 49 10-7 16-18 17-33 13 15 17 32 11 49 7-4 12-11 15-21 5 19 0 39-13 52-10 10-24 16-46 17Z"
          />
          <path
            class="hot-card__flame-inner"
            d="M50 126c-13-9-20-21-18-35 2-12 11-20 20-30 8-9 11-17 10-27 13 13 15 27 8 42 7-3 12-9 16-18 5 17 1 34-10 47-7 9-15 16-26 21Z"
          />
          <path
            class="hot-card__flame-core"
            d="M52 116c-8-7-11-15-8-25 2-8 9-14 15-21 4-5 7-11 7-18 8 10 8 21 2 32 5-2 9-6 12-12 1 16-9 34-28 44Z"
          />
        </svg>
        <div class="hot-card__content">
          <div
            class="hot-card__main"
            role="button"
            tabindex="0"
            :ref="(element) => setCardMainRef(item.id, element)"
            :aria-expanded="expandedId === item.id"
            @click="toggleExplanation(item)"
            @keydown.enter.prevent="toggleExplanation(item)"
            @keydown.space.prevent="toggleExplanation(item)"
          >
            <div class="hot-card__image-wrap">
              <MarketProductImage :src="item.thumbnailUrl" :alt="item.productName" />
            </div>

            <div class="hot-card__body">
              <div class="hot-card__topline">
                <span>{{ textValue(item.sourceCategory) }}</span>
                <span>{{ textValue(item.sourceSubcategory) }}</span>
              </div>

              <h3 class="hot-card__name">{{ item.productName }}</h3>

              <div class="hot-card__identity">
                <span>{{ identityValue(item.brandName) }}</span>
                <span>{{ identityValue(item.sellerName) }}</span>
              </div>

              <div class="hot-card__metrics" aria-label="Показатели рекомендации">
                <div class="hot-card__metric hot-card__metric--price">
                  <strong>{{ formatMoney(displayPrice(item)) }}</strong>
                  <span>Цена</span>
                </div>
                <div class="hot-card__metric">
                  <strong>{{ formatPosition(item) }}</strong>
                  <span>Позиция</span>
                </div>
                <div class="hot-card__metric">
                  <strong :class="`hot-card__rating--${ratingTone(item.rating)}`">{{ numberValue(item.rating) }}</strong>
                  <span>Рейтинг WB</span>
                </div>
                <div class="hot-card__metric">
                  <strong>{{ numberValue(item.feedbackCount) }}</strong>
                  <span>Отзывы WB</span>
                </div>
                <div class="hot-card__metric">
                  <strong>{{ formatStock(item.totalQuantity) }}</strong>
                  <span>Остаток</span>
                </div>
              </div>
            </div>

            <aside class="hot-card__side" aria-label="Оценка рекомендации">
              <div class="hot-card__score">
                <strong>{{ formatScore(item.score) }}</strong>
                <span>Индекс перспективности</span>
              </div>

              <button
                class="hot-card__why-link app-operator-link"
                type="button"
                :aria-expanded="expandedId === item.id"
                @click.stop="toggleExplanation(item)"
              >
                Почему
              </button>
            </aside>

            <pre
              v-if="activeFireId === item.id"
              class="hot-card__ascii-fire"
              aria-hidden="true"
              v-bind="fireDebugAttributes(item)"
            >{{ activeFireText }}</pre>
          </div>

          <div v-if="expandedId === item.id" class="hot-card__details">
            <section class="hot-card__details-section hot-card__details-section--wide">
              <h4>Почему товар перспективен</h4>
              <p>{{ item.reason }}</p>
              <div class="hot-card__details-actions">
                <div class="hot-card__confidence">
                  <span>Уверенность расчёта</span>
                  <strong>{{ formatConfidence(item.confidence) }}</strong>
                </div>
              </div>
            </section>

            <section v-if="factorsByDirection(item, 'positive').length" class="hot-card__details-section">
              <h4>Удачные параметры</h4>
              <div class="hot-card__factor-list">
                <span
                  v-for="factor in factorsByDirection(item, 'positive')"
                  :key="`${item.id}-positive-${factor.code}`"
                  class="hot-card__factor hot-card__factor--positive"
                >
                  <strong>{{ factor.label }}</strong>
                  <span>{{ factorValue(factor) }}</span>
                </span>
              </div>
            </section>

            <section class="hot-card__details-section">
              <h4>На что обратить внимание</h4>
              <div v-if="factorsByDirection(item, 'negative').length" class="hot-card__factor-list">
                <span
                  v-for="factor in factorsByDirection(item, 'negative')"
                  :key="`${item.id}-negative-${factor.code}`"
                  class="hot-card__factor hot-card__factor--negative"
                >
                  <strong>{{ factor.label }}</strong>
                  <span>{{ factorValue(factor) }}</span>
                </span>
              </div>
              <p v-else class="hot-card__calm-note">
                Явные рискованные параметры в расчёте не выделены, но товар всё равно требует проверки маржинальности,
                поставщика и конкуренции.
              </p>
            </section>

            <section v-if="factorsByDirection(item, 'neutral').length" class="hot-card__details-section">
              <h4>Дополнительные признаки</h4>
              <div class="hot-card__factor-list">
                <span
                  v-for="factor in factorsByDirection(item, 'neutral')"
                  :key="`${item.id}-neutral-${factor.code}`"
                  class="hot-card__factor"
                >
                  <strong>{{ factor.label }}</strong>
                  <span>{{ factorValue(factor) }}</span>
                </span>
              </div>
            </section>

            <div v-if="item.wbProductId" class="hot-card__details-footer">
              <button
                class="hot-card__locate"
                type="button"
                @click="locate(item)"
              >
                Показать в таблице
              </button>
            </div>
          </div>
        </div>
      </article>
    </div>
  </section>
</template>

<style scoped>
.hot-products {
  position: relative;
  overflow: visible;
  border: 0;
  background: transparent;
  box-shadow: none;
}

.hot-products::before {
  display: none;
  content: none;
}

.hot-products__header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: var(--space-4);
  border-bottom: 0;
  padding: 0 0 var(--space-3);
}

.hot-products__heading {
  display: grid;
  gap: var(--space-2);
  max-width: 58rem;
}

.hot-products__title-row {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--space-2);
}

.hot-products__title-row h2 {
  margin: 0;
  color: var(--color-text);
  font-size: clamp(1.15rem, 1.55vw, 1.5rem);
  font-weight: 860;
  letter-spacing: 0;
}

.hot-products__heading p {
  display: grid;
  gap: 0.15rem;
  margin: 0;
  color: var(--color-text-muted);
  font-size: 0.875rem;
  line-height: 1.5;
}

.hot-products__state {
  display: flex;
  min-height: 6rem;
  align-items: center;
  justify-content: center;
  gap: var(--space-2);
  padding: var(--space-5);
  color: var(--color-text-muted);
  font-size: 0.875rem;
  text-align: center;
}

.hot-products__state--empty {
  display: grid;
  gap: var(--space-1);
}

.hot-products__state--empty strong {
  color: var(--color-text);
}

.hot-products__state--error {
  color: var(--state-danger);
}

.hot-products__pulse {
  width: 0.7rem;
  height: 0.7rem;
  border-radius: 999px;
  background: var(--accent-ember);
  box-shadow: 0 0 0 0 rgb(249 115 22 / 0.32);
  animation: hot-pulse 1.2s ease-out infinite;
}

.hot-products__showcase {
  display: grid;
  grid-template-columns: 1fr;
  gap: var(--space-4);
  overflow: visible;
  padding: var(--space-3) 0 var(--space-4) 3.2rem;
}

.hot-card {
  --flame-aura: rgb(249 115 22 / 0.2);
  --flame-core: rgb(249 115 22 / 0.42);
  --flame-deep: rgb(127 29 29 / 0.2);
  --flame-edge: rgb(249 115 22 / 0.16);
  --flame-hot: rgb(255 196 87 / 0.42);
  --flame-outline: rgb(249 115 22 / 0.58);
  --flame-shadow: rgb(185 28 28 / 0.18);
  position: relative;
  isolation: isolate;
  overflow: visible;
  border: 1px solid rgb(249 115 22 / 0.18);
  border-radius: var(--radius-md);
  background:
    linear-gradient(180deg, rgb(255 255 255 / 0.028), transparent),
    rgb(10 13 18 / 0.94);
  box-shadow:
    inset 0 1px 0 rgb(255 255 255 / 0.035),
    0 16px 34px rgb(0 0 0 / 0.22);
  transition:
    border-color 140ms ease,
    box-shadow 140ms ease,
    transform 140ms ease;
}

.hot-card::after {
  position: absolute;
  content: '';
  pointer-events: none;
}

.hot-card::after {
  z-index: 0;
  inset: -1px -1px auto;
  height: 7.75rem;
  border-radius: inherit;
  border: 1px solid color-mix(in srgb, var(--flame-outline), transparent 42%);
  opacity: var(--edge-opacity, 0.5);
  background: linear-gradient(90deg, var(--flame-outline), transparent 5rem);
  clip-path: polygon(0 0, 100% 0, 100% 100%, 0 100%, 0 82%, 1.1% 75%, 0 66%, 1.3% 56%, 0 45%, 1.1% 34%, 0 24%);
  transition: opacity 140ms ease, border-color 140ms ease;
}

.hot-card__flame {
  position: absolute;
  z-index: 4;
  top: 0.02rem;
  left: -3.2rem;
  width: 5.45rem;
  height: 7.65rem;
  pointer-events: none;
  filter:
    drop-shadow(0.06rem 0 0 var(--flame-outline))
    drop-shadow(0 0 0.5rem var(--flame-shadow));
  opacity: var(--flame-opacity, 0.86);
  transition: filter 140ms ease, opacity 140ms ease;
}

.hot-card__flame-outer {
  fill: var(--flame-outer, rgb(220 38 38 / 0.68));
  stroke: var(--flame-outline);
  stroke-linejoin: round;
  stroke-width: 2;
}

.hot-card__flame-middle {
  fill: var(--flame-middle, rgb(249 115 22 / 0.76));
}

.hot-card__flame-inner {
  fill: var(--flame-inner, rgb(251 146 60 / 0.8));
}

.hot-card__flame-core {
  fill: var(--flame-core-fill, rgb(254 240 138 / 0.82));
}

.hot-card:hover,
.hot-card--open {
  transform: none;
}

.hot-card:hover {
  border-color: color-mix(in srgb, var(--flame-outline), transparent 22%);
}

.hot-card:hover::after {
  opacity: 0.72;
}

.hot-card:hover .hot-card__flame {
  filter:
    drop-shadow(0.06rem 0 0 var(--flame-outline))
    drop-shadow(0 0 0.72rem var(--flame-shadow));
  opacity: 1;
}

.hot-card--steady {
  --ascii-fire-color: rgb(251 146 60 / 0.42);
  --ascii-fire-shadow-primary: rgb(249 115 22 / 0.14);
  --ascii-fire-shadow-secondary: rgb(194 65 12 / 0.06);
  --flame-aura: rgb(249 115 22 / 0.12);
  --flame-core: rgb(249 115 22 / 0.24);
  --flame-deep: rgb(127 29 29 / 0.12);
  --flame-edge: rgb(249 115 22 / 0.14);
  --flame-hot: rgb(255 196 87 / 0.18);
  --flame-outline: rgb(249 115 22 / 0.34);
  --flame-outer: rgb(194 65 12 / 0.48);
  --flame-middle: rgb(249 115 22 / 0.5);
  --flame-inner: rgb(251 146 60 / 0.56);
  --flame-core-fill: rgb(253 186 116 / 0.5);
  --flame-shadow: rgb(249 115 22 / 0.08);
  --flame-opacity: 0.72;
  --edge-opacity: 0.58;
  border-color: rgb(249 115 22 / 0.24);
  box-shadow:
    inset 0 1px 0 rgb(255 255 255 / 0.035),
    0 12px 24px rgb(0 0 0 / 0.2);
}

.hot-card--strong {
  --ascii-fire-color: rgb(251 146 60 / 0.52);
  --ascii-fire-shadow-primary: rgb(249 115 22 / 0.2);
  --ascii-fire-shadow-secondary: rgb(185 28 28 / 0.08);
  --flame-aura: rgb(249 115 22 / 0.18);
  --flame-core: rgb(249 115 22 / 0.4);
  --flame-deep: rgb(127 29 29 / 0.18);
  --flame-edge: rgb(249 115 22 / 0.24);
  --flame-hot: rgb(255 196 87 / 0.34);
  --flame-outline: rgb(249 115 22 / 0.58);
  --flame-outer: rgb(220 38 38 / 0.6);
  --flame-middle: rgb(234 88 12 / 0.76);
  --flame-inner: rgb(249 115 22 / 0.72);
  --flame-core-fill: rgb(254 215 170 / 0.72);
  --flame-shadow: rgb(249 115 22 / 0.13);
  --flame-opacity: 0.9;
  --edge-opacity: 0.72;
  border-color: rgb(249 115 22 / 0.38);
  box-shadow:
    inset 0 1px 0 rgb(255 255 255 / 0.04),
    0 14px 28px rgb(0 0 0 / 0.22),
    0 0 10px rgb(249 115 22 / 0.035);
}

.hot-card--priority {
  --ascii-fire-color: rgb(248 113 113 / 0.58);
  --ascii-fire-shadow-primary: rgb(249 115 22 / 0.22);
  --ascii-fire-shadow-secondary: rgb(127 29 29 / 0.12);
  --flame-aura: rgb(185 28 28 / 0.25);
  --flame-core: rgb(239 68 68 / 0.42);
  --flame-deep: rgb(127 29 29 / 0.28);
  --flame-edge: rgb(239 68 68 / 0.28);
  --flame-hot: rgb(255 196 87 / 0.4);
  --flame-outline: rgb(248 113 113 / 0.68);
  --flame-outer: rgb(185 28 28 / 0.72);
  --flame-middle: rgb(234 88 12 / 0.84);
  --flame-inner: rgb(249 115 22 / 0.86);
  --flame-core-fill: rgb(254 240 138 / 0.86);
  --flame-shadow: rgb(185 28 28 / 0.2);
  --flame-opacity: 1;
  --edge-opacity: 0.82;
  border-color: rgb(185 28 28 / 0.46);
  box-shadow:
    inset 0 1px 0 rgb(255 255 255 / 0.045),
    0 15px 30px rgb(0 0 0 / 0.24),
    0 0 12px rgb(185 28 28 / 0.045);
  animation: none;
}

.hot-card--open {
  box-shadow:
    inset 0 1px 0 rgb(255 255 255 / 0.04),
    0 12px 24px rgb(0 0 0 / 0.2);
}

.hot-card__content {
  position: relative;
  z-index: auto;
  overflow: hidden;
  border-radius: inherit;
  background:
    linear-gradient(180deg, rgb(255 255 255 / 0.026), transparent 60%),
    rgb(8 11 16 / 0.88);
}

.hot-card__main {
  display: grid;
  position: relative;
  grid-template-columns: 5.75rem minmax(0, 1fr) minmax(9rem, 11rem);
  gap: var(--space-3);
  align-items: stretch;
  height: 7.75rem;
  padding: 0.25rem 0.65rem 0.25rem 0.25rem;
  cursor: pointer;
}

.hot-card__main::before {
  position: absolute;
  z-index: 0;
  inset: 0;
  background: linear-gradient(90deg, rgb(249 115 22 / 0.07), rgb(255 255 255 / 0.025) 34%, transparent 72%);
  content: '';
  opacity: 0;
  pointer-events: none;
  transition: opacity 140ms ease;
}

.hot-card__ascii-fire {
  position: absolute;
  z-index: 1;
  inset: 0;
  margin: 0;
  overflow: hidden;
  color: var(--ascii-fire-color, rgb(251 146 60 / 0.5));
  font-family: ui-monospace, SFMono-Regular, Consolas, 'Liberation Mono', monospace;
  font-size: 0.42rem;
  font-weight: 800;
  line-height: calc(7.75rem / 24);
  opacity: 0;
  pointer-events: none;
  text-shadow:
    0 0 0.08rem var(--ascii-fire-shadow-primary, rgb(249 115 22 / 0.18)),
    0 0 0.16rem var(--ascii-fire-shadow-secondary, rgb(185 28 28 / 0.08));
  transition: opacity 140ms ease;
  user-select: none;
  white-space: pre;
}

.hot-card:hover .hot-card__main::before {
  opacity: 1;
}

.hot-card:hover .hot-card__ascii-fire,
.hot-card:focus-within .hot-card__ascii-fire {
  opacity: 0.54;
}

.hot-card__main:focus-visible {
  outline: 2px solid var(--accent-primary-border);
  outline-offset: -2px;
}

.hot-card__image-wrap {
  display: grid;
  position: relative;
  z-index: 6;
  width: 5.75rem;
  height: 7.25rem;
  min-height: 7.25rem;
  place-items: center;
  overflow: hidden;
  border: 1px solid rgb(255 255 255 / 0.08);
  border-radius: var(--radius-sm);
  background: var(--surface-control);
  transition: border-color 140ms ease, box-shadow 140ms ease;
}

.hot-card:hover .hot-card__image-wrap {
  border-color: rgb(249 115 22 / 0.28);
  box-shadow: 0 0 0 1px rgb(249 115 22 / 0.1);
}

.hot-card__image-wrap :deep(.market-image__asset),
.hot-card__image-wrap :deep(.market-image__preview) {
  object-fit: cover;
}

.hot-card__body {
  display: grid;
  position: relative;
  z-index: 2;
  min-width: 0;
  align-content: start;
  gap: 0.3rem;
  padding-block: 0.36rem;
}

.hot-card__topline,
.hot-card__identity {
  display: flex;
  min-width: 0;
  flex-wrap: wrap;
  gap: 0.3rem 0.45rem;
  color: var(--color-text-muted);
  font-size: 0.73rem;
  line-height: 1.25;
}

.hot-card__topline span,
.hot-card__identity span {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.hot-card__topline span + span::before,
.hot-card__identity span + span::before {
  color: var(--color-text-subtle);
  content: '· ';
}

.hot-card__name {
  display: -webkit-box;
  margin: 0;
  overflow: hidden;
  color: var(--color-text);
  font-size: 1rem;
  font-weight: 800;
  letter-spacing: 0;
  line-height: 1.24;
  -webkit-box-orient: vertical;
  -webkit-line-clamp: 2;
}

.hot-card__metrics {
  display: grid;
  grid-template-columns: repeat(5, minmax(0, 1fr));
  gap: 0.4rem;
  margin-top: 0.16rem;
}

.hot-card__metric {
  display: grid;
  min-width: 0;
  gap: 0.11rem;
  border: 1px solid rgb(255 255 255 / 0.07);
  border-radius: var(--radius-sm);
  background: rgb(255 255 255 / 0.032);
  padding: 0.32rem 0.46rem;
  transition: background 140ms ease, border-color 140ms ease;
}

.hot-card:hover .hot-card__metric,
.hot-card:focus-within .hot-card__metric {
  border-color: rgb(249 115 22 / 0.15);
  background: rgb(8 11 16 / 0.58);
}

.hot-card__metric strong {
  min-width: 0;
  overflow: hidden;
  color: var(--color-text);
  font-size: 0.9rem;
  font-weight: 780;
  line-height: 1.15;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.hot-card__metric span {
  min-width: 0;
  overflow: hidden;
  color: var(--color-text-muted);
  font-size: 0.68rem;
  line-height: 1.15;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.hot-card__metric--price {
  border-color: rgb(249 115 22 / 0.17);
  background:
    linear-gradient(180deg, rgb(249 115 22 / 0.08), rgb(249 115 22 / 0.025)),
    rgb(255 255 255 / 0.032);
}

.hot-card:hover .hot-card__metric--price,
.hot-card:focus-within .hot-card__metric--price {
  border-color: rgb(249 115 22 / 0.24);
  background:
    linear-gradient(180deg, rgb(249 115 22 / 0.055), rgb(249 115 22 / 0.018)),
    rgb(8 11 16 / 0.54);
}

.hot-card__rating--positive {
  color: var(--state-success) !important;
}

.hot-card__rating--warning {
  color: var(--state-warning) !important;
}

.hot-card__rating--negative {
  color: var(--state-danger) !important;
}

.hot-card__rating--neutral {
  color: var(--color-text-muted) !important;
}

.hot-card__side {
  display: grid;
  position: relative;
  z-index: 2;
  align-content: center;
  align-self: start;
  justify-self: end;
  box-sizing: border-box;
  height: calc(100% - 0.72rem);
  width: 100%;
  min-height: 0;
  gap: 0.42rem;
  border: 1px solid var(--flame-edge);
  border-radius: var(--radius-sm);
  background:
    linear-gradient(180deg, rgb(249 115 22 / 0.11), rgb(249 115 22 / 0.03)),
    rgb(255 255 255 / 0.028);
  margin-top: 0.1rem;
  padding: 0.52rem 0.72rem;
  transition: background 140ms ease, border-color 140ms ease;
}

.hot-card:hover .hot-card__side,
.hot-card:focus-within .hot-card__side {
  border-color: color-mix(in srgb, var(--flame-edge) 76%, rgb(251 146 60 / 0.34));
  background:
    linear-gradient(180deg, rgb(249 115 22 / 0.075), rgb(249 115 22 / 0.02)),
    rgb(8 11 16 / 0.62);
}

.hot-card__score {
  display: grid;
  justify-items: center;
  gap: 0.16rem;
  text-align: center;
}

.hot-card__score strong {
  color: var(--accent-ember-text-strong);
  font-size: clamp(1.16rem, 1.45vw, 1.38rem);
  font-weight: 880;
  line-height: 1;
}

.hot-card__score span {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
  color: var(--color-text-muted);
  font-size: 0.72rem;
  line-height: 1.2;
}

.hot-card__confidence {
  display: inline-flex;
  width: max-content;
  max-width: 100%;
  align-items: center;
  gap: 0.45rem;
  border: 1px solid rgb(255 255 255 / 0.08);
  border-radius: var(--radius-sm);
  background: rgb(255 255 255 / 0.035);
  color: var(--color-text-muted);
  font-size: 0.74rem;
  line-height: 1.2;
  padding: 0.34rem 0.5rem;
}

.hot-card__confidence strong {
  color: var(--color-text);
  font-size: 0.9rem;
  line-height: 1;
}

.hot-card__confidence span {
  color: var(--color-text-muted);
}

.hot-card__why-link {
  justify-self: center;
  width: max-content;
  max-width: 100%;
  border: 0;
  background: transparent;
  color: var(--operator-link);
  cursor: pointer;
  font: inherit;
  font-size: var(--operator-body-size);
  font-weight: 820;
  line-height: 1.2;
  padding: 0.04rem 0;
  transition:
    color 120ms ease;
}

.hot-card__why-link:hover,
.hot-card__why-link:focus-visible {
  color: var(--operator-link-hover);
  text-decoration: underline;
  text-underline-offset: 0.18rem;
  outline: none;
}

.hot-card__details {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: var(--space-2);
  max-width: 100%;
  border-top: 1px solid rgb(249 115 22 / 0.16);
  background: rgb(4 7 12 / 0.34);
  padding: var(--space-3);
}

.hot-card__details-section {
  display: grid;
  align-content: start;
  min-width: 0;
  gap: var(--space-2);
  border: 1px solid rgb(249 115 22 / 0.15);
  border-radius: var(--radius-sm);
  background: rgb(255 255 255 / 0.03);
  padding: var(--space-3);
}

.hot-card__details-section--wide {
  grid-column: 1 / -1;
}

.hot-card__details-section h4 {
  margin: 0;
  color: var(--color-text);
  font-size: 0.8rem;
  font-weight: 780;
}

.hot-card__details-section strong {
  display: -webkit-box;
  overflow: hidden;
  color: var(--color-text);
  font-size: 0.82rem;
  line-height: 1.35;
  -webkit-box-orient: vertical;
  -webkit-line-clamp: 2;
}

.hot-card__details-section p {
  display: -webkit-box;
  margin: 0;
  overflow: hidden;
  color: var(--color-text-muted);
  font-size: 0.78rem;
  line-height: 1.48;
  -webkit-box-orient: vertical;
  -webkit-line-clamp: 4;
}

.hot-card__details-actions {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: flex-start;
  gap: var(--space-2);
  margin-top: 0;
}

.hot-card__details-footer {
  display: flex;
  grid-column: 1 / -1;
  justify-content: flex-start;
  border-top: 1px solid rgb(249 115 22 / 0.12);
  padding-top: var(--space-2);
}

.hot-card__locate {
  border: 1px solid rgb(249 115 22 / 0.28);
  border-radius: var(--radius-sm);
  background:
    linear-gradient(180deg, rgb(249 115 22 / 0.12), rgb(249 115 22 / 0.035)),
    rgb(255 255 255 / 0.025);
  color: var(--accent-ember-text-strong);
  cursor: pointer;
  font: inherit;
  font-size: 0.78rem;
  font-weight: 760;
  padding: 0.42rem 0.68rem;
}

.hot-card__locate:hover,
.hot-card__locate:focus-visible {
  border-color: rgb(251 146 60 / 0.46);
  color: rgb(255 214 170);
  outline: none;
}

.hot-card__factor-list {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
}

.hot-card__factor {
  display: inline-grid;
  max-width: 100%;
  gap: 0.14rem;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  background: rgb(255 255 255 / 0.035);
  color: var(--color-text-muted);
  padding: 0.38rem 0.5rem;
  font-size: 0.74rem;
  line-height: 1.2;
}

.hot-card__factor strong,
.hot-card__factor span {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.hot-card__factor strong {
  color: var(--color-text);
}

.hot-card__factor--positive {
  border-color: var(--state-success-border);
  background: var(--state-success-soft);
  color: var(--state-success-text);
}

.hot-card__factor--negative {
  border-color: var(--state-danger-border);
  background: var(--state-danger-soft);
  color: var(--state-danger);
}

.hot-card__calm-note {
  border-left: 2px solid var(--state-warning-border);
  padding-left: var(--space-2);
}

:global([data-theme='ash'] .hot-products__showcase) {
  padding-top: var(--space-3);
}

:global([data-theme='ash'] .hot-card) {
  border-color: var(--accent-ember-border);
  background:
    linear-gradient(180deg, var(--accent-ember-soft), transparent 62%),
    var(--background-card-soft);
  box-shadow: var(--shadow-panel);
}

:global([data-theme='ash'] .hot-card::after) {
  border-color: color-mix(in srgb, var(--accent-ember-border) 72%, transparent);
  background: linear-gradient(90deg, var(--accent-ember-soft), transparent 5.5rem);
  opacity: 0.86;
}

:global([data-theme='ash'] .hot-card:hover) {
  border-color: var(--accent-primary-hover-border);
}

:global([data-theme='ash'] .hot-card__content) {
  background:
    linear-gradient(180deg, rgb(255 255 255 / 0.72), transparent 68%),
    var(--surface-panel-raised);
}

:global([data-theme='ash'] .hot-card__main::before) {
  background: linear-gradient(90deg, var(--accent-ember-soft), rgb(255 255 255 / 0.42) 38%, transparent 76%);
}

:global([data-theme='ash'] .hot-card__image-wrap),
:global([data-theme='ash'] .hot-card__metric),
:global([data-theme='ash'] .hot-card__confidence),
:global([data-theme='ash'] .hot-card__factor) {
  border-color: var(--color-border);
  background: var(--surface-control);
}

:global([data-theme='ash'] .hot-card:hover .hot-card__metric),
:global([data-theme='ash'] .hot-card:focus-within .hot-card__metric) {
  border-color: var(--accent-ember-border);
  background: var(--color-surface-hover);
}

:global([data-theme='ash'] .hot-card__metric--price),
:global([data-theme='ash'] .hot-card:hover .hot-card__metric--price),
:global([data-theme='ash'] .hot-card:focus-within .hot-card__metric--price) {
  border-color: var(--accent-ember-border);
  background:
    linear-gradient(180deg, var(--accent-ember-soft), transparent 82%),
    var(--surface-control);
}

:global([data-theme='ash'] .hot-card__side) {
  border-color: var(--accent-ember-border);
  background:
    linear-gradient(180deg, var(--accent-ember-soft), transparent 74%),
    var(--surface-control-raised);
}

:global([data-theme='ash'] .hot-card:hover .hot-card__side),
:global([data-theme='ash'] .hot-card:focus-within .hot-card__side) {
  border-color: var(--accent-primary-hover-border);
  background:
    linear-gradient(180deg, var(--accent-ember-hover-bg), transparent 78%),
    var(--color-surface-hover);
}

:global([data-theme='ash'] .hot-card__locate) {
  color: var(--operator-link);
}

:global([data-theme='ash'] .hot-card__details) {
  border-top-color: var(--accent-ember-border);
  background: var(--background-card-soft);
}

:global([data-theme='ash'] .hot-card__details-section) {
  border-color: var(--color-border);
  background: var(--surface-panel-raised);
}

:global([data-theme='ash'] .hot-card__locate) {
  border-color: var(--accent-ember-border);
  background:
    linear-gradient(180deg, var(--accent-ember-soft), transparent 80%),
    var(--surface-control-raised);
}

@media (max-width: 1180px) {
  .hot-card__main {
    grid-template-columns: 7.5rem minmax(0, 1fr);
  }

  .hot-card__side {
    grid-column: 1 / -1;
    grid-template-columns: repeat(3, minmax(0, 1fr));
    align-items: center;
  }

  .hot-card__metrics {
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }
}

@media (max-width: 760px) {
  .hot-products__showcase {
    padding: var(--space-3) 0 var(--space-3) 2.35rem;
  }

  .hot-card__flame {
    left: -2.35rem;
    width: 3.95rem;
  }

  .hot-card__ascii-fire {
    inset: 0;
    font-size: 0.42rem;
  }

  .hot-card__main,
  .hot-card__side,
  .hot-card__details {
    grid-template-columns: 1fr;
  }

  .hot-card__image-wrap {
    width: 5.75rem;
    height: 7.25rem;
    min-height: 7.25rem;
  }

  .hot-card__metrics {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (prefers-reduced-motion: reduce) {
  .hot-card,
  .hot-card__ascii-fire,
  .hot-products__pulse {
    animation: none;
  }

  .hot-card,
  .hot-card__ascii-fire,
  .hot-card__why-link,
  .hot-card__locate {
    transition: none;
  }
}

@keyframes hot-pulse {
  to {
    box-shadow: 0 0 0 0.55rem rgb(249 115 22 / 0);
  }
}

@keyframes hot-card-breathe {
  0%,
  100% {
    box-shadow:
      inset 0 1px 0 rgb(255 255 255 / 0.045),
      0 15px 30px rgb(0 0 0 / 0.24),
      0 0 10px rgb(185 28 28 / 0.04);
  }

  50% {
    box-shadow:
      inset 0 1px 0 rgb(255 255 255 / 0.045),
      0 15px 30px rgb(0 0 0 / 0.24),
      0 0 14px rgb(185 28 28 / 0.055);
  }
}

</style>
