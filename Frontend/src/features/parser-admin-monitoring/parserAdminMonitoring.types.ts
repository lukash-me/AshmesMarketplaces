export interface ParserAdminProxyRun {
  id: string;
  externalProxyRunId: string;
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
  progressPercent: number;
  startedAtUtc: string;
  lastHeartbeatAtUtc: string;
  finishedAtUtc: string | null;
  runtimeMinutes: number;
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

export interface ParserAdminProxyRunJournal {
  id: string;
  parserInstanceId: string;
  externalProxyRunId: string;
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
  startedAtUtc: string;
  finishedAtUtc: string | null;
  runtimeMinutes: number;
  error: string | null;
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
