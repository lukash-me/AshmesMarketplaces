<script setup lang="ts">
import { computed, onMounted, onUnmounted, reactive, ref } from 'vue';
import { ChevronDown, ChevronUp, Pencil, Plus, RefreshCw, Search, Trash2, X } from 'lucide-vue-next';

import HelpTooltip from '@/shared/ui/HelpTooltip.vue';
import PageHeader from '@/widgets/PageHeader.vue';

import {
  createParserAdminInstanceConfiguration,
  createParserAdminProxy,
  deleteParserAdminInstanceConfiguration,
  deleteParserAdminProxy,
  getParserAdminInstanceConfigurations,
  getParserAdminInstances,
  getParserAdminJournal,
  getParserAdminProxies,
  getParserRunRollbackPreview,
  launchParserAdminInstance,
  rollbackParserRun,
  searchWildberriesLeaves,
  updateParserAdminInstanceConfiguration,
  updateParserAdminProxy
} from './parserAdminMonitoring.api';
import type {
  ParserAdminInstance,
  ParserAdminProxyRun,
  ParserAdminProxyRunJournal,
  ParserInstanceConfiguration,
  ParserInstanceConfigurationSavePayload,
  ParserLaunchRequest,
  ParserLaunchSavePayload,
  ParserProxy,
  ParserProxySavePayload,
  ParserRunRollbackPreview,
  ParserRunRollbackResponse,
  WbCategoryLeaf
} from './parserAdminMonitoring.types';
import { parserAdminTexts, parserStatusLabel } from './parserAdminMonitoring.texts';

type ViewMode = 'instances' | 'journal' | 'proxies';
type LaunchMode = 'limited_all' | 'full_all' | 'check_proxy';
const ACTIVE_POLLING_INTERVAL_MS = 3000;

const noNicheLabel = parserAdminTexts.form.noNiche;

const viewModes: Array<{ key: ViewMode; label: string }> = [
  { key: 'instances', label: parserAdminTexts.tabs.instances },
  { key: 'journal', label: parserAdminTexts.tabs.journal },
  { key: 'proxies', label: parserAdminTexts.tabs.proxies }
];

const activeMode = ref<ViewMode>('instances');
const loading = ref(false);
const savingProxy = ref(false);
const savingInstance = ref(false);
const launching = ref(false);
const error = ref('');
const proxyFormError = ref('');
const instanceFormError = ref('');
const launchFormError = ref('');
const launchMessage = ref('');
const rollbackError = ref('');
const rollbackMessage = ref('');
const categorySearchError = ref('');
const instances = ref<ParserAdminInstance[]>([]);
const instanceConfigurations = ref<ParserInstanceConfiguration[]>([]);
const journal = ref<ParserAdminProxyRunJournal[]>([]);
const proxies = ref<ParserProxy[]>([]);
const categoryOptions = ref<WbCategoryLeaf[]>([]);
const categoryDropdownOpen = ref(false);
const expandedInstanceIds = ref<Set<string>>(new Set());
const proxyModalOpen = ref(false);
const instanceModalOpen = ref(false);
const launchModalOpen = ref(false);
const detailsModalOpen = ref(false);
const rollbackModalOpen = ref(false);
const editingProxy = ref<ParserProxy | null>(null);
const editingInstance = ref<ParserInstanceConfiguration | null>(null);
const launchInstance = ref<ParserAdminInstance | null>(null);
const launchMode = ref<LaunchMode>('limited_all');
const launchBatchLimit = ref(3);
const launchProxyKey = ref('');
const detailsJournalRow = ref<ParserAdminProxyRunJournal | null>(null);
const rollbackJournalRow = ref<ParserAdminProxyRunJournal | null>(null);
const rollbackPreview = ref<ParserRunRollbackPreview | null>(null);
const rollbackResult = ref<ParserRunRollbackResponse | null>(null);
const loadingRollbackPreview = ref(false);
const rollingBack = ref(false);
const pollingTimerId = ref<number | null>(null);

const proxyForm = reactive({
  ip: '',
  httpPort: 0,
  socksPort: 0,
  login: '',
  password: '',
  wbCategoryId: null as number | null,
  categoryQuery: ''
});

const instanceForm = reactive({
  displayName: '',
  proxyIds: [] as string[]
});

const hasInstances = computed(() => instances.value.length > 0);
const hasJournal = computed(() => journal.value.length > 0);
const hasProxies = computed(() => proxies.value.length > 0);
const hasActiveParserProcess = computed(() => instances.value.some(isInstanceLaunchActive));
const showInitialLoading = computed(() => loading.value && !hasInstances.value && !hasJournal.value && !hasProxies.value);

onMounted(() => {
  void loadAll();
});

onUnmounted(() => {
  stopActivePolling();
});

async function loadAll(): Promise<void> {
  if (loading.value) {
    return;
  }

  loading.value = true;
  error.value = '';
  try {
    const [instanceItems, journalItems, proxyItems, configItems] = await Promise.all([
      getParserAdminInstances(),
      getParserAdminJournal({ page: 1, pageSize: 100 }),
      getParserAdminProxies(),
      getParserAdminInstanceConfigurations()
    ]);
    instances.value = instanceItems;
    instanceConfigurations.value = configItems;
    journal.value = journalItems;
    proxies.value = proxyItems;
  } catch (exception) {
    error.value = getErrorMessage(exception);
  } finally {
    loading.value = false;
    syncActivePolling();
  }
}

function startActivePolling(): void {
  if (pollingTimerId.value !== null || !hasActiveParserProcess.value) {
    return;
  }

  pollingTimerId.value = window.setInterval(() => {
    if (loading.value) {
      return;
    }

    if (!hasActiveParserProcess.value) {
      stopActivePolling();
      return;
    }

    void loadAll();
  }, ACTIVE_POLLING_INTERVAL_MS);
}

function stopActivePolling(): void {
  if (pollingTimerId.value === null) {
    return;
  }

  window.clearInterval(pollingTimerId.value);
  pollingTimerId.value = null;
}

function syncActivePolling(): void {
  if (hasActiveParserProcess.value) {
    startActivePolling();
  } else {
    stopActivePolling();
  }
}

function toggleInstance(id: string): void {
  const next = new Set(expandedInstanceIds.value);
  if (next.has(id)) {
    next.delete(id);
  } else {
    next.add(id);
  }
  expandedInstanceIds.value = next;
}

function isExpanded(id: string): boolean {
  return expandedInstanceIds.value.has(id);
}

function instanceConfigFor(instance: ParserAdminInstance): ParserInstanceConfiguration | null {
  return instanceConfigurations.value.find((item) => item.parserInstanceId === instance.parserInstanceId) ?? null;
}

function openCreateInstanceModal(): void {
  editingInstance.value = null;
  instanceForm.displayName = '';
  instanceForm.proxyIds = [];
  instanceFormError.value = '';
  instanceModalOpen.value = true;
}

function openEditInstanceModal(instance: ParserInstanceConfiguration): void {
  editingInstance.value = instance;
  instanceForm.displayName = instance.displayName;
  instanceForm.proxyIds = instance.proxies.map((proxy) => proxy.proxyId);
  instanceFormError.value = '';
  instanceModalOpen.value = true;
}

function closeInstanceModal(): void {
  instanceModalOpen.value = false;
  instanceFormError.value = '';
}

function isProxySelected(proxyId: string): boolean {
  return instanceForm.proxyIds.includes(proxyId);
}

function isProxyUnavailableForInstance(proxy: ParserProxy): boolean {
  return Boolean(
    proxy.assignedInstance &&
      (!editingInstance.value || proxy.assignedInstance.instanceConfigurationId !== editingInstance.value.id)
  );
}

function toggleInstanceProxy(proxy: ParserProxy): void {
  if (isProxyUnavailableForInstance(proxy)) {
    return;
  }

  if (isProxySelected(proxy.id)) {
    instanceForm.proxyIds = instanceForm.proxyIds.filter((id) => id !== proxy.id);
  } else {
    instanceForm.proxyIds = [...instanceForm.proxyIds, proxy.id];
  }
}

