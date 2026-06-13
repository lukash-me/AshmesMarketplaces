import { http } from '@/shared/api/http';

import type {
  WorkspaceOverview,
  WorkspaceOverviewMarkViewedRequest,
  WorkspaceOverviewMarkViewedResponse,
  WorkspaceOverviewRecalculateResponse
} from './workspaceOverview.types';

function basePath(workspaceId: string): string {
  return `/workspaces/${workspaceId}/overview`;
}

export async function getWorkspaceOverview(workspaceId: string): Promise<WorkspaceOverview> {
  const response = await http.get<WorkspaceOverview>(basePath(workspaceId));
  return response.data;
}

export async function recalculateWorkspaceOverview(
  workspaceId: string
): Promise<WorkspaceOverviewRecalculateResponse> {
  const response = await http.post<WorkspaceOverviewRecalculateResponse>(`${basePath(workspaceId)}/recalculate`);
  return response.data;
}

export async function markWorkspaceOverviewViewed(
  workspaceId: string,
  request: WorkspaceOverviewMarkViewedRequest
): Promise<WorkspaceOverviewMarkViewedResponse> {
  const response = await http.post<WorkspaceOverviewMarkViewedResponse>(`${basePath(workspaceId)}/new/mark-viewed`, request);
  return response.data;
}
