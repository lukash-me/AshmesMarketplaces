<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';

import { getProblemMessage } from '@/shared/api/problemDetails';
import EmptyState from '@/shared/ui/EmptyState.vue';
import LoadingState from '@/shared/ui/LoadingState.vue';
import PageHeader from '@/widgets/PageHeader.vue';

import AccessMembershipDetailDrawer from './AccessMembershipDetailDrawer.vue';
import AccessMembershipsFilters from './AccessMembershipsFilters.vue';
import AccessMembershipsTable from './AccessMembershipsTable.vue';
import { getPermissions, getRoles, getUsers, getUserWorkspaces, getWorkspaces } from './accessSettings.api';
import {
  membershipKey,
  type PermissionLookup,
  type RoleLookup,
  type UserLookup,
  type WorkspaceLookup
} from './accessSettingsDisplay';
import {
  parseAccessSettingsQuery,
  removeAccessSettingsQueryFilter,
  resetAccessSettingsQueryFilters,
  toAccessSettingsApiParams,
  toAccessSettingsRouteQuery,
  type AccessSettingsQueryFilterKey
} from './accessSettingsQuery';
import type {
  AccessSettingsQueryState,
  PermissionListItem,
  RoleListItem,
  UserListItem,
  UserWorkspaceListItem,
  WorkspaceListItem
} from './accessSettings.types';

const route = useRoute();
const router = useRouter();

const queryState = ref<AccessSettingsQueryState>(parseAccessSettingsQuery(route.query));
const memberships = ref<UserWorkspaceListItem[]>([]);
const totalCount = ref(0);
const loading = ref(false);
const error = ref('');
const selectedMembership = ref<UserWorkspaceListItem | null>(null);

const users = ref<UserListItem[]>([]);
const workspaces = ref<WorkspaceListItem[]>([]);
const roles = ref<RoleListItem[]>([]);
const permissions = ref<PermissionListItem[]>([]);
const lookupsLoading = ref(false);
const lookupsError = ref('');

const usersById = computed<UserLookup>(() =>
  users.value.reduce<UserLookup>((lookup, user) => {
    lookup[user.id] = user;
    return lookup;
  }, {})
);

const workspacesById = computed<WorkspaceLookup>(() =>
  workspaces.value.reduce<WorkspaceLookup>((lookup, workspace) => {
    lookup[workspace.id] = workspace;
    return lookup;
  }, {})
);

const rolesById = computed<RoleLookup>(() =>
  roles.value.reduce<RoleLookup>((lookup, role) => {
    lookup[role.id] = role;
    return lookup;
  }, {})
);

const permissionsById = computed<PermissionLookup>(() =>
  permissions.value.reduce<PermissionLookup>((lookup, permission) => {
    lookup[permission.id] = permission;
    return lookup;
  }, {})
);

const selectedMembershipKey = computed(() =>
  selectedMembership.value
    ? membershipKey(selectedMembership.value.idUser, selectedMembership.value.idWorkspace)
    : null
);

watch(
  () => route.query,
  async (query) => {
    queryState.value = parseAccessSettingsQuery(query);
    await loadMemberships();
  },
  { immediate: true }
);

void loadLookups();

async function loadMemberships() {
  loading.value = true;
  error.value = '';

  try {
    const response = await getUserWorkspaces(toAccessSettingsApiParams(queryState.value));
    memberships.value = response.items;
    totalCount.value = response.totalCount;
  } catch (err) {
    memberships.value = [];
    totalCount.value = 0;
    error.value = getProblemMessage(err, 'Unable to load workspace memberships.');
  } finally {
    loading.value = false;
  }
}

async function loadLookups() {
  lookupsLoading.value = true;
  lookupsError.value = '';

  try {
    const [usersResponse, workspacesResponse, rolesResponse, permissionsResponse] = await Promise.all([
      getUsers({ page: 1, pageSize: 200, sort: 'login' }),
      getWorkspaces({ page: 1, pageSize: 200, sort: 'name' }),
      getRoles({ page: 1, pageSize: 200, sort: 'name' }),
      getPermissions({ page: 1, pageSize: 200, sort: 'name' })
    ]);

    users.value = usersResponse.items;
    workspaces.value = workspacesResponse.items;
    roles.value = rolesResponse.items;
    permissions.value = permissionsResponse.items;
  } catch (err) {
    users.value = [];
    workspaces.value = [];
    roles.value = [];
    permissions.value = [];
    lookupsError.value = getProblemMessage(
      err,
      'Unable to load access reference context.'
    );
  } finally {
    lookupsLoading.value = false;
  }
}

async function updateQuery(patch: Partial<AccessSettingsQueryState>) {
  const nextState = {
    ...queryState.value,
    ...patch
  };

  await router.replace({
    query: toAccessSettingsRouteQuery(nextState)
  });
}

function resetFilters() {
  void router.replace({
    query: toAccessSettingsRouteQuery(resetAccessSettingsQueryFilters(queryState.value))
  });
}

function removeFilter(key: AccessSettingsQueryFilterKey) {
  void router.replace({
    query: toAccessSettingsRouteQuery(removeAccessSettingsQueryFilter(queryState.value, key))
  });
}

function openMembership(row: UserWorkspaceListItem) {
  selectedMembership.value = row;
}

function closeMembership() {
  selectedMembership.value = null;
}
</script>

<template>
  <div class="access-page">
    <PageHeader
      title="Access"
      description="Read-only users, workspaces, roles and persisted workspace membership records."
    />

    <AccessMembershipsFilters
      :state="queryState"
      :users-by-id="usersById"
      :workspaces-by-id="workspacesById"
      :roles-by-id="rolesById"
      @apply="updateQuery"
      @reset="resetFilters"
      @remove="removeFilter"
    />

    <section v-if="lookupsError" class="access-page__notice app-surface">
      {{ lookupsError }} Raw IDs are still shown from membership records.
    </section>

    <section v-else-if="lookupsLoading" class="access-page__hint app-surface">
      Loading access reference context. Membership records can still render with raw IDs.
    </section>

    <LoadingState v-if="loading" class="app-surface" />

    <EmptyState
      v-else-if="error"
      class="app-surface"
      title="Access memberships could not be loaded"
      :description="error"
    />

    <EmptyState
      v-else-if="memberships.length === 0"
      class="app-surface"
      title="No workspace memberships found"
      description="Adjust filters or load user-workspace records through the existing backend API."
    />

    <AccessMembershipsTable
      v-else
      :rows="memberships"
      :page="queryState.page"
      :page-size="queryState.pageSize"
      :total-count="totalCount"
      :sort="queryState.sort"
      :selected-key="selectedMembershipKey"
      :users-by-id="usersById"
      :workspaces-by-id="workspacesById"
      :roles-by-id="rolesById"
      @sort="updateQuery({ page: 1, sort: $event })"
      @page="updateQuery({ page: $event })"
      @open="openMembership"
    />

    <AccessMembershipDetailDrawer
      :open="Boolean(selectedMembership)"
      :membership="selectedMembership"
      :users-by-id="usersById"
      :workspaces-by-id="workspacesById"
      :roles-by-id="rolesById"
      :permissions-by-id="permissionsById"
      @close="closeMembership"
    />
  </div>
</template>

<style scoped>
.access-page {
  display: grid;
  gap: var(--space-4);
}

.access-page__notice,
.access-page__hint {
  padding: var(--space-3);
  font-size: 0.8125rem;
}

.access-page__notice {
  border-color: var(--state-warning-border);
  color: var(--state-warning);
}

.access-page__hint {
  color: var(--color-text-muted);
}
</style>