async function saveInstance(): Promise<void> {
  if (!instanceForm.displayName.trim()) {
    instanceFormError.value = parserAdminTexts.validation.instanceName;
    return;
  }

  savingInstance.value = true;
  try {
    const payload: ParserInstanceConfigurationSavePayload = {
      displayName: instanceForm.displayName.trim(),
      proxyIds: instanceForm.proxyIds
    };
    if (editingInstance.value) {
      await updateParserAdminInstanceConfiguration(editingInstance.value.id, payload);
    } else {
      await createParserAdminInstanceConfiguration(payload);
    }
    await loadAll();
    closeInstanceModal();
  } catch (exception) {
    instanceFormError.value = getErrorMessage(exception);
  } finally {
    savingInstance.value = false;
  }
}

async function disableInstance(instance: ParserInstanceConfiguration): Promise<void> {
  if (!window.confirm(`${parserAdminTexts.actions.disable} ${instance.displayName}?`)) {
    return;
  }

  try {
    await deleteParserAdminInstanceConfiguration(instance.id);
    await loadAll();
  } catch (exception) {
    error.value = getErrorMessage(exception);
  }
}

function openEditInstanceFromCard(instance: ParserAdminInstance): void {
  const config = instanceConfigFor(instance);
  if (config) {
    openEditInstanceModal(config);
  }
}

function disableInstanceFromCard(instance: ParserAdminInstance): void {
  const config = instanceConfigFor(instance);
  if (config) {
    void disableInstance(config);
  }
}

function launchableProxiesFor(instance: ParserAdminInstance | null): ParserAdminProxyRun[] {
  if (!instance) {
    return [];
  }

  return instance.proxies.filter((proxy) => proxy.sourceSubcategory && proxy.status !== 'disabled');
}

function isProxyLaunchActive(proxy: ParserAdminProxyRun): boolean {
  return proxy.status === 'queued' ||
    proxy.status === 'running' ||
    ['queued', 'wb_preflight', 'ranges', 'download'].includes(proxy.phase);
}

function isInstanceLaunchActive(instance: ParserAdminInstance): boolean {
  return instance.proxies.some(isProxyLaunchActive);
}

function openLaunchModal(instance: ParserAdminInstance, mode: LaunchMode, proxyKey?: string): void {
  if (mode === 'check_proxy') {
    const proxy = instance.proxies.find((item) => item.proxyKey === proxyKey);
    if (proxy && isProxyLaunchActive(proxy)) {
      return;
    }
  } else if (isInstanceLaunchActive(instance)) {
    return;
  }

  launchInstance.value = instance;
  launchMode.value = mode;
  launchBatchLimit.value = 3;
  launchProxyKey.value = proxyKey ?? launchableProxiesFor(instance)[0]?.proxyKey ?? '';
  launchFormError.value = '';
  launchModalOpen.value = true;
}

function closeLaunchModal(): void {
  launchModalOpen.value = false;
  launchFormError.value = '';
}

function launchTitle(): string {
  if (launchMode.value === 'full_all') {
    return parserAdminTexts.launch.fullTitle;
  }
  if (launchMode.value === 'check_proxy') {
    return parserAdminTexts.launch.proxyTitle;
  }

  return parserAdminTexts.launch.limitedTitle;
}

async function submitLaunch(): Promise<void> {
  const config = launchInstance.value ? instanceConfigFor(launchInstance.value) : null;
  if (!launchInstance.value || !config) {
    launchFormError.value = parserAdminTexts.fallbackError;
    return;
  }

  if (launchMode.value !== 'full_all' && (!Number(launchBatchLimit.value) || launchBatchLimit.value < 1)) {
    launchFormError.value = parserAdminTexts.validation.batchLimit;
    return;
  }

  if (launchMode.value === 'check_proxy' && !launchProxyKey.value) {
    launchFormError.value = parserAdminTexts.validation.proxy;
    return;
  }

  const payload: ParserLaunchSavePayload = {
    mode: launchMode.value,
    batchLimit: launchMode.value === 'full_all' ? null : Number(launchBatchLimit.value),
    proxyKey: launchMode.value === 'check_proxy' ? launchProxyKey.value : null
  };

  launching.value = true;
  launchFormError.value = '';
  try {
    const launch = await launchParserAdminInstance(config.id, payload);
    applyQueuedLaunchProjection(launch);
    syncActivePolling();
    closeLaunchModal();
    launchMessage.value = parserAdminTexts.launch.queued;
    window.setTimeout(() => {
      launchMessage.value = '';
    }, 5000);
    void loadAll();
  } catch (exception) {
    const message = getErrorMessage(exception);
    launchFormError.value = message.includes('409') ? parserAdminTexts.launch.alreadyRunning : message;
  } finally {
    launching.value = false;
  }
}

function applyQueuedLaunchProjection(launch: ParserLaunchRequest): void {
  const requestedAtUtc = launch.requestedAtUtc;
  const plannedProductsCount = launch.mode === 'full_all' ? 0 : Math.max(0, launch.batchLimit ?? 0) * 100;
  instances.value = instances.value.map((instance) => {
    if (instance.parserInstanceId !== launch.parserInstanceId) {
      return instance;
    }

    const proxies = instance.proxies.map((proxy) => {
      if (launch.mode === 'check_proxy' && proxy.proxyKey !== launch.proxyKey) {
        return proxy;
      }

      return {
        ...proxy,
        id: `launch:${launch.id}:${proxy.proxyKey}`,
        externalProxyRunId: '',
        status: 'queued',
        phase: 'queued',
        plannedProductsCount,
        downloadedProductsCount: 0,
        plannedRangesCount: 0,
        completedRangesCount: 0,
        rangeProgressPercent: 0,
        rangeChecksCount: 0,
        finalRangesCount: 0,
        emptyRangesCount: 0,
        splitRangesCount: 0,
        progressPercent: 0,
        startedAtUtc: requestedAtUtc,
        lastHeartbeatAtUtc: requestedAtUtc,
        lastLogAtUtc: null,
        finishedAtUtc: null,
        runtimeMinutes: 0,
        productsPerSecond: 0,
        rangesPerSecond: 0,
        error: null
      };
    });
    const planned = proxies.reduce((sum, proxy) => sum + proxy.plannedProductsCount, 0);
    const downloaded = proxies.reduce((sum, proxy) => sum + proxy.downloadedProductsCount, 0);

    return {
      ...instance,
      proxies,
      runningProxiesCount: proxies.filter(isProxyLaunchActive).length,
      plannedProductsCount: planned,
      downloadedProductsCount: downloaded,
      progressPercent: planned > 0 ? Math.round((downloaded / planned) * 1000) / 10 : 0,
      runtimeMinutes: 0,
      lastHeartbeatUtc: requestedAtUtc
    };
  });
}

function openDetailsModal(item: ParserAdminProxyRunJournal): void {
  detailsJournalRow.value = item;
  detailsModalOpen.value = true;
}

function closeDetailsModal(): void {
  detailsModalOpen.value = false;
  detailsJournalRow.value = null;
}

async function openRollbackModal(item: ParserAdminProxyRunJournal): Promise<void> {
  rollbackJournalRow.value = item;
  rollbackPreview.value = null;
  rollbackResult.value = null;
  rollbackError.value = '';
  rollbackModalOpen.value = true;
  loadingRollbackPreview.value = true;
  try {
    rollbackPreview.value = await getParserRunRollbackPreview(item.id);
  } catch (exception) {
    rollbackError.value = getErrorMessage(exception);
  } finally {
    loadingRollbackPreview.value = false;
  }
}

function closeRollbackModal(): void {
  rollbackModalOpen.value = false;
  rollbackError.value = '';
  rollbackPreview.value = null;
  rollbackResult.value = null;
  rollbackJournalRow.value = null;
}

