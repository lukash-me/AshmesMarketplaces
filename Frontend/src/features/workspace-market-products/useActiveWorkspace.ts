import { computed, ref, watch } from 'vue';

import { useAuthStore } from '@/features/auth/auth.store';

const STORAGE_KEY = 'ashmes.activeWorkspaceId';

export function useActiveWorkspace() {
  const auth = useAuthStore();
  const selectedWorkspaceId = ref(localStorage.getItem(STORAGE_KEY) ?? '');

  const workspaceOptions = computed(() => auth.user?.workspaces ?? []);
  const activeWorkspaceId = computed(() => selectedWorkspaceId.value || null);
  const hasMultipleWorkspaces = computed(() => workspaceOptions.value.length > 1);

  watch(
    workspaceOptions,
    (workspaces) => {
      if (workspaces.length === 0) {
        selectedWorkspaceId.value = '';
        localStorage.removeItem(STORAGE_KEY);
        return;
      }

      if (!workspaces.some((workspace) => workspace.idWorkspace === selectedWorkspaceId.value)) {
        selectedWorkspaceId.value = workspaces[0].idWorkspace;
      }
    },
    { immediate: true }
  );

  watch(selectedWorkspaceId, (workspaceId) => {
    if (workspaceId) {
      localStorage.setItem(STORAGE_KEY, workspaceId);
    }
  });

  return {
    workspaceOptions,
    selectedWorkspaceId,
    activeWorkspaceId,
    hasMultipleWorkspaces
  };
}
