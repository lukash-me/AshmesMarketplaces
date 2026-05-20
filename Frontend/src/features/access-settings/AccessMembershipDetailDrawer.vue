<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { X } from 'lucide-vue-next';

import { getProblemMessage } from '@/shared/api/problemDetails';
import Badge from '@/shared/ui/Badge.vue';
import LoadingState from '@/shared/ui/LoadingState.vue';

import {
  compactId,
  fieldValue,
  formatDateTime,
  getAccessNeutralTone,
  getDomainLabel,
  getPermissionLabel,
  getRoleLabel,
  getStatusLabel,
  getUserLabel,
  getWorkspaceLabel,
  type PermissionLookup,
  type RoleLookup,
  type UserLookup,
  type WorkspaceLookup
} from './accessSettingsDisplay';
import {
  getPermission,
  getRole,
  getRolePermissions,
  getUser,
  getUserWorkspace,
  getWorkspace
} from './accessSettings.api';
import type {
  PermissionDetail,
  RoleDetail,
  RolePermissionListItem,
  UserDetail,
  UserWorkspaceDetail,
  UserWorkspaceListItem,
  WorkspaceDetail
} from './accessSettings.types';

const props = defineProps<{
  open: boolean;
  membership: UserWorkspaceListItem | null;
  usersById: UserLookup;
  workspacesById: WorkspaceLookup;
  rolesById: RoleLookup;
  permissionsById: PermissionLookup;
}>();

const emit = defineEmits<{
  close: [];
}>();

const detail = ref<UserWorkspaceDetail | null>(null);
const user = ref<UserDetail | null>(null);
const workspace = ref<WorkspaceDetail | null>(null);
const role = ref<RoleDetail | null>(null);
const rolePermissions = ref<RolePermissionListItem[]>([]);
const loadedPermissions = ref<Record<string, PermissionDetail>>({});

const detailLoading = ref(false);
const userLoading = ref(false);
const workspaceLoading = ref(false);
const roleLoading = ref(false);
const rolePermissionsLoading = ref(false);
const permissionsLoading = ref(false);

const detailError = ref('');
const userError = ref('');
const workspaceError = ref('');
const roleError = ref('');
const rolePermissionsError = ref('');
const permissionsError = ref('');

let detailLoadVersion = 0;
let userLoadVersion = 0;
let workspaceLoadVersion = 0;
let roleLoadVersion = 0;
let rolePermissionsLoadVersion = 0;
let permissionsLoadVersion = 0;

const displayMembership = computed(() => detail.value ?? props.membership);
const displayUser = computed(() => {
  const id = displayMembership.value?.idUser;
  return user.value ?? (id ? props.usersById[id] ?? null : null);
});
const displayWorkspace = computed(() => {
  const id = displayMembership.value?.idWorkspace;
  return workspace.value ?? (id ? props.workspacesById[id] ?? null : null);
});
const displayRole = computed(() => {
  const id = displayMembership.value?.idRole;
  return role.value ?? (id ? props.rolesById[id] ?? null : null);
});
const permissionsLookup = computed<PermissionLookup>(() => ({
  ...props.permissionsById,
  ...loadedPermissions.value
}));

watch(
  () => [props.open, props.membership?.idUser, props.membership?.idWorkspace] as const,
  async ([open, idUser, idWorkspace]) => {
    if (!open || !idUser || !idWorkspace) {
      resetDrawerState();
      return;
    }

    await loadDetail(idUser, idWorkspace);
  },
  { immediate: true }
);

watch(
  () => [props.open, displayMembership.value?.idUser] as const,
  async ([open, idUser]) => {
    if (!open || !idUser) {
      user.value = null;
      userError.value = '';
      userLoading.value = false;
      return;
    }

    if (props.usersById[idUser]) {
      user.value = null;
      userError.value = '';
      userLoading.value = false;
      return;
    }

    await loadUser(idUser);
  },
  { immediate: true }
);

watch(
  () => [props.open, displayMembership.value?.idWorkspace] as const,
  async ([open, idWorkspace]) => {
    if (!open || !idWorkspace) {
      workspace.value = null;
      workspaceError.value = '';
      workspaceLoading.value = false;
      return;
    }

    if (props.workspacesById[idWorkspace]) {
      workspace.value = null;
      workspaceError.value = '';
      workspaceLoading.value = false;
      return;
    }

    await loadWorkspace(idWorkspace);
  },
  { immediate: true }
);