async function submitRollback(): Promise<void> {
  if (!rollbackJournalRow.value || !rollbackPreview.value?.canRollback) {
    return;
  }

  rollingBack.value = true;
  rollbackError.value = '';
  try {
    rollbackResult.value = await rollbackParserRun(rollbackJournalRow.value.id);
    if (rollbackPreview.value) {
      rollbackPreview.value = {
        ...rollbackPreview.value,
        canRollback: false,
        message: rollbackResult.value.message
      };
    }
    rollbackMessage.value = parserAdminTexts.rollback.success;
    window.setTimeout(() => {
      rollbackMessage.value = '';
    }, 5000);
    await loadAll();
  } catch (exception) {
    rollbackError.value = getErrorMessage(exception);
  } finally {
    rollingBack.value = false;
  }
}

function openCreateProxyModal(): void {
  editingProxy.value = null;
  resetProxyForm();
  proxyModalOpen.value = true;
}

function openEditProxyModal(proxy: ParserProxy): void {
  editingProxy.value = proxy;
  proxyForm.ip = proxy.ip;
  proxyForm.httpPort = proxy.httpPort;
  proxyForm.socksPort = proxy.socksPort;
  proxyForm.login = proxy.login;
  proxyForm.password = '';
  proxyForm.wbCategoryId = proxy.assignment?.wbCategoryId ?? null;
  proxyForm.categoryQuery = proxy.assignment?.sourcePath ?? noNicheLabel;
  categoryOptions.value = proxy.assignment
    ? [
        {
          id: proxy.assignment.wbCategoryId,
          name: proxy.assignment.sourceSubcategory,
          sourceCategory: proxy.assignment.sourceCategory,
          sourceSubcategory: proxy.assignment.sourceSubcategory,
          path: proxy.assignment.sourcePath,
          searchQuery: proxy.assignment.searchQuery,
          parentId: null,
          isLeaf: true,
          level: 0
        }
      ]
    : [];
  proxyFormError.value = '';
  categorySearchError.value = '';
  categoryDropdownOpen.value = false;
  proxyModalOpen.value = true;
}

function closeProxyModal(): void {
  proxyModalOpen.value = false;
  proxyFormError.value = '';
  categoryDropdownOpen.value = false;
}

function resetProxyForm(): void {
  proxyForm.ip = '';
  proxyForm.httpPort = 0;
  proxyForm.socksPort = 0;
  proxyForm.login = '';
  proxyForm.password = '';
  proxyForm.wbCategoryId = null;
  proxyForm.categoryQuery = '';
  categoryOptions.value = [];
  proxyFormError.value = '';
  categorySearchError.value = '';
  categoryDropdownOpen.value = false;
}

async function onCategoryQueryInput(): Promise<void> {
  proxyForm.wbCategoryId = null;
  categoryDropdownOpen.value = true;
  await loadCategoryOptions();
}

async function loadCategoryOptions(): Promise<void> {
  const query = proxyForm.categoryQuery.trim();
  categorySearchError.value = '';
  if (query.length < 2 || query === noNicheLabel) {
    categoryOptions.value = [];
    return;
  }

  try {
    categoryOptions.value = await searchWildberriesLeaves(query);
  } catch (exception) {
    categorySearchError.value = getErrorMessage(exception);
  }
}

function selectCategory(category: WbCategoryLeaf | null): void {
  if (!category) {
    proxyForm.wbCategoryId = null;
    proxyForm.categoryQuery = noNicheLabel;
    categoryDropdownOpen.value = false;
    return;
  }

  proxyForm.wbCategoryId = category.id;
  proxyForm.categoryQuery = category.path;
  categoryOptions.value = [category];
  categoryDropdownOpen.value = false;
}

async function saveProxy(): Promise<void> {
  proxyFormError.value = validateProxyForm();
  if (proxyFormError.value) {
    return;
  }

  savingProxy.value = true;
  try {
    const payload: ParserProxySavePayload = {
      ip: proxyForm.ip.trim(),
      httpPort: Number(proxyForm.httpPort),
      socksPort: Number(proxyForm.socksPort),
      login: proxyForm.login.trim(),
      wbCategoryId: proxyForm.wbCategoryId
    };
    if (proxyForm.password.trim()) {
      payload.password = proxyForm.password;
    }
    if (editingProxy.value) {
      payload.assignmentEnabled = proxyForm.wbCategoryId !== null;
      payload.status = editingProxy.value.status;
      await updateParserAdminProxy(editingProxy.value.id, payload);
    } else {
      payload.password = proxyForm.password;
      await createParserAdminProxy(payload);
    }

    await loadAll();
    closeProxyModal();
  } catch (exception) {
    proxyFormError.value = getErrorMessage(exception);
  } finally {
    savingProxy.value = false;
  }
}

async function disableProxy(proxy: ParserProxy): Promise<void> {
  if (!window.confirm(`${parserAdminTexts.actions.disable} ${proxy.key}?`)) {
    return;
  }

  try {
    await deleteParserAdminProxy(proxy.id);
    await loadAll();
  } catch (exception) {
    error.value = getErrorMessage(exception);
  }
}

function validateProxyForm(): string {
  if (!proxyForm.ip.trim()) {
    return parserAdminTexts.validation.ip;
  }
  if (!Number(proxyForm.httpPort) || proxyForm.httpPort < 1 || proxyForm.httpPort > 65535) {
    return parserAdminTexts.validation.httpPort;
  }
  if (!Number(proxyForm.socksPort) || proxyForm.socksPort < 1 || proxyForm.socksPort > 65535) {
    return parserAdminTexts.validation.socksPort;
  }
  if (!proxyForm.login.trim()) {
    return parserAdminTexts.validation.login;
  }
  if (!editingProxy.value && !proxyForm.password.trim()) {
    return parserAdminTexts.validation.password;
  }
  const categoryText = proxyForm.categoryQuery.trim();
  if (categoryText && categoryText !== noNicheLabel && proxyForm.wbCategoryId === null) {
    return parserAdminTexts.validation.niche;
  }
  return '';
}

function getErrorMessage(exception: unknown): string {
  if (typeof exception === 'object' && exception && 'message' in exception) {
    return String((exception as { message?: unknown }).message ?? parserAdminTexts.fallbackError);
  }

  return parserAdminTexts.fallbackError;
}

function formatDate(value: string | null | undefined): string {
  if (!value) {
    return parserAdminTexts.values.dash;
  }

  return new Intl.DateTimeFormat('ru-RU', {
    day: '2-digit',
    month: '2-digit',
    year: '2-digit',
    hour: '2-digit',
    minute: '2-digit'
  }).format(new Date(value));
}

function formatDateParts(value: string | null | undefined): { date: string; time: string } | null {
  if (!value) {
    return null;
  }

  const date = new Date(value);
  return {
    date: new Intl.DateTimeFormat('ru-RU', {
      day: '2-digit',
      month: '2-digit',
      year: '2-digit'
    }).format(date),
    time: new Intl.DateTimeFormat('ru-RU', {
      hour: '2-digit',
      minute: '2-digit'
    }).format(date)
  };
}

function formatProxyLastRun(proxy: ParserAdminProxyRun): { date: string; time: string } | null {
  if (proxy.status === 'configured') {
    return null;
  }

  return formatDateParts(proxy.startedAtUtc);
}

function formatDuration(minutes: number | null | undefined): string {
  const safeMinutes = Math.max(0, Math.round(minutes ?? 0));
  const hours = Math.floor(safeMinutes / 60);
  const rest = safeMinutes % 60;
  return `${String(hours).padStart(2, '0')}:${String(rest).padStart(2, '0')}`;
}

function formatProgress(downloaded: number, planned: number): string {
  const downloadedText = formatNumber(downloaded);
  if (!planned || planned <= 0) {
    return `${downloadedText}/${parserAdminTexts.values.dash}`;
  }

  return `${downloadedText}/${formatNumber(planned)}`;
}

