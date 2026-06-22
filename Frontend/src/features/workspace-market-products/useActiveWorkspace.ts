import { computed, ref, watch } from 'vue';

import { getManagementWorkspaces } from '@/features/access-settings/accessSettings.api';
import { useAuthStore } from '@/features/auth/auth.store';
import type { AuthUserWorkspace } from '@/features/auth/auth.types';

const STORAGE_KEY = 'ashmes.activeWorkspaceId';

const selectedWorkspaceId = ref(localStorage.getItem(STORAGE_KEY) ?? '');
const workspaceNamesById = ref<Record<string, string>>({});
let loadingWorkspaceNames = false;
let initialized = false;

export function useActiveWorkspace() {
  const auth = useAuthStore();

  const workspaceOptions = computed(() => auth.user?.workspaces ?? []);
  const activeWorkspace = computed(() =>
    workspaceOptions.value.find((workspace) => workspace.idWorkspace === selectedWorkspaceId.value) ?? null
  );
  const activeWorkspaceId = computed(() => activeWorkspace.value?.idWorkspace ?? null);
  const activeWorkspaceName = computed(() =>
    activeWorkspace.value ? getWorkspaceDisplayName(activeWorkspace.value) : 'Вне рабочей области'
  );
  const hasMultipleWorkspaces = computed(() => workspaceOptions.value.length > 1);

  if (!initialized) {
    initialized = true;

    watch(
      [workspaceOptions, () => auth.ready, () => auth.bootstrapping, () => auth.isAuthenticated],
      ([workspaces, ready, bootstrapping, authenticated]) => {
        if (!ready || bootstrapping) {
          return;
        }

        if (!authenticated || workspaces.length === 0) {
          selectedWorkspaceId.value = '';
          localStorage.removeItem(STORAGE_KEY);
          return;
        }

        if (!workspaces.some((workspace) => workspace.idWorkspace === selectedWorkspaceId.value)) {
          selectedWorkspaceId.value = workspaces[0].idWorkspace;
        }

        rememberAuthWorkspaceNames(workspaces);
        if (workspaces.some((workspace) => shouldLoadWorkspaceName(workspace))) {
          void loadWorkspaceNames();
        }
      },
      { immediate: true }
    );

    watch(selectedWorkspaceId, (workspaceId) => {
      if (workspaceId) {
        localStorage.setItem(STORAGE_KEY, workspaceId);
      } else {
        localStorage.removeItem(STORAGE_KEY);
      }
    });
  }

  function selectWorkspace(workspaceId: string): void {
    if (workspaceOptions.value.some((workspace) => workspace.idWorkspace === workspaceId)) {
      selectedWorkspaceId.value = workspaceId;
    }
  }

  function getWorkspaceDisplayName(workspace: AuthUserWorkspace): string {
    const declaredName = workspace.workspaceName?.trim();
    if (declaredName && !isGuidLike(declaredName)) {
      return declaredName;
    }

    return workspaceNamesById.value[workspace.idWorkspace] ?? 'Рабочая область';
  }

  return {
    workspaceOptions,
    selectedWorkspaceId,
    activeWorkspace,
    activeWorkspaceId,
    activeWorkspaceName,
    hasMultipleWorkspaces,
    selectWorkspace,
    getWorkspaceDisplayName
  };
}

function rememberAuthWorkspaceNames(workspaces: AuthUserWorkspace[]): void {
  const next = { ...workspaceNamesById.value };
  for (const workspace of workspaces) {
    const name = workspace.workspaceName?.trim();
    if (name && !isGuidLike(name)) {
      next[workspace.idWorkspace] = name;
    }
  }

  workspaceNamesById.value = next;
}

function shouldLoadWorkspaceName(workspace: AuthUserWorkspace): boolean {
  const name = workspace.workspaceName?.trim();
  return !name || isGuidLike(name);
}

async function loadWorkspaceNames(): Promise<void> {
  if (loadingWorkspaceNames) {
    return;
  }

  loadingWorkspaceNames = true;
  try {
    const workspaces = await getManagementWorkspaces();
    const next = { ...workspaceNamesById.value };
    for (const workspace of workspaces) {
      if (workspace.name) {
        next[workspace.id] = workspace.name;
      }
    }

    workspaceNamesById.value = next;
  } catch {
    // Keep the generic label instead of exposing workspace ids when the fallback fails.
  } finally {
    loadingWorkspaceNames = false;
  }
}

function isGuidLike(value: string): boolean {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value);
}