watch(
  () => [props.open, displayMembership.value?.idRole] as const,
  async ([open, idRole]) => {
    if (!open || !idRole) {
      role.value = null;
      roleError.value = '';
      roleLoading.value = false;
      rolePermissions.value = [];
      rolePermissionsError.value = '';
      return;
    }

    if (props.rolesById[idRole]) {
      role.value = null;
      roleError.value = '';
      roleLoading.value = false;
    } else {
      await loadRole(idRole);
    }

    await loadRolePermissions(idRole);
  },
  { immediate: true }
);

watch(
  () => rolePermissions.value.map((item) => item.idPermission).join('|'),
  async () => {
    if (!props.open || rolePermissions.value.length === 0) {
      loadedPermissions.value = {};
      permissionsError.value = '';
      permissionsLoading.value = false;
      return;
    }

    await loadMissingPermissions(rolePermissions.value.map((item) => item.idPermission));
  }
);

onMounted(() => {
  window.addEventListener('keydown', onKeydown);
});

onBeforeUnmount(() => {
  window.removeEventListener('keydown', onKeydown);
});

async function loadDetail(idUser: string, idWorkspace: string): Promise<void> {
  const version = ++detailLoadVersion;

  detailLoading.value = true;
  detailError.value = '';
  detail.value = null;

  try {
    const response = await getUserWorkspace(idUser, idWorkspace);

    if (version === detailLoadVersion) {
      detail.value = response;
    }
  } catch (err) {
    if (version === detailLoadVersion) {
      detailError.value = getProblemMessage(err, 'Unable to load membership details.');
    }
  } finally {
    if (version === detailLoadVersion) {
      detailLoading.value = false;
    }
  }
}

async function loadUser(id: string): Promise<void> {
  const version = ++userLoadVersion;

  userLoading.value = true;
  userError.value = '';
  user.value = null;

  try {
    const response = await getUser(id);

    if (version === userLoadVersion) {
      user.value = response;
    }
  } catch (err) {
    if (version === userLoadVersion) {
      userError.value = getProblemMessage(err, 'Unable to load linked user.');
    }
  } finally {
    if (version === userLoadVersion) {
      userLoading.value = false;
    }
  }
}

async function loadWorkspace(id: string): Promise<void> {
  const version = ++workspaceLoadVersion;

  workspaceLoading.value = true;
  workspaceError.value = '';
  workspace.value = null;

  try {
    const response = await getWorkspace(id);

    if (version === workspaceLoadVersion) {
      workspace.value = response;
    }
  } catch (err) {
    if (version === workspaceLoadVersion) {
      workspaceError.value = getProblemMessage(err, 'Unable to load linked workspace.');
    }
  } finally {
    if (version === workspaceLoadVersion) {
      workspaceLoading.value = false;
    }
  }
}

async function loadRole(id: string): Promise<void> {
  const version = ++roleLoadVersion;

  roleLoading.value = true;
  roleError.value = '';
  role.value = null;

  try {
    const response = await getRole(id);

    if (version === roleLoadVersion) {
      role.value = response;
    }
  } catch (err) {
    if (version === roleLoadVersion) {
      roleError.value = getProblemMessage(err, 'Unable to load linked role.');
    }
  } finally {
    if (version === roleLoadVersion) {
      roleLoading.value = false;
    }
  }
}

async function loadRolePermissions(idRole: string): Promise<void> {
  const version = ++rolePermissionsLoadVersion;

  rolePermissionsLoading.value = true;
  rolePermissionsError.value = '';
  rolePermissions.value = [];

  try {
    const response = await getRolePermissions({
      page: 1,
      pageSize: 200,
      sort: 'idPermission',
      idRole
    });

    if (version === rolePermissionsLoadVersion) {
      rolePermissions.value = response.items;
    }
  } catch (err) {
    if (version === rolePermissionsLoadVersion) {
      rolePermissionsError.value = getProblemMessage(err, 'Unable to load role permissions.');
    }
  } finally {
    if (version === rolePermissionsLoadVersion) {
      rolePermissionsLoading.value = false;
    }
  }
}