function formatPercent(value: number | null | undefined): string {
  return `${formatNumber(value ?? 0, 1)}%`;
}

function formatRate(value: number | null | undefined): string {
  return formatNumber(value ?? 0, 2);
}

function formatLastActivity(item: ParserAdminProxyRun | ParserAdminProxyRunJournal): { date: string; time: string } | null {
  return formatDateParts(item.lastLogAtUtc ?? null);
}

function formatProxyIpTooltip(proxy: ParserAdminProxyRun): string {
  return proxy.egressIp ? `IP: ${proxy.egressIp}` : parserAdminTexts.values.ipUnavailable;
}

function formatRangeCounters(item: ParserAdminProxyRun | ParserAdminProxyRunJournal): string {
  const checked = Math.max(0, item.rangeChecksCount ?? item.completedRangesCount ?? 0);
  const final = Math.max(0, item.finalRangesCount ?? item.completedRangesCount ?? 0);

  if (item.phase === 'ranges' && checked === 0 && final === 0) {
    return parserAdminTexts.values.priceBounds;
  }

  if (item.phase === 'ranges' || item.status === 'failed') {
    return `${parserAdminTexts.values.rangeChecks}: ${formatNumber(checked)}, ${parserAdminTexts.values.finalRanges}: ${formatNumber(final)}`;
  }

  return `${parserAdminTexts.values.finalRanges}: ${formatNumber(final)}`;
}

function formatNumber(value: number, maximumFractionDigits = 0): string {
  return new Intl.NumberFormat('ru-RU', { maximumFractionDigits }).format(value);
}

function formatJournalEffectCount(item: ParserAdminProxyRunJournal, value: number): string {
  return item.hasProductEffectsLedger ? formatNumber(value) : parserAdminTexts.values.dash;
}

function statusLabel(item: Pick<ParserAdminProxyRun | ParserAdminProxyRunJournal, 'status' | 'phase'>): string {
  return parserStatusLabel(item);
}

function statusClass(item: Pick<ParserAdminProxyRun | ParserAdminProxyRunJournal, 'status' | 'phase'>): string {
  if (item.status === 'queued' || item.phase === 'queued') {
    return 'parser-admin__status--queued';
  }
  if (item.status === 'running' && item.phase === 'wb_preflight') {
    return 'parser-admin__status--preflight';
  }
  if (item.status === 'running' && item.phase === 'ranges') {
    return 'parser-admin__status--ranges';
  }
  if (item.status === 'running' && item.phase === 'download') {
    return 'parser-admin__status--download';
  }

  return `parser-admin__status--${item.status}`;
}
</script>

