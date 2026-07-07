export interface ParserAdminProxyRun {
  id: string;
  externalProxyRunId: string;
  parserCycleId: string;
  cycleKind: string;
  proxyKey: string;
  sourceCategory: string;
  sourceSubcategory: string;
  egressIp: string | null;
  status: string;
  phase: string;
  plannedProductsCount: number;
  downloadedProductsCount: number;
  plannedRangesCount: number;
  completedRangesCount: number;
  rangeProgressPercent: number;
  rangeChecksCount: number;
  finalRangesCount: number;
  emptyRangesCount: number;
  splitRangesCount: number;
  progressPercent: number;
  startedAtUtc: string;
  lastHeartbeatAtUtc: string;
  lastLogAtUtc: string | null;
  finishedAtUtc: string | null;
  runtimeMinutes: number;
  productsPerSecond: number;
  rangesPerSecond: number;
  error: string | null;
}

export interface ParserAdminInstance {
  id: string;
  parserInstanceId: string;
  displayName: string | null;
  runningProxiesCount: number;
  totalProxiesCount: number;
  plannedProductsCount: number;
  downloadedProductsCount: number;
  progressPercent: number;
  runtimeMinutes: number;
  lastHeartbeatUtc: string;
  proxies: ParserAdminProxyRun[];
}

export interface ParserInstanceConfiguredProxy {
  proxyId: string;
  proxyKey: string;
  ip: string;
  sourceSubcategory: string | null;
  assignmentEnabled: boolean;
}

export interface ParserInstanceConfiguration {
  id: string;
  parserInstanceId: string;
  displayName: string;
  hostKind: string;
  status: string;
  createdAtUtc: string;
  updatedAtUtc: string;
  proxies: ParserInstanceConfiguredProxy[];
}

export interface ParserInstanceConfigurationSavePayload {
  displayName: string;
  proxyIds: string[];
  status?: string;
}

export interface ParserLaunchRequest {
  id: string;
  parserInstanceId: string;
  mode: string;
  proxyKey: string | null;
  batchLimit: number | null;
  status: string;
  requestedAtUtc: string;
}

export interface ParserLaunchSavePayload {
  mode: 'limited_all' | 'full_all' | 'check_proxy';
  batchLimit?: number | null;
  proxyKey?: string | null;
}

export interface ParserAdminProxyRunJournal {
  id: string;
  parserInstanceId: string;
  parserCycleId: string;
  cycleKind: string;
  externalProxyRunId: string;
  proxyKey: string;
  sourceCategory: string;
  sourceSubcategory: string;
  egressIp: string | null;
  status: string;
  phase: string;
  plannedProductsCount: number;
  downloadedProductsCount: number;
  createdProductsCount: number;
  updatedProductsCount: number;
  hasProductEffectsLedger: boolean;
  plannedRangesCount: number;
  completedRangesCount: number;
  rangeProgressPercent: number;
  rangeChecksCount: number;
  finalRangesCount: number;
  emptyRangesCount: number;
  splitRangesCount: number;
  startedAtUtc: string;
  lastLogAtUtc: string | null;
  finishedAtUtc: string | null;
  runtimeMinutes: number;
  productsPerSecond: number;
  rangesPerSecond: number;
  error: string | null;
}

export interface ParserRunRollbackPreview {
  parserProxyRunId: string;
  canRollback: boolean;
  message: string;
  createdProductsCount: number;
  updatedProductsCount: number;
  conflictProductsCount: number;
  alreadyRolledBackCount: number;
}

export interface ParserRunRollbackResponse {
  rollbackId: string;
  parserProxyRunId: string;
  status: string;
  message: string;
  createdProductsCount: number;
  updatedProductsCount: number;
  deletedProductsCount: number;
  restoredProductsCount: number;
  conflictProductsCount: number;
}

export interface ParserAdminBatch {
  id: string;
  parserInstanceId: string;
  externalBatchId: string;
  sourceCategory: string;
  sourceSubcategory: string;
  proxyKey: string | null;
  batchKind: string;
  status: string;
  attemptsCount: number;
  acceptedAtUtc: string;
  processingStartedAtUtc: string | null;
  completedAtUtc: string | null;
  error: string | null;
}

export interface ParserAdminBatchEvent {
  id: string;
  eventType: string;
  status: string;
  message: string | null;
  createdAtUtc: string;
}

export interface ParserAdminBatchArtifact {
  id: string;
  artifactKind: string;
  createdAtUtc: string;
}

export interface ParserAdminBatchDetail {
  batch: ParserAdminBatch;
  events: ParserAdminBatchEvent[];
  artifacts: ParserAdminBatchArtifact[];
}

export interface ParserAdminNiche {
  sourceCategory: string;
  sourceSubcategory: string;
  assignedParserInstanceId: string | null;
  lastProxyKey: string | null;
  isEnabled: boolean;
  lastSuccessfulBatchAtUtc: string | null;
  lagHours: number | null;
  failedBatchesCount: number;
}

export interface ParserAdminError {
  batchId: string | null;
  parserInstanceId: string | null;
  externalBatchId: string | null;
  sourceCategory: string | null;
  sourceSubcategory: string | null;
  proxyKey: string | null;
  status: string;
  error: string;
  createdAtUtc: string;
}

export interface ParserAdminRetryResponse {
  id: string;
  externalBatchId: string;
  status: string;
  queuedAtUtc: string;
}

export interface ParserProxyAssignment {
  id: string;
  wbCategoryId: number;
  sourceCategory: string;
  sourceSubcategory: string;
  sourcePath: string;
  searchQuery: string;
  parserSearchText: string;
  enabled: boolean;
}

export interface ParserProxy {
  id: string;
  key: string;
  ip: string;
  httpPort: number;
  socksPort: number;
  login: string;
  hasPassword: boolean;
  status: string;
  createdAtUtc: string;
  updatedAtUtc: string;
  assignment: ParserProxyAssignment | null;
  assignedInstance: {
    instanceConfigurationId: string;
    parserInstanceId: string;
    displayName: string;
  } | null;
}

export interface ParserProxySavePayload {
  ip: string;
  httpPort: number;
  socksPort: number;
  login: string;
  password?: string;
  wbCategoryId?: number | null;
  assignmentEnabled?: boolean;
  status?: string;
}

export interface WbCategoryLeaf {
  id: number;
  name: string;
  sourceCategory: string;
  sourceSubcategory: string;
  path: string;
  searchQuery: string | null;
  parentId: number | null;
  isLeaf: boolean;
  level: number;
}