async function loadMissingPermissions(ids: string[]): Promise<void> {
  const version = ++permissionsLoadVersion;
  const uniqueIds = Array.from(new Set(ids)).filter((id) => !props.permissionsById[id]);

  if (uniqueIds.length === 0) {
    loadedPermissions.value = {};
    permissionsError.value = '';
    permissionsLoading.value = false;
    return;
  }

  permissionsLoading.value = true;
  permissionsError.value = '';

  try {
    const responses = await Promise.allSettled(uniqueIds.map((id) => getPermission(id)));

    if (version !== permissionsLoadVersion) {
      return;
    }

    const nextLookup: Record<string, PermissionDetail> = {};
    let failed = false;

    responses.forEach((response, index) => {
      if (response.status === 'fulfilled') {
        nextLookup[uniqueIds[index]] = response.value;
      } else {
        failed = true;
      }
    });

    loadedPermissions.value = nextLookup;

    if (failed) {
      permissionsError.value = 'Some linked permissions could not be loaded. Raw IDs remain visible.';
    }
  } finally {
    if (version === permissionsLoadVersion) {
      permissionsLoading.value = false;
    }
  }
}

function resetDrawerState(): void {
  detail.value = null;
  user.value = null;
  workspace.value = null;
  role.value = null;
  rolePermissions.value = [];
  loadedPermissions.value = {};
  detailError.value = '';
  userError.value = '';
  workspaceError.value = '';
  roleError.value = '';
  rolePermissionsError.value = '';
  permissionsError.value = '';
  detailLoading.value = false;
  userLoading.value = false;
  workspaceLoading.value = false;
  roleLoading.value = false;
  rolePermissionsLoading.value = false;
  permissionsLoading.value = false;
}

function close(): void {
  emit('close');
}

function onKeydown(event: KeyboardEvent): void {
  if (props.open && event.key === 'Escape') {
    close();
  }
}
</script>