<template>
  <main class="parser-admin">
    <PageHeader :title="parserAdminTexts.pageTitle" />

    <section class="parser-admin__panel">
      <div class="parser-admin__toolbar">
        <div class="parser-admin__tabs" role="tablist" :aria-label="parserAdminTexts.tabsLabel">
          <button
            v-for="mode in viewModes"
            :key="mode.key"
            class="parser-admin__tab"
            :class="{ 'parser-admin__tab--active': activeMode === mode.key }"
            type="button"
            @click="activeMode = mode.key"
          >
            {{ mode.label }}
          </button>
        </div>

        <div class="parser-admin__actions">
          <button
            v-if="activeMode === 'instances'"
            class="parser-admin__refresh"
            type="button"
            @click="openCreateInstanceModal"
          >
            <Plus :size="16" />
            {{ parserAdminTexts.actions.addInstance }}
          </button>
          <button
            v-if="activeMode === 'proxies'"
            class="parser-admin__refresh"
            type="button"
            @click="openCreateProxyModal"
          >
            <Plus :size="16" />
            {{ parserAdminTexts.actions.addProxy }}
          </button>
          <button class="parser-admin__refresh" type="button" :disabled="loading" @click="loadAll">
            <RefreshCw :size="16" />
            {{ parserAdminTexts.actions.refresh }}
          </button>
        </div>
      </div>

      <p v-if="error" class="parser-admin__error">{{ error }}</p>
      <p v-if="launchMessage" class="parser-admin__notice">{{ launchMessage }}</p>
      <p v-if="rollbackMessage" class="parser-admin__notice">{{ rollbackMessage }}</p>
      <p v-if="showInitialLoading" class="parser-admin__loading">{{ parserAdminTexts.loading }}</p>

      <div v-if="activeMode === 'instances'" class="parser-admin__instances">
        <article v-for="instance in instances" :key="instance.id" class="parser-admin__instance">
          <button class="parser-admin__instance-main" type="button" @click="toggleInstance(instance.id)">
            <span class="parser-admin__instance-title">
              <strong>{{ instance.displayName || instance.parserInstanceId }}</strong>
              <span>{{ instance.parserInstanceId }}</span>
            </span>
            <span class="parser-admin__metric">
              <span>{{ parserAdminTexts.metrics.runningProxies }}</span>
              <strong>{{ instance.runningProxiesCount }}/{{ instance.totalProxiesCount }}</strong>
            </span>
            <span class="parser-admin__metric">
              <span>{{ parserAdminTexts.metrics.progress }}</span>
              <strong>{{ formatProgress(instance.downloadedProductsCount, instance.plannedProductsCount) }}</strong>
            </span>
            <span class="parser-admin__metric">
              <span>{{ parserAdminTexts.metrics.completed }}</span>
              <strong>{{ formatPercent(instance.progressPercent) }}</strong>
            </span>
            <span class="parser-admin__metric">
              <span>{{ parserAdminTexts.metrics.runtime }}</span>
              <strong>{{ formatDuration(instance.runtimeMinutes) }}</strong>
            </span>
            <ChevronUp v-if="isExpanded(instance.id)" :size="18" />
            <ChevronDown v-else :size="18" />
          </button>

          <div v-if="isExpanded(instance.id) && instanceConfigFor(instance)" class="parser-admin__instance-actions">
            <div class="parser-admin__action-group">
              <span>{{ parserAdminTexts.actions.management }}</span>
              <button
                class="parser-admin__secondary"
                type="button"
                @click="openEditInstanceFromCard(instance)"
              >
                <Pencil :size="16" />
                {{ parserAdminTexts.actions.edit }}
              </button>
              <button
                class="parser-admin__secondary"
                type="button"
                @click="disableInstanceFromCard(instance)"
              >
                <Trash2 :size="16" />
                {{ parserAdminTexts.actions.disable }}
              </button>
            </div>
            <div class="parser-admin__action-group">
              <span>{{ parserAdminTexts.actions.launch }}</span>
              <button
                class="parser-admin__primary"
                type="button"
                :disabled="isInstanceLaunchActive(instance)"
                @click="openLaunchModal(instance, 'limited_all')"
              >
                {{ parserAdminTexts.actions.launchLimited }}
              </button>
              <button
                class="parser-admin__primary"
                type="button"
                :disabled="isInstanceLaunchActive(instance)"
                @click="openLaunchModal(instance, 'full_all')"
              >
                {{ parserAdminTexts.actions.launchFull }}
              </button>
            </div>
          </div>

          <div v-if="isExpanded(instance.id)" class="parser-admin__proxies">
            <div v-if="instance.proxies.length === 0" class="parser-admin__empty-row">
              {{ parserAdminTexts.empty.proxyRuns }}
            </div>
            <div v-for="proxy in instance.proxies" :key="proxy.id" class="parser-admin__proxy-run">
              <span class="parser-admin__proxy-title">
                <span class="parser-admin__proxy-title-head">
                  <strong class="parser-admin__proxy-name">
                    {{ proxy.proxyKey }}
                    <HelpTooltip :text="formatProxyIpTooltip(proxy)" />
                  </strong>
                  <span>{{ proxy.sourceSubcategory }}</span>
                </span>
                <button
                  class="parser-admin__proxy-check"
                  type="button"
                  :disabled="isProxyLaunchActive(proxy)"
                  @click="openLaunchModal(instance, 'check_proxy', proxy.proxyKey)"
                >
                  {{ parserAdminTexts.actions.launchProxy }}
                </button>
              </span>
              <span class="parser-admin__metric">
                <span>{{ parserAdminTexts.metrics.status }}</span>
                <strong :class="['parser-admin__status', statusClass(proxy)]">
                  {{ statusLabel(proxy) }}
                </strong>
              </span>
              <span class="parser-admin__metric">
                <span>{{ parserAdminTexts.metrics.ranges }}</span>
                <strong>{{ formatRangeCounters(proxy) }}</strong>
              </span>
              <span class="parser-admin__metric">
                <span>{{ parserAdminTexts.metrics.completed }}</span>
                <strong>{{ formatPercent(proxy.progressPercent) }}</strong>
              </span>
              <span class="parser-admin__metric">
                <span>{{ parserAdminTexts.metrics.progress }}</span>
                <strong>{{ formatProgress(proxy.downloadedProductsCount, proxy.plannedProductsCount) }}</strong>
              </span>
              <span class="parser-admin__metric">
                <span>{{ parserAdminTexts.metrics.lastRun }}</span>
                <strong v-if="formatProxyLastRun(proxy)" class="parser-admin__date-stack">
                  <span>{{ formatProxyLastRun(proxy)?.date }}</span>
                  <span>{{ formatProxyLastRun(proxy)?.time }}</span>
                </strong>
                <strong v-else>{{ parserAdminTexts.values.dash }}</strong>
              </span>
              <span class="parser-admin__metric">
                <span>{{ parserAdminTexts.metrics.lastActivity }}</span>
                <strong v-if="formatLastActivity(proxy)" class="parser-admin__date-stack">
                  <span>{{ formatLastActivity(proxy)?.date }}</span>
                  <span>{{ formatLastActivity(proxy)?.time }}</span>
                </strong>
                <strong v-else>{{ parserAdminTexts.values.dash }}</strong>
              </span>
              <span class="parser-admin__metric">
                <span>{{ parserAdminTexts.metrics.runtime }}</span>
                <strong>{{ formatDuration(proxy.runtimeMinutes) }}</strong>
              </span>
              <span class="parser-admin__metric">
                <span>{{ parserAdminTexts.metrics.productsPerSecond }}</span>
                <strong>{{ formatRate(proxy.productsPerSecond) }}</strong>
              </span>
              <span class="parser-admin__metric">
                <span>{{ parserAdminTexts.metrics.rangesPerSecond }}</span>
                <strong>{{ formatRate(proxy.rangesPerSecond) }}</strong>
              </span>
            </div>
          </div>
        </article>

        <div v-if="!loading && !hasInstances" class="parser-admin__empty">
          {{ parserAdminTexts.empty.instances }}
        </div>
      </div>

      <div v-else-if="activeMode === 'journal'" class="parser-admin__table-wrap">
        <table class="parser-admin__table">
          <thead>
            <tr>
              <th>{{ parserAdminTexts.table.proxy }}</th>
              <th>{{ parserAdminTexts.table.status }}</th>
              <th>{{ parserAdminTexts.table.start }}</th>
              <th>{{ parserAdminTexts.table.finish }}</th>
              <th>{{ parserAdminTexts.table.instance }}</th>
              <th>{{ parserAdminTexts.table.downloaded }}</th>
              <th>{{ parserAdminTexts.table.planned }}</th>
              <th>{{ parserAdminTexts.table.time }}</th>
              <th>{{ parserAdminTexts.table.error }}</th>
              <th>{{ parserAdminTexts.table.actions }}</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="item in journal" :key="item.id">
              <td>
                <strong>{{ item.proxyKey }}</strong>
                <span>{{ item.sourceSubcategory }}</span>
              </td>
              <td>
                <strong :class="['parser-admin__status', statusClass(item)]">
                  {{ statusLabel(item) }}
                </strong>
              </td>
              <td>{{ formatDate(item.startedAtUtc) }}</td>
              <td>{{ formatDate(item.finishedAtUtc) }}</td>
              <td>{{ item.parserInstanceId }}</td>
              <td>{{ formatNumber(item.downloadedProductsCount) }}</td>
              <td>{{ formatNumber(item.plannedProductsCount) }}</td>
              <td>{{ formatDuration(item.runtimeMinutes) }}</td>
              <td>{{ item.error || parserAdminTexts.values.dash }}</td>
              <td>
                <div class="parser-admin__row-actions">
                  <button class="parser-admin__secondary" type="button" @click="openDetailsModal(item)">
                    {{ parserAdminTexts.actions.details }}
                  </button>
                  <button class="parser-admin__secondary" type="button" @click="openRollbackModal(item)">
                    {{ parserAdminTexts.actions.rollback }}
                  </button>
                </div>
              </td>
            </tr>
          </tbody>
        </table>

        <div v-if="!loading && !hasJournal" class="parser-admin__empty">
          {{ parserAdminTexts.empty.journal }}
        </div>
      </div>

      <div v-else class="parser-admin__table-wrap">
        <table class="parser-admin__table parser-admin__table--proxies">
          <thead>
            <tr>
              <th>{{ parserAdminTexts.table.key }}</th>
              <th>IP</th>
              <th>HTTP</th>
              <th>SOCKS5</th>
              <th>{{ parserAdminTexts.table.login }}</th>
              <th>{{ parserAdminTexts.table.password }}</th>
              <th>{{ parserAdminTexts.table.niche }}</th>
              <th>{{ parserAdminTexts.table.instance }}</th>
              <th>{{ parserAdminTexts.table.status }}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="proxy in proxies" :key="proxy.id">
              <td><strong>{{ proxy.key }}</strong></td>
              <td>{{ proxy.ip }}</td>
              <td>{{ proxy.httpPort }}</td>
              <td>{{ proxy.socksPort }}</td>
              <td>{{ proxy.login }}</td>
              <td>{{ proxy.hasPassword ? parserAdminTexts.values.passwordSet : parserAdminTexts.values.passwordMissing }}</td>
              <td>
                <strong>{{ proxy.assignment?.sourceSubcategory || noNicheLabel }}</strong>
                <span v-if="proxy.assignment">{{ proxy.assignment.sourcePath }}</span>
              </td>
              <td>
                <strong>{{ proxy.assignedInstance?.displayName || parserAdminTexts.form.unassigned }}</strong>
                <span v-if="proxy.assignedInstance">{{ proxy.assignedInstance.parserInstanceId }}</span>
              </td>
              <td>{{ proxy.status === 'active' ? parserAdminTexts.values.active : parserAdminTexts.values.disabled }}</td>
              <td>
                <div class="parser-admin__row-actions">
                  <button class="parser-admin__icon-button" type="button" :title="parserAdminTexts.actions.edit" @click="openEditProxyModal(proxy)">
                    <Pencil :size="16" />
                  </button>
                  <button class="parser-admin__icon-button" type="button" :title="parserAdminTexts.actions.disable" @click="disableProxy(proxy)">
                    <Trash2 :size="16" />
                  </button>
                </div>
              </td>
            </tr>
          </tbody>
        </table>

        <div v-if="!loading && !hasProxies" class="parser-admin__empty">
          {{ parserAdminTexts.empty.proxies }}
        </div>
      </div>
    </section>

    <div v-if="launchModalOpen" class="parser-admin__modal-backdrop">
      <section class="parser-admin__modal" role="dialog" aria-modal="true">
        <header class="parser-admin__modal-header">
          <h2>{{ launchTitle() }}</h2>
          <button class="parser-admin__icon-button" type="button" @click="closeLaunchModal">
            <X :size="18" />
          </button>
        </header>

        <p v-if="launchFormError" class="parser-admin__error">{{ launchFormError }}</p>

        <div class="parser-admin__proxy-form">
          <p v-if="launchMode === 'full_all'" class="parser-admin__launch-confirmation">
            {{ parserAdminTexts.launch.fullConfirmation }}
          </p>

          <label
            v-if="launchMode === 'check_proxy'"
            class="parser-admin__form-field parser-admin__form-field--wide"
          >
            <span>{{ parserAdminTexts.launch.proxy }}</span>
            <select v-model="launchProxyKey" class="parser-admin__input">
              <option value="" disabled>{{ parserAdminTexts.validation.proxy }}</option>
              <option v-for="proxy in launchableProxiesFor(launchInstance)" :key="proxy.proxyKey" :value="proxy.proxyKey">
                {{ proxy.proxyKey }} - {{ proxy.sourceSubcategory }}
              </option>
            </select>
          </label>

          <label
            v-if="launchMode !== 'full_all'"
            class="parser-admin__form-field parser-admin__form-field--wide"
          >
            <span>{{ parserAdminTexts.launch.batchLimit }}</span>
            <input v-model.number="launchBatchLimit" class="parser-admin__input" type="number" min="1" step="1" />
          </label>
        </div>

        <footer class="parser-admin__modal-footer">
          <button class="parser-admin__secondary" type="button" @click="closeLaunchModal">
            {{ parserAdminTexts.actions.cancel }}
          </button>
          <button class="parser-admin__primary" type="button" :disabled="launching" @click="submitLaunch">
            {{ parserAdminTexts.actions.start }}
          </button>
        </footer>
      </section>
    </div>

    <div v-if="detailsModalOpen && detailsJournalRow" class="parser-admin__modal-backdrop">
      <section class="parser-admin__modal" role="dialog" aria-modal="true">
        <header class="parser-admin__modal-header">
          <h2>{{ parserAdminTexts.details.title }}</h2>
          <button class="parser-admin__icon-button" type="button" @click="closeDetailsModal">
            <X :size="18" />
          </button>
        </header>

        <div class="parser-admin__rollback-preview">
          <div class="parser-admin__rollback-grid parser-admin__rollback-grid--details">
            <span class="parser-admin__metric">
              <span>{{ parserAdminTexts.details.ip }}</span>
              <strong>{{ detailsJournalRow.egressIp || parserAdminTexts.values.dash }}</strong>
            </span>
            <span class="parser-admin__metric">
              <span>{{ parserAdminTexts.details.created }}</span>
              <strong>{{ formatJournalEffectCount(detailsJournalRow, detailsJournalRow.createdProductsCount) }}</strong>
            </span>
            <span class="parser-admin__metric">
              <span>{{ parserAdminTexts.details.updated }}</span>
              <strong>{{ formatJournalEffectCount(detailsJournalRow, detailsJournalRow.updatedProductsCount) }}</strong>
            </span>
          </div>
          <p v-if="!detailsJournalRow.hasProductEffectsLedger" class="parser-admin__error">
            {{ parserAdminTexts.details.missingLedger }}
          </p>
        </div>

        <footer class="parser-admin__modal-footer">
          <button class="parser-admin__secondary" type="button" @click="closeDetailsModal">
            {{ parserAdminTexts.actions.cancel }}
          </button>
        </footer>
      </section>
    </div>

    <div v-if="rollbackModalOpen" class="parser-admin__modal-backdrop">
      <section class="parser-admin__modal" role="dialog" aria-modal="true">
        <header class="parser-admin__modal-header">
          <h2>{{ parserAdminTexts.rollback.title }}</h2>
          <button class="parser-admin__icon-button" type="button" @click="closeRollbackModal">
            <X :size="18" />
          </button>
        </header>

        <p v-if="rollbackError" class="parser-admin__error">{{ rollbackError }}</p>
        <p v-if="loadingRollbackPreview" class="parser-admin__loading">
          {{ parserAdminTexts.rollback.previewLoading }}
        </p>

        <div v-if="rollbackPreview" class="parser-admin__rollback-preview">
          <p>{{ rollbackPreview.message }}</p>
          <div class="parser-admin__rollback-grid">
            <span class="parser-admin__metric">
              <span>{{ parserAdminTexts.rollback.created }}</span>
              <strong>{{ formatNumber(rollbackPreview.createdProductsCount) }}</strong>
            </span>
            <span class="parser-admin__metric">
              <span>{{ parserAdminTexts.rollback.updated }}</span>
              <strong>{{ formatNumber(rollbackPreview.updatedProductsCount) }}</strong>
            </span>
            <span class="parser-admin__metric">
              <span>{{ parserAdminTexts.rollback.conflicts }}</span>
              <strong>{{ formatNumber(rollbackPreview.conflictProductsCount) }}</strong>
            </span>
            <span class="parser-admin__metric">
              <span>{{ parserAdminTexts.rollback.alreadyRolledBack }}</span>
              <strong>{{ formatNumber(rollbackPreview.alreadyRolledBackCount) }}</strong>
            </span>
          </div>
        </div>

        <div v-if="rollbackResult" class="parser-admin__rollback-preview">
          <p>{{ rollbackResult.message }}</p>
          <div class="parser-admin__rollback-grid">
            <span class="parser-admin__metric">
              <span>{{ parserAdminTexts.rollback.deleted }}</span>
              <strong>{{ formatNumber(rollbackResult.deletedProductsCount) }}</strong>
            </span>
            <span class="parser-admin__metric">
              <span>{{ parserAdminTexts.rollback.restored }}</span>
              <strong>{{ formatNumber(rollbackResult.restoredProductsCount) }}</strong>
            </span>
          </div>
        </div>

        <footer class="parser-admin__modal-footer">
          <button class="parser-admin__secondary" type="button" @click="closeRollbackModal">
            {{ parserAdminTexts.actions.cancel }}
          </button>
          <button
            class="parser-admin__primary"
            type="button"
            :disabled="rollingBack || !rollbackPreview?.canRollback"
            @click="submitRollback"
          >
            {{ parserAdminTexts.actions.confirmRollback }}
          </button>
        </footer>
      </section>
    </div>

    <div v-if="instanceModalOpen" class="parser-admin__modal-backdrop">
      <section class="parser-admin__modal" role="dialog" aria-modal="true">
        <header class="parser-admin__modal-header">
          <h2>{{ editingInstance ? parserAdminTexts.form.editInstanceTitle : parserAdminTexts.form.addInstanceTitle }}</h2>
          <button class="parser-admin__icon-button" type="button" @click="closeInstanceModal">
            <X :size="18" />
          </button>
        </header>

        <p v-if="instanceFormError" class="parser-admin__error">{{ instanceFormError }}</p>

        <div class="parser-admin__proxy-form">
          <label class="parser-admin__form-field parser-admin__form-field--wide">
            <span>{{ parserAdminTexts.form.instanceName }}</span>
            <input v-model="instanceForm.displayName" class="parser-admin__input" type="text" autocomplete="off" />
          </label>

          <div class="parser-admin__form-field parser-admin__form-field--wide">
            <span>{{ parserAdminTexts.form.assignedProxies }}</span>
            <div class="parser-admin__proxy-picker">
              <label
                v-for="proxy in proxies"
                :key="proxy.id"
                class="parser-admin__proxy-choice"
                :class="{ 'parser-admin__proxy-choice--disabled': isProxyUnavailableForInstance(proxy) }"
              >
                <input
                  type="checkbox"
                  :checked="isProxySelected(proxy.id)"
                  :disabled="isProxyUnavailableForInstance(proxy)"
                  @change="toggleInstanceProxy(proxy)"
                />
                <span>
                  <strong>{{ proxy.key }}</strong>
                  <small>
                    {{ proxy.assignment?.sourceSubcategory || noNicheLabel }}
                    <template v-if="proxy.assignedInstance && isProxyUnavailableForInstance(proxy)">
                      · {{ parserAdminTexts.form.alreadyAssigned }}: {{ proxy.assignedInstance.displayName }}
                    </template>
                  </small>
                </span>
              </label>
              <div v-if="proxies.length === 0" class="parser-admin__empty-row">
                {{ parserAdminTexts.empty.instanceProxies }}
              </div>
            </div>
          </div>
        </div>

        <footer class="parser-admin__modal-footer">
          <button class="parser-admin__secondary" type="button" @click="closeInstanceModal">{{ parserAdminTexts.actions.cancel }}</button>
          <button class="parser-admin__primary" type="button" :disabled="savingInstance" @click="saveInstance">
            {{ parserAdminTexts.actions.save }}
          </button>
        </footer>
      </section>
    </div>

    <div v-if="proxyModalOpen" class="parser-admin__modal-backdrop">
      <section class="parser-admin__modal" role="dialog" aria-modal="true">
        <header class="parser-admin__modal-header">
          <h2>{{ editingProxy ? parserAdminTexts.form.editProxyTitle : parserAdminTexts.form.addProxyTitle }}</h2>
          <button class="parser-admin__icon-button" type="button" @click="closeProxyModal">
            <X :size="18" />
          </button>
        </header>

        <p v-if="proxyFormError" class="parser-admin__error">{{ proxyFormError }}</p>

        <div class="parser-admin__proxy-form">
          <label class="parser-admin__form-field parser-admin__form-field--wide">
            <span>IP</span>
            <input v-model="proxyForm.ip" class="parser-admin__input" type="text" autocomplete="off" />
          </label>

          <div class="parser-admin__form-column">
            <label class="parser-admin__form-field">
              <span>{{ parserAdminTexts.form.login }}</span>
              <input v-model="proxyForm.login" class="parser-admin__input" type="text" autocomplete="off" />
            </label>
            <label class="parser-admin__form-field">
              <span>{{ parserAdminTexts.form.password }}</span>
              <input
                v-model="proxyForm.password"
                class="parser-admin__input"
                type="password"
                autocomplete="new-password"
                :placeholder="editingProxy ? parserAdminTexts.form.passwordPlaceholder : ''"
              />
            </label>
          </div>

          <div class="parser-admin__form-column">
            <label class="parser-admin__form-field">
              <span>HTTP port</span>
              <input v-model.number="proxyForm.httpPort" class="parser-admin__input" type="number" min="1" max="65535" />
            </label>
            <label class="parser-admin__form-field">
              <span>SOCKS5 port</span>
              <input v-model.number="proxyForm.socksPort" class="parser-admin__input" type="number" min="1" max="65535" />
            </label>
          </div>

          <div class="parser-admin__form-field parser-admin__form-field--wide parser-admin__category-field">
            <label for="parser-proxy-category">{{ parserAdminTexts.form.niche }}</label>
            <div class="parser-admin__category-input-wrap">
              <Search :size="16" />
              <input
                id="parser-proxy-category"
                v-model="proxyForm.categoryQuery"
                class="parser-admin__input parser-admin__input--with-icon"
                type="text"
                autocomplete="off"
                :placeholder="parserAdminTexts.form.nichePlaceholder"
                @focus="categoryDropdownOpen = true"
                @input="onCategoryQueryInput"
              />
            </div>
            <div v-if="categoryDropdownOpen" class="parser-admin__category-list">
              <button class="parser-admin__category-option" type="button" @click="selectCategory(null)">
                {{ noNicheLabel }}
              </button>
              <button
                v-for="category in categoryOptions"
                :key="category.id"
                class="parser-admin__category-option"
                :class="{ 'parser-admin__category-option--active': proxyForm.wbCategoryId === category.id }"
                type="button"
                @click="selectCategory(category)"
              >
                {{ category.path }}
              </button>
            </div>
          </div>
        </div>

        <p v-if="categorySearchError" class="parser-admin__error">{{ categorySearchError }}</p>

        <footer class="parser-admin__modal-footer">
          <button class="parser-admin__secondary" type="button" @click="closeProxyModal">{{ parserAdminTexts.actions.cancel }}</button>
          <button class="parser-admin__primary" type="button" :disabled="savingProxy" @click="saveProxy">
            {{ parserAdminTexts.actions.save }}
          </button>
        </footer>
      </section>
    </div>
  </main>