<template>
  <Teleport to="body">
    <div v-if="open" class="drawer-shell" role="presentation">
      <button class="drawer-shell__backdrop" type="button" aria-label="Close access detail" @click="close" />

      <aside
        class="drawer app-surface"
        role="dialog"
        aria-modal="true"
        aria-labelledby="access-detail-title"
      >
        <header class="drawer__header">
          <div v-if="displayMembership" class="drawer__title">
            <Badge :tone="getAccessNeutralTone()">
              {{ getRoleLabel(displayMembership.idRole, rolesById) }}
            </Badge>
            <h2 id="access-detail-title">
              {{ getUserLabel(displayMembership.idUser, usersById) }}
            </h2>
            <p>
              Workspace {{ getWorkspaceLabel(displayMembership.idWorkspace, workspacesById) }}
            </p>
          </div>
          <button class="app-icon-button" type="button" aria-label="Close access detail" @click="close">
            <X :size="18" />
          </button>
        </header>

        <div v-if="displayMembership" class="drawer__body">
          <section class="drawer__section drawer__section--summary">
            <div>
              <span>User</span>
              <strong>{{ getUserLabel(displayMembership.idUser, usersById) }}</strong>
            </div>
            <div>
              <span>Workspace</span>
              <strong>{{ getWorkspaceLabel(displayMembership.idWorkspace, workspacesById) }}</strong>
            </div>
            <div>
              <span>Role</span>
              <strong>{{ getRoleLabel(displayMembership.idRole, rolesById) }}</strong>
            </div>
          </section>

          <LoadingState v-if="detailLoading" class="drawer__loading" :rows="3" />

          <section v-if="detailError" class="drawer__notice">
            {{ detailError }} Raw membership fields remain visible from the selected row.
          </section>

          <section class="drawer__section">
            <h3>Membership record</h3>
            <dl class="drawer__fields">
              <div><dt>User ID</dt><dd>{{ displayMembership.idUser }}</dd></div>
              <div><dt>Workspace ID</dt><dd>{{ displayMembership.idWorkspace }}</dd></div>
              <div><dt>Role ID</dt><dd>{{ displayMembership.idRole }}</dd></div>
            </dl>
          </section>

          <section class="drawer__section">
            <header class="linked-header">
              <h3>Linked user</h3>
              <code :title="displayMembership.idUser">{{ compactId(displayMembership.idUser) }}</code>
            </header>

            <LoadingState v-if="userLoading" class="drawer__loading" :rows="3" />

            <div v-else-if="userError" class="drawer__notice">
              {{ userError }} Raw user ID remains visible above.
            </div>

            <dl v-else-if="displayUser" class="drawer__fields drawer__fields--two">
              <div><dt>Login</dt><dd>{{ displayUser.login }}</dd></div>
              <div><dt>Email</dt><dd>{{ fieldValue(displayUser.email) }}</dd></div>
              <div><dt>Phone</dt><dd>{{ displayUser.phone }}</dd></div>
              <div><dt>Status</dt><dd>{{ getStatusLabel(displayUser.status) }}</dd></div>
              <div><dt>Global role ID</dt><dd>{{ displayUser.idRole }}</dd></div>
              <div><dt>Created</dt><dd>{{ formatDateTime(displayUser.dateCreate) }}</dd></div>
              <div><dt>Last login</dt><dd>{{ formatDateTime(displayUser.dateLogin) }}</dd></div>
            </dl>

            <div v-else class="drawer__placeholder">
              No linked user details are available from the loaded backend records.
            </div>
          </section>

          <section class="drawer__section">
            <header class="linked-header">
              <h3>Linked workspace</h3>
              <code :title="displayMembership.idWorkspace">{{ compactId(displayMembership.idWorkspace) }}</code>
            </header>

            <LoadingState v-if="workspaceLoading" class="drawer__loading" :rows="3" />

            <div v-else-if="workspaceError" class="drawer__notice">
              {{ workspaceError }} Raw workspace ID remains visible above.
            </div>

            <dl v-else-if="displayWorkspace" class="drawer__fields drawer__fields--two">
              <div><dt>Name</dt><dd>{{ displayWorkspace.name }}</dd></div>
              <div><dt>Status</dt><dd>{{ getStatusLabel(displayWorkspace.status) }}</dd></div>
              <div><dt>Brand ID</dt><dd>{{ fieldValue(displayWorkspace.idBrand) }}</dd></div>
              <div><dt>Invite URL</dt><dd>{{ fieldValue(displayWorkspace.urlInvite) }}</dd></div>
              <div><dt>Description</dt><dd>{{ fieldValue(displayWorkspace.description) }}</dd></div>
              <div><dt>Created</dt><dd>{{ formatDateTime(displayWorkspace.dateCreate) }}</dd></div>
              <div><dt>Updated</dt><dd>{{ formatDateTime(displayWorkspace.dateUpdate) }}</dd></div>
            </dl>

            <div v-else class="drawer__placeholder">
              No linked workspace details are available from the loaded backend records.
            </div>
          </section>

          <section class="drawer__section">
            <header class="linked-header">
              <h3>Linked role</h3>
              <code :title="displayMembership.idRole">{{ compactId(displayMembership.idRole) }}</code>
            </header>

            <LoadingState v-if="roleLoading" class="drawer__loading" :rows="3" />

            <div v-else-if="roleError" class="drawer__notice">
              {{ roleError }} Raw role ID remains visible above.
            </div>

            <dl v-else-if="displayRole" class="drawer__fields drawer__fields--two">
              <div><dt>Name</dt><dd>{{ displayRole.name }}</dd></div>
              <div><dt>Description</dt><dd>{{ fieldValue(displayRole.description) }}</dd></div>
              <div><dt>Created</dt><dd>{{ formatDateTime(displayRole.dateCreate) }}</dd></div>
              <div><dt>Updated</dt><dd>{{ formatDateTime(displayRole.dateUpdate) }}</dd></div>
            </dl>

            <div v-else class="drawer__placeholder">
              No linked role details are available from the loaded backend records.
            </div>
          </section>

          <section class="drawer__section">
            <header class="linked-header">
              <h3>Persisted role permissions</h3>
              <code>{{ rolePermissions.length }}</code>
            </header>

            <LoadingState v-if="rolePermissionsLoading || permissionsLoading" class="drawer__loading" :rows="3" />

            <div v-else-if="rolePermissionsError" class="drawer__notice">
              {{ rolePermissionsError }}
            </div>

            <div v-else-if="rolePermissions.length === 0" class="drawer__placeholder">
              No role-permission records were returned for this role.
            </div>

            <div v-else class="permission-list">
              <div v-if="permissionsError" class="drawer__notice">{{ permissionsError }}</div>

              <article
                v-for="rolePermission in rolePermissions"
                :key="`${rolePermission.idRole}:${rolePermission.idPermission}`"
                class="permission-row"
              >
                <div>
                  <strong>
                    {{ getPermissionLabel(rolePermission.idPermission, permissionsLookup) }}
                  </strong>
                  <code :title="rolePermission.idPermission">
                    {{ compactId(rolePermission.idPermission) }}
                  </code>
                </div>

                <p v-if="permissionsLookup[rolePermission.idPermission]">
                  {{ permissionsLookup[rolePermission.idPermission].description }}
                </p>

                <dl v-if="permissionsLookup[rolePermission.idPermission]" class="drawer__fields drawer__fields--two">
                  <div>
                    <dt>Domain</dt>
                    <dd>{{ getDomainLabel(permissionsLookup[rolePermission.idPermission].domain) }}</dd>
                  </div>
                  <div>
                    <dt>Category ID</dt>
                    <dd>{{ permissionsLookup[rolePermission.idPermission].idCategory }}</dd>
                  </div>
                </dl>
              </article>
            </div>
          </section>
        </div>
      </aside>
    </div>
  </Teleport>