</template>

<style scoped>
.parser-admin {
  display: grid;
  gap: var(--space-6);
}

.parser-admin__panel {
  display: grid;
  gap: var(--space-4);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--surface-panel);
  padding: var(--space-4);
}

.parser-admin__toolbar,
.parser-admin__actions,
.parser-admin__modal-header,
.parser-admin__modal-footer,
.parser-admin__row-actions {
  display: flex;
  align-items: center;
  gap: var(--space-2);
}

.parser-admin__toolbar {
  flex-wrap: wrap;
  justify-content: space-between;
}

.parser-admin__tabs {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
}

.parser-admin__tab,
.parser-admin__refresh,
.parser-admin__primary,
.parser-admin__secondary,
.parser-admin__proxy-check,
.parser-admin__icon-button,
.parser-admin__category-option {
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  background: var(--surface-card);
  color: var(--color-text);
  font-weight: 750;
}

.parser-admin__tab,
.parser-admin__refresh,
.parser-admin__primary,
.parser-admin__secondary,
.parser-admin__proxy-check {
  min-height: 2.25rem;
  padding: 0 var(--space-4);
}

.parser-admin__refresh,
.parser-admin__primary {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
}

.parser-admin__tab--active,
.parser-admin__refresh,
.parser-admin__primary {
  border-color: var(--color-ember);
  background: var(--accent-ember-hover-bg);
  color: var(--accent-ember-text-strong);
}

.parser-admin__primary:disabled,
.parser-admin__secondary:disabled,
.parser-admin__proxy-check:disabled {
  cursor: not-allowed;
  opacity: 0.55;
}

.parser-admin__error {
  border: 1px solid var(--color-danger);
  border-radius: var(--radius-sm);
  padding: var(--space-3);
  color: var(--color-danger);
}

.parser-admin__notice {
  border: 1px solid rgba(5, 150, 105, 0.35);
  border-radius: var(--radius-sm);
  padding: var(--space-3);
  background: rgba(5, 150, 105, 0.08);
  color: #047857;
  font-weight: 700;
}

.parser-admin__loading,
.parser-admin__empty,
.parser-admin__empty-row {
  color: var(--color-text-muted);
}

.parser-admin__instances {
  display: grid;
  gap: var(--space-3);
}

.parser-admin__instance {
  overflow: visible;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--surface-card);
}

.parser-admin__instance-main,
.parser-admin__proxy-run {
  display: grid;
  grid-template-columns: minmax(220px, 1.3fr) repeat(4, minmax(130px, 1fr)) auto;
  align-items: center;
  gap: var(--space-4);
  width: 100%;
  padding: var(--space-4);
  text-align: left;
}

.parser-admin__instance-main {
  border: 0;
  background: transparent;
  color: var(--color-text);
  cursor: pointer;
}

.parser-admin__instance-main:hover {
  background: var(--surface-active-overlay);
}

.parser-admin__instance-title,
.parser-admin__proxy-title,
.parser-admin__metric,
.parser-admin__table td span {
  display: grid;
  gap: 0.2rem;
}

.parser-admin__proxy-title,
.parser-admin__metric {
  --proxy-label-row-height: 2.55rem;
  grid-template-rows: var(--proxy-label-row-height) auto;
  align-content: start;
  gap: 0.45rem;
  min-height: 5rem;
}

.parser-admin__proxy-title-head {
  display: grid;
  align-content: start;
  gap: 0.2rem;
  min-width: 0;
  min-height: var(--proxy-label-row-height);
}

.parser-admin__metric > span:first-child {
  min-height: var(--proxy-label-row-height);
}

.parser-admin__metric > strong,
.parser-admin__proxy-check {
  align-self: start;
}

.parser-admin__date-stack {
  display: grid;
  gap: 0.05rem;
  line-height: 1.2;
}