</template>

<style scoped>
.drawer-shell {
  position: fixed;
  inset: 0;
  z-index: 50;
}

.drawer-shell__backdrop {
  position: absolute;
  inset: 0;
  border: 0;
  background: var(--theme-backdrop);
}

.drawer {
  position: absolute;
  top: var(--space-3);
  right: var(--space-3);
  bottom: var(--space-3);
  display: grid;
  width: min(44rem, calc(100vw - 1.5rem));
  grid-template-rows: auto 1fr;
  overflow: hidden;
}

.drawer__header {
  display: flex;
  align-items: start;
  justify-content: space-between;
  gap: var(--space-3);
  border-bottom: 1px solid var(--color-border);
  background: var(--background-panel-highlight);
  padding: var(--space-4);
}

.drawer__title {
  display: grid;
  min-width: 0;
  gap: var(--space-2);
}

.drawer__title h2 {
  margin: 0;
  overflow-wrap: anywhere;
  font-size: 1rem;
  font-weight: 760;
}

.drawer__title p {
  margin: 0;
  color: var(--color-text-muted);
  font-family: var(--font-mono);
  font-size: 0.78rem;
}

.drawer__body {
  display: grid;
  align-content: start;
  gap: var(--space-3);
  overflow-y: auto;
  padding: var(--space-3);
}

.drawer__section {
  display: grid;
  gap: var(--space-3);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--surface-panel-muted);
  padding: var(--space-3);
}

.drawer__section--summary {
  grid-template-columns: repeat(1, minmax(0, 1fr));
}

.drawer__section--summary div {
  display: grid;
  gap: var(--space-1);
}

.drawer__section--summary span,
.drawer__fields dt {
  color: var(--color-text-muted);
  font-size: 0.72rem;
  font-weight: 680;
  letter-spacing: 0.02em;
  text-transform: uppercase;
}

.drawer__section--summary strong {
  min-width: 0;
  overflow-wrap: anywhere;
  font-size: 0.9rem;
}

.drawer__section h3 {
  margin: 0;
  color: var(--color-text);
  font-size: 0.78rem;
  font-weight: 740;
  text-transform: uppercase;
}

.drawer__fields {
  display: grid;
  gap: var(--space-2);
  margin: 0;
}

.drawer__fields div {
  display: grid;
  gap: 0.2rem;
}

.drawer__fields dd {
  margin: 0;
  overflow-wrap: anywhere;
  color: var(--color-text);
  font-family: var(--font-mono);
  font-size: 0.78rem;
}

.drawer__notice,
.drawer__placeholder {
  border: 1px dashed var(--color-border);
  border-radius: var(--radius-sm);
  color: var(--color-text-muted);
  padding: var(--space-3);
  font-size: 0.8125rem;
}

.drawer__notice {
  border-color: var(--state-danger-border);
  background: var(--state-danger-soft);
  color: var(--state-danger);
}

.drawer__loading {
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--surface-panel-muted);
}

.linked-header,
.permission-row > div {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-2);
}

.linked-header code,
.permission-row code {
  color: var(--color-text-muted);
  font-family: var(--font-mono);
  font-size: 0.75rem;
}

.permission-list {
  display: grid;
  gap: var(--space-2);
}

.permission-row {
  display: grid;
  gap: var(--space-2);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  background: var(--surface-control);
  padding: var(--space-3);
}

.permission-row strong {
  color: var(--color-text);
  font-size: 0.86rem;
}

.permission-row p {
  margin: 0;
  color: var(--color-text-muted);
  line-height: 1.5;
}

@media (min-width: 680px) {
  .drawer__section--summary {
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }

  .drawer__fields--two {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}
</style>