.parser-admin__date-stack span {
  color: inherit;
  font-size: inherit;
  font-weight: inherit;
}

.parser-admin__instance-title strong,
.parser-admin__proxy-title strong,
.parser-admin__metric strong,
.parser-admin__table td {
  font-weight: 750;
}

.parser-admin__proxy-name {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  line-height: 1.2;
}

.parser-admin__proxy-check {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  justify-self: start;
  margin-top: 0;
  border-color: var(--color-ember);
  background: var(--accent-ember-hover-bg);
  color: var(--accent-ember-text-strong);
}

.parser-admin__instance-title span,
.parser-admin__proxy-title span,
.parser-admin__metric > span:first-child,
.parser-admin__table td span,
.parser-admin__form-field span,
.parser-admin__category-field label {
  color: var(--color-text-muted);
  font-size: 0.78rem;
  font-weight: 650;
}

.parser-admin__proxies {
  display: grid;
  border-top: 1px solid var(--color-border);
}

.parser-admin__instance-actions {
  display: grid;
  grid-template-columns: minmax(220px, auto) 1fr;
  gap: var(--space-2);
  align-items: start;
  padding: 0 var(--space-4) var(--space-4);
}

.parser-admin__action-group {
  display: inline-flex;
  align-items: center;
  flex-wrap: wrap;
  gap: var(--space-2);
}

.parser-admin__action-group > span {
  width: 100%;
  color: var(--color-text-muted);
  font-size: 0.75rem;
  font-weight: 750;
}

.parser-admin__instance-actions .parser-admin__secondary,
.parser-admin__instance-actions .parser-admin__primary {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
}

.parser-admin__proxy-run {
  grid-template-columns:
    minmax(180px, 1.25fr)
    minmax(96px, 0.75fr)
    minmax(112px, 1fr)
    minmax(90px, 0.7fr)
    minmax(96px, 0.8fr)
    minmax(108px, 0.95fr)
    minmax(112px, 0.95fr)
    minmax(92px, 0.75fr)
    minmax(96px, 0.8fr)
    minmax(108px, 0.9fr);
  align-items: start;
  border-top: 1px solid var(--color-border-muted);
}

.parser-admin__proxy-run:first-child {
  border-top: 0;
}

.parser-admin__status {
  display: inline-flex;
  width: fit-content;
  border-radius: 999px;
  padding: 0.15rem 0.55rem;
  background: var(--surface-muted);
  color: var(--color-text);
}

.parser-admin__status--completed {
  background: rgba(5, 150, 105, 0.12);
  color: #047857;
}

.parser-admin__status--failed,
.parser-admin__status--interrupted {
  background: rgba(190, 18, 60, 0.12);
  color: #9f1239;
}

.parser-admin__status--running,
.parser-admin__status--queued,
.parser-admin__status--preflight,
.parser-admin__status--ranges,
.parser-admin__status--download,
.parser-admin__status--cooldown {
  background: rgba(217, 119, 6, 0.12);
  color: #9a3412;
}

.parser-admin__table-wrap {
  overflow: auto;
}

.parser-admin__table {
  width: 100%;
  min-width: 1120px;
  border-collapse: collapse;
}

.parser-admin__table th,
.parser-admin__table td {
  border-bottom: 1px solid var(--color-border);
  padding: var(--space-3);
  text-align: left;
  vertical-align: top;
}

.parser-admin__table th {
  color: var(--color-text-muted);
  font-size: 0.75rem;
  font-weight: 850;
}

.parser-admin__table--proxies {
  min-width: 980px;
}

.parser-admin__empty,
.parser-admin__empty-row {
  padding: var(--space-4);
}

.parser-admin__icon-button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 2rem;
  height: 2rem;
}

.parser-admin__modal-backdrop {
  position: fixed;
  inset: 0;
  z-index: 40;
  display: grid;
  place-items: center;
  padding: var(--space-4);
  background: rgba(15, 23, 42, 0.42);
}

.parser-admin__modal {
  display: grid;
  gap: var(--space-4);
  width: min(680px, 100%);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--surface-panel);
  padding: var(--space-5);
  box-shadow: var(--shadow-lg);
}

.parser-admin__modal-header {
  justify-content: space-between;
}

.parser-admin__modal-header h2 {
  margin: 0;
  font-size: 1.1rem;
}

.parser-admin__launch-confirmation {
  grid-column: 1 / -1;
  margin: 0;
  color: var(--color-text);
  line-height: 1.5;
}

.parser-admin__rollback-preview {
  display: grid;
  gap: var(--space-3);
}

.parser-admin__rollback-preview p {
  margin: 0;
  line-height: 1.5;
}

.parser-admin__rollback-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: var(--space-3);
}

.parser-admin__rollback-grid--details {
  grid-template-columns: repeat(3, minmax(0, 1fr));
}

.parser-admin__proxy-form {
  display: grid;
  align-items: start;
  grid-template-columns: 1fr 1fr;
  gap: var(--space-3);
}

.parser-admin__form-column,
.parser-admin__form-field {
  display: grid;
  gap: var(--space-2);
  min-width: 0;
}

.parser-admin__form-field--wide {
  grid-column: 1 / -1;
}

.parser-admin__form-column {
  align-content: start;
}

.parser-admin__input {
  display: block;
  box-sizing: border-box;
  width: 100%;
  min-height: 2.5rem;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  background: var(--surface-card);
  padding: 0 var(--space-3);
  color: var(--color-text);
  font: inherit;
  line-height: 1.4;
}

.parser-admin__input::placeholder {
  color: var(--color-text-muted);
  opacity: 0.75;
}

.parser-admin__input:focus {
  outline: none;
  border-color: var(--accent-primary-border);
  box-shadow: var(--focus-ring);
}

.parser-admin__category-field {
  position: relative;
}

.parser-admin__category-input-wrap {
  position: relative;
  display: flex;
  align-items: center;
  width: 100%;
}

.parser-admin__category-input-wrap svg {
  position: absolute;
  left: var(--space-3);
  color: var(--color-text-muted);
}

.parser-admin__input--with-icon {
  padding-left: 2.25rem;
}

.parser-admin__category-list {
  position: absolute;
  z-index: 50;
  top: calc(100% + 0.25rem);
  left: 0;
  right: 0;
  display: grid;
  max-height: 260px;
  overflow: auto;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  background: var(--surface-panel);
  box-shadow: var(--shadow-lg);
}

.parser-admin__category-option {
  border: 0;
  border-bottom: 1px solid var(--color-border-muted);
  border-radius: 0;
  padding: var(--space-3);
  text-align: left;
}

.parser-admin__category-option--active {
  background: var(--accent-ember-hover-bg);
  color: var(--accent-ember-text-strong);
}

.parser-admin__proxy-picker {
  display: grid;
  gap: var(--space-2);
  max-height: 16rem;
  overflow-y: auto;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  padding: var(--space-2);
  background: var(--surface-card);
}

.parser-admin__proxy-choice {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  border-radius: var(--radius-sm);
  padding: var(--space-2);
  cursor: pointer;
}

.parser-admin__proxy-choice:hover {
  background: var(--surface-muted);
}

.parser-admin__proxy-choice--disabled {
  cursor: not-allowed;
  opacity: 0.55;
}

.parser-admin__proxy-choice span {
  display: grid;
  gap: 0.15rem;
}

.parser-admin__proxy-choice small {
  color: var(--color-text-muted);
}

.parser-admin__modal-footer {
  justify-content: flex-end;
}

@media (max-width: 1450px) {
  .parser-admin__proxy-run {
    grid-template-columns: 1fr 1fr 1fr 1fr;
  }
}

@media (max-width: 1100px) {
  .parser-admin__instance-main,
  .parser-admin__proxy-run,
  .parser-admin__instance-actions,
  .parser-admin__proxy-form {
    grid-template-columns: 1fr;
  }
}
</style>
