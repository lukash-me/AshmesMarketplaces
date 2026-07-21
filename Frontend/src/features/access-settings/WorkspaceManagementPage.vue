<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { ChevronDown, Pencil, Plus, Trash2, Users, X } from 'lucide-vue-next';

import AuthRequiredState from '@/features/auth/AuthRequiredState.vue';
import { useAuthStore } from '@/features/auth/auth.store';
import MarketFilterSelect from '@/features/parser-products/MarketFilterSelect.vue';
import { getProblemMessage } from '@/shared/api/problemDetails';
import Button from '@/shared/ui/Button.vue';
import EmptyState from '@/shared/ui/EmptyState.vue';
import LoadingState from '@/shared/ui/LoadingState.vue';
import PageHeader from '@/widgets/PageHeader.vue';

import {
  addManagementWorkspaceMember,
  createManagementWorkspace,
  deleteManagementWorkspace,
  deleteManagementWorkspaceMember,
  getManagementWorkspaceRoles,
  getManagementWorkspaces,
  updateManagementWorkspace,
  updateManagementWorkspaceMemberRole
} from './accessSettings.api';
import type { ManagementWorkspace, ManagementWorkspaceMember, ManagementWorkspaceRole } from './accessSettings.types';

type ModalKind = 'workspace' | 'member' | 'delete-member' | 'delete-workspace';

interface PendingMemberDelete {
  workspaceId: string;
  member: ManagementWorkspaceMember;
}

const auth = useAuthStore();

const loading = ref(false);
const error = ref('');
const workspaces = ref<ManagementWorkspace[]>([]);
const roles = ref<ManagementWorkspaceRole[]>([]);

const expandedWorkspaceIds = ref<Set<string>>(new Set());
const activeModal = ref<ModalKind | null>(null);

const editingWorkspaceId = ref<string | null>(null);
const workspaceName = ref('');
const workspaceDescription = ref('');
const workspaceSubmitting = ref(false);
const workspaceNameError = ref('');
const workspaceFormError = ref('');

const membershipWorkspaceId = ref('');
const membershipEmail = ref('');
const membershipRoleId = ref('');
const membershipSubmitting = ref(false);
const membershipEmailError = ref('');
const membershipRoleError = ref('');
const membershipGeneralError = ref('');
const memberRoleErrors = ref<Record<string, string>>({});
const updatingMemberRoleKeys = ref<Set<string>>(new Set());
const pendingMemberDelete = ref<PendingMemberDelete | null>(null);
const deletingMember = ref(false);
const deleteMemberError = ref('');
const pendingWorkspaceDelete = ref<ManagementWorkspace | null>(null);
const deletingWorkspace = ref(false);
const deleteWorkspaceError = ref('');

const isGuest = computed(() => !auth.isAuthenticated);

const visibleWorkspaces = computed(() =>
  [...workspaces.value].sort((left, right) => left.name.localeCompare(right.name, 'ru-RU'))
);

const selectedMembershipWorkspace = computed(() =>
  visibleWorkspaces.value.find((workspace) => workspace.id === membershipWorkspaceId.value) ?? null
);

const workspaceModalTitle = computed(() =>
  editingWorkspaceId.value ? 'Изменить рабочую область' : 'Создать рабочую область'
);

const workspaceSubmitLabel = computed(() =>
  editingWorkspaceId.value ? 'Сохранить' : 'Создать'
);

const modalTitle = computed(() => {
  if (activeModal.value === 'workspace') {
    return workspaceModalTitle.value;
  }

  if (activeModal.value === 'delete-member') {
    return 'Удалить пользователя';
  }

  if (activeModal.value === 'delete-workspace') {
    return 'Удалить рабочую область';
  }

  return 'Добавить пользователя';
});

const roleOptions = computed(() => roles.value.map((role) => roleLabel(role.name)));

const membershipRoleLabel = computed({
  get: () => roleLabelById(membershipRoleId.value),
  set: (value: string) => {
    membershipRoleId.value = roleIdByLabel(value);
  }
});

const roleLabelByIdLookup = computed(() =>
  roles.value.reduce<Record<string, string>>((lookup, role) => {
    lookup[role.id] = roleLabel(role.name);
    return lookup;
  }, {})
);

const roleIdByLabelLookup = computed(() =>
  roles.value.reduce<Record<string, string>>((lookup, role) => {
    lookup[roleLabel(role.name)] = role.id;
    return lookup;
  }, {})
);

function getWorkspaceDescription(workspace: ManagementWorkspace): string {
  if (workspace.description === 'Personal workspace created during registration.') {
    return '';
  }

  return workspace.description || '';
}

watch(isGuest, (guest) => {
  if (!guest) {
    void loadData();
  } else {
    workspaces.value = [];
  }
});

if (!isGuest.value) {
  void loadData();
}

async function loadData() {
  loading.value = true;
  error.value = '';

  try {
    const [workspaceItems, roleItems] = await Promise.all([
      getManagementWorkspaces(),
      getManagementWorkspaceRoles()
    ]);
    workspaces.value = workspaceItems;
    roles.value = roleItems;
  } catch (err) {
    workspaces.value = [];
    roles.value = [];
    error.value = getProblemMessage(err, 'Не удалось загрузить рабочие области.');
  } finally {
    loading.value = false;
  }
}

function getWorkspaceMembers(workspace: ManagementWorkspace): ManagementWorkspaceMember[] {
  return [...workspace.members].sort((left, right) =>
    getMemberLabel(left).localeCompare(getMemberLabel(right), 'ru-RU')
  );
}

function getMemberLabel(member: ManagementWorkspaceMember): string {
  return member.login || member.email || 'Пользователь';
}

function getMemberMeta(member: ManagementWorkspaceMember): string {
  if (member.email && member.email !== member.login) {
    return member.email;
  }

  return '';
}

function roleLabel(name: string): string {
  const normalized = name.trim().toLocaleLowerCase('ru-RU');
  const labels: Record<string, string> = {
    admin: 'Администратор',
    administrator: 'Администратор',
    manager: 'Менеджер',
    analyst: 'Аналитик',
    viewer: 'Наблюдатель'
  };

  return labels[normalized] ?? name;
}

function roleLabelById(idRole: string): string {
  return roleLabelByIdLookup.value[idRole] ?? '';
}

function roleIdByLabel(label: string): string {
  return roleIdByLabelLookup.value[label] ?? '';
}

function toggleWorkspace(workspaceId: string) {
  const next = new Set(expandedWorkspaceIds.value);
  if (next.has(workspaceId)) {
    next.delete(workspaceId);
  } else {
    next.add(workspaceId);
  }

  expandedWorkspaceIds.value = next;
}

function openWorkspaceModal() {
  editingWorkspaceId.value = null;
  workspaceName.value = '';
  workspaceDescription.value = '';
  workspaceNameError.value = '';
  workspaceFormError.value = '';
  activeModal.value = 'workspace';
}

function openWorkspaceEditModal(workspace: ManagementWorkspace) {
  editingWorkspaceId.value = workspace.id;
  workspaceName.value = workspace.name;
  workspaceDescription.value = workspace.description === 'Personal workspace created during registration.'
    ? ''
    : workspace.description ?? '';
  workspaceNameError.value = '';
  workspaceFormError.value = '';
  activeModal.value = 'workspace';
}

function openMemberModal(workspaceId: string) {
  membershipWorkspaceId.value = workspaceId;
  membershipEmail.value = '';
  membershipRoleId.value = roles.value[0]?.id ?? '';
  membershipEmailError.value = '';
  membershipRoleError.value = '';
  membershipGeneralError.value = '';
  activeModal.value = 'member';
}

function openDeleteMemberModal(workspaceId: string, member: ManagementWorkspaceMember) {
  pendingMemberDelete.value = { workspaceId, member };
  deleteMemberError.value = '';
  activeModal.value = 'delete-member';
}

function openDeleteWorkspaceModal() {
  const workspace = visibleWorkspaces.value.find((item) => item.id === editingWorkspaceId.value);
  if (!workspace) {
    return;
  }

  pendingWorkspaceDelete.value = workspace;
  deleteWorkspaceError.value = '';
  activeModal.value = 'delete-workspace';
}

function closeModal() {
  if (workspaceSubmitting.value || membershipSubmitting.value || deletingMember.value || deletingWorkspace.value) {
    return;
  }

  activeModal.value = null;
  pendingMemberDelete.value = null;
  deleteMemberError.value = '';
  pendingWorkspaceDelete.value = null;
  deleteWorkspaceError.value = '';
}

async function submitWorkspace() {
  workspaceNameError.value = '';
  workspaceFormError.value = '';

  const name = workspaceName.value.trim();
  if (!name) {
    workspaceNameError.value = 'Введите название рабочей области.';
    return;
  }

  workspaceSubmitting.value = true;
  try {
    const request = {
      name,
      description: workspaceDescription.value.trim() || null
    };
    const workspace = editingWorkspaceId.value
      ? await updateManagementWorkspace(editingWorkspaceId.value, request)
      : await createManagementWorkspace(request);

    expandedWorkspaceIds.value = new Set([...expandedWorkspaceIds.value, workspace.id]);
    activeModal.value = null;
    await reloadData();
  } catch (err) {
    workspaceFormError.value = getProblemMessage(
      err,
      editingWorkspaceId.value
        ? 'Не удалось изменить рабочую область.'
        : 'Не удалось создать рабочую область.'
    );
  } finally {
    workspaceSubmitting.value = false;
  }
}

async function submitMembership() {
  membershipEmailError.value = '';
  membershipRoleError.value = '';
  membershipGeneralError.value = '';

  const email = membershipEmail.value.trim();
  let hasError = false;
  if (!email) {
    membershipEmailError.value = 'Введите email зарегистрированного пользователя.';
    hasError = true;
  } else if (!isValidEmail(email)) {
    membershipEmailError.value = 'Введите корректный email.';
    hasError = true;
  }

  if (!membershipRoleId.value) {
    membershipRoleError.value = 'Выберите роль пользователя.';
    hasError = true;
  }

  if (!membershipWorkspaceId.value || hasError) {
    return;
  }

  membershipSubmitting.value = true;
  try {
    await addManagementWorkspaceMember(membershipWorkspaceId.value, {
      email,
      idRole: membershipRoleId.value
    });

    expandedWorkspaceIds.value = new Set([...expandedWorkspaceIds.value, membershipWorkspaceId.value]);
    activeModal.value = null;
    await reloadData();
  } catch (err) {
    membershipGeneralError.value = getProblemMessage(err, 'Не удалось добавить пользователя.');
  } finally {
    membershipSubmitting.value = false;
  }
}

async function updateMemberRole(workspaceId: string, member: ManagementWorkspaceMember, idRole: string) {
  if (!idRole || idRole === member.idRole) {
    return;
  }

  const key = getMemberRoleKey(workspaceId, member.idUser);
  memberRoleErrors.value = { ...memberRoleErrors.value, [key]: '' };
  updatingMemberRoleKeys.value = new Set([...updatingMemberRoleKeys.value, key]);

  try {
    await updateManagementWorkspaceMemberRole(workspaceId, member.idUser, { idRole });
    await reloadData();
  } catch (err) {
    memberRoleErrors.value = {
      ...memberRoleErrors.value,
      [key]: getProblemMessage(err, 'Не удалось изменить роль пользователя.')
    };
  } finally {
    const next = new Set(updatingMemberRoleKeys.value);
    next.delete(key);
    updatingMemberRoleKeys.value = next;
  }
}

function updateMemberRoleByLabel(workspaceId: string, member: ManagementWorkspaceMember, label: string) {
  const idRole = roleIdByLabel(label);
  void updateMemberRole(workspaceId, member, idRole);
}

async function confirmDeleteMember() {
  const pending = pendingMemberDelete.value;
  if (!pending) {
    return;
  }

  deleteMemberError.value = '';
  deletingMember.value = true;

  try {
    await deleteManagementWorkspaceMember(pending.workspaceId, pending.member.idUser);
    activeModal.value = null;
    pendingMemberDelete.value = null;
    await reloadData();
  } catch (err) {
    deleteMemberError.value = getProblemMessage(err, 'Не удалось удалить пользователя из рабочей области.');
  } finally {
    deletingMember.value = false;
  }
}

async function confirmDeleteWorkspace() {
  const pending = pendingWorkspaceDelete.value;
  if (!pending) {
    return;
  }

  deleteWorkspaceError.value = '';
  deletingWorkspace.value = true;

  try {
    await deleteManagementWorkspace(pending.id);
    activeModal.value = null;
    pendingWorkspaceDelete.value = null;
    editingWorkspaceId.value = null;
    const nextExpanded = new Set(expandedWorkspaceIds.value);
    nextExpanded.delete(pending.id);
    expandedWorkspaceIds.value = nextExpanded;
    await reloadData();
  } catch (err) {
    deleteWorkspaceError.value = getProblemMessage(err, 'Не удалось удалить рабочую область.');
  } finally {
    deletingWorkspace.value = false;
  }
}

function getMemberRoleKey(workspaceId: string, userId: string): string {
  return `${workspaceId}:${userId}`;
}

function isValidEmail(value: string): boolean {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value);
}

async function reloadData() {
  await auth.bootstrap();
  await loadData();
}
</script>

<template>
  <div class="workspace-management-page">
    <PageHeader
      title="Рабочие области"
      description="Создавайте рабочие области и управляйте пользователями, которые имеют к ним доступ."
    />

    <AuthRequiredState
      v-if="isGuest"
      description="Рабочие области и участники доступны после входа. Авторизуйтесь, чтобы управлять командой."
    />

    <template v-else>
      <div class="workspace-management-page__toolbar">
        <Button variant="primary" @click="openWorkspaceModal">
          <Plus :size="16" />
          Создать рабочую область
        </Button>
      </div>

      <LoadingState v-if="loading" class="app-surface" />

      <EmptyState
        v-else-if="error"
        class="app-surface"
        title="Не удалось загрузить рабочие области"
        :description="error"
      />

      <EmptyState
        v-else-if="visibleWorkspaces.length === 0"
        class="app-surface"
        title="Рабочие области не найдены"
        description="Создайте рабочую область, чтобы разделить расходы, наблюдаемые товары и аналитику по командам."
      />

      <section v-else class="workspace-card-list" aria-label="Рабочие области">
        <article
          v-for="workspace in visibleWorkspaces"
          :key="workspace.id"
          class="workspace-card app-surface"
        >
          <div
            class="workspace-card__summary"
            role="button"
            tabindex="0"
            :aria-expanded="expandedWorkspaceIds.has(workspace.id)"
            @click="toggleWorkspace(workspace.id)"
            @keydown.enter.prevent="toggleWorkspace(workspace.id)"
            @keydown.space.prevent="toggleWorkspace(workspace.id)"
          >
            <button
              class="workspace-card__toggle"
              type="button"
              :aria-expanded="expandedWorkspaceIds.has(workspace.id)"
              @click.stop="toggleWorkspace(workspace.id)"
            >
              <span class="workspace-card__icon"><Users :size="18" /></span>
            </button>
            <span class="workspace-card__title">
              <span class="workspace-card__editable-line">
                <button class="workspace-card__text-button" type="button" @click.stop="toggleWorkspace(workspace.id)">
                  <strong>{{ workspace.name }}</strong>
                </button>
                <button
                  class="workspace-card__edit"
                  type="button"
                  aria-label="Изменить название рабочей области"
                  title="Изменить название"
                  @click.stop="openWorkspaceEditModal(workspace)"
                >
                  <Pencil :size="14" />
                </button>
              </span>
              <span
                v-if="getWorkspaceDescription(workspace)"
                class="workspace-card__editable-line workspace-card__editable-line--description"
              >
                <button class="workspace-card__text-button" type="button" @click.stop="toggleWorkspace(workspace.id)">
                  <small>{{ getWorkspaceDescription(workspace) }}</small>
                </button>
              </span>
            </span>
            <button
              class="workspace-card__count"
              type="button"
              :aria-expanded="expandedWorkspaceIds.has(workspace.id)"
              @click.stop="toggleWorkspace(workspace.id)"
            >
              Пользователей: {{ workspace.members.length }}
            </button>
            <button
              class="workspace-card__chevron-button"
              type="button"
              :aria-expanded="expandedWorkspaceIds.has(workspace.id)"
              aria-label="Раскрыть рабочую область"
              @click.stop="toggleWorkspace(workspace.id)"
            >
              <ChevronDown
                :size="18"
                class="workspace-card__chevron"
                :class="{ 'workspace-card__chevron--open': expandedWorkspaceIds.has(workspace.id) }"
              />
            </button>
          </div>

          <div v-if="expandedWorkspaceIds.has(workspace.id)" class="workspace-card__details">
            <div class="workspace-card__details-header">
              <Button variant="secondary" @click="openMemberModal(workspace.id)">
                <Plus :size="15" />
                Добавить пользователя
              </Button>
            </div>

            <div v-if="workspace.members.length" class="workspace-member-list">
              <div
                v-for="member in getWorkspaceMembers(workspace)"
                :key="member.idUser"
                class="workspace-member-list__row"
              >
                <span class="workspace-member-list__identity">
                  <strong>
                    {{ getMemberLabel(member) }}
                    <small v-if="member.isCurrentUser" class="workspace-member-list__self">Вы</small>
                  </strong>
                  <small v-if="getMemberMeta(member)">{{ getMemberMeta(member) }}</small>
                </span>
                <span class="workspace-member-list__role">
                  <small
                    v-if="memberRoleErrors[getMemberRoleKey(workspace.id, member.idUser)]"
                    class="workspace-field-error"
                  >
                    {{ memberRoleErrors[getMemberRoleKey(workspace.id, member.idUser)] }}
                  </small>
                  <span class="workspace-member-list__role-control">
                    <MarketFilterSelect
                      class="workspace-role-filter"
                      :class="{ 'workspace-role-filter--updating': updatingMemberRoleKeys.has(getMemberRoleKey(workspace.id, member.idUser)) }"
                      label="Роль"
                      :model-value="roleLabelById(member.idRole)"
                      :options="roleOptions"
                      placeholder="Выберите роль"
                      search-placeholder="Найти роль"
                      :clearable="false"
                      @update:model-value="updateMemberRoleByLabel(workspace.id, member, $event)"
                    />
                    <button
                      class="workspace-member-list__delete"
                      type="button"
                      :aria-label="`Удалить пользователя ${getMemberLabel(member)}`"
                      title="Удалить пользователя"
                      @click="openDeleteMemberModal(workspace.id, member)"
                    >
                      <Trash2 :size="16" />
                    </button>
                  </span>
                </span>
              </div>
            </div>

            <p v-else class="workspace-card__empty">Пользователи еще не добавлены.</p>
          </div>
        </article>
      </section>
    </template>

    <Teleport to="body">
      <div
        v-if="activeModal"
        class="workspace-modal"
        role="presentation"
        @click.self="closeModal"
      >
        <section class="workspace-modal__panel app-surface" role="dialog" aria-modal="true">
          <header class="workspace-modal__header">
            <div>
              <h2>{{ modalTitle }}</h2>
              <p v-if="activeModal === 'workspace'">
                {{
                  'В каждой рабочей области собственные наблюдаемые товары, пользователи, расходы'
                }}
              </p>
            </div>
            <button class="app-icon-button" type="button" aria-label="Закрыть" @click="closeModal">
              <X :size="18" />
            </button>
          </header>

          <form v-if="activeModal === 'workspace'" class="workspace-modal__form" @submit.prevent="submitWorkspace">
            <label>
              <span>Название</span>
              <small v-if="workspaceNameError" class="workspace-field-error">
                {{ workspaceNameError }}
              </small>
              <input
                v-model="workspaceName"
                class="app-input"
                type="text"
                autocomplete="off"
                placeholder="Например, Команда ковриков"
              />
            </label>

            <label>
              <span>Описание</span>
              <textarea
                v-model="workspaceDescription"
                class="app-textarea"
                rows="3"
                placeholder="Кратко опишите назначение рабочей области"
              />
            </label>

            <p v-if="workspaceFormError" class="workspace-modal__error">{{ workspaceFormError }}</p>

            <footer
              class="workspace-modal__actions"
              :class="{ 'workspace-modal__actions--split': editingWorkspaceId }"
            >
              <Button
                v-if="editingWorkspaceId"
                variant="danger"
                :disabled="workspaceSubmitting"
                @click="openDeleteWorkspaceModal"
              >
                Удалить рабочую область
              </Button>
              <span class="workspace-modal__actions-main">
                <Button variant="secondary" @click="closeModal">Отмена</Button>
                <Button type="submit" variant="primary" :loading="workspaceSubmitting">{{ workspaceSubmitLabel }}</Button>
              </span>
            </footer>
          </form>

          <form v-else-if="activeModal === 'member'" class="workspace-modal__form" @submit.prevent="submitMembership">
            <div class="workspace-modal__readonly">
              <span>Рабочая область</span>
              <strong>{{ selectedMembershipWorkspace?.name || 'Не выбрана' }}</strong>
            </div>

            <label>
              <span>Email пользователя</span>
              <small v-if="membershipEmailError" class="workspace-field-error">
                {{ membershipEmailError }}
              </small>
              <input
                v-model="membershipEmail"
                class="app-input"
                type="text"
                autocomplete="email"
                placeholder="seller@example.com"
              />
            </label>

            <label>
              <span>Роль</span>
              <small v-if="membershipRoleError" class="workspace-field-error">
                {{ membershipRoleError }}
              </small>
              <MarketFilterSelect
                v-model="membershipRoleLabel"
                label="Роль"
                :options="roleOptions"
                placeholder="Выберите роль"
                search-placeholder="Найти роль"
                :clearable="false"
              />
            </label>

            <p v-if="membershipGeneralError" class="workspace-modal__error">{{ membershipGeneralError }}</p>

            <footer class="workspace-modal__actions">
              <Button variant="secondary" @click="closeModal">Отмена</Button>
              <Button type="submit" variant="primary" :loading="membershipSubmitting">Добавить</Button>
            </footer>
          </form>

          <div v-else-if="activeModal === 'delete-member'" class="workspace-modal__form">
            <p class="workspace-modal__confirm-text">
              Вы уверены, что хотите удалить пользователя {{ pendingMemberDelete ? getMemberLabel(pendingMemberDelete.member) : '' }}?
            </p>

            <p v-if="deleteMemberError" class="workspace-modal__error">{{ deleteMemberError }}</p>

            <footer class="workspace-modal__actions">
              <Button variant="secondary" @click="closeModal">Отмена</Button>
              <Button variant="danger" :loading="deletingMember" @click="confirmDeleteMember">
                Удалить
              </Button>
            </footer>
          </div>

          <div v-else class="workspace-modal__form">
            <p class="workspace-modal__confirm-text">
              Вы уверены, что хотите удалить рабочую область {{ pendingWorkspaceDelete?.name || '' }}?
            </p>

            <p v-if="deleteWorkspaceError" class="workspace-modal__error">{{ deleteWorkspaceError }}</p>

            <footer class="workspace-modal__actions">
              <Button variant="secondary" @click="closeModal">Отмена</Button>
              <Button variant="danger" :loading="deletingWorkspace" @click="confirmDeleteWorkspace">
                Удалить
              </Button>
            </footer>
          </div>
        </section>
      </div>
    </Teleport>
  </div>
</template>

<style scoped>
.workspace-management-page {
  display: grid;
  gap: var(--space-4);
}

.workspace-management-page__toolbar {
  display: flex;
  justify-content: flex-start;
}

.workspace-card-list {
  display: grid;
  gap: var(--space-3);
  overflow: visible;
}

.workspace-card {
  position: relative;
  overflow: visible;
}

.workspace-card:focus-within {
  z-index: 20;
}

.workspace-card__summary {
  display: grid;
  width: 100%;
  grid-template-columns: auto minmax(0, 1fr) auto auto;
  align-items: center;
  gap: var(--space-3);
  background: transparent;
  padding: var(--space-4);
  text-align: left;
}

.workspace-card__summary:hover,
.workspace-card__toggle:hover,
.workspace-card__text-button:hover,
.workspace-card__count:hover,
.workspace-card__chevron-button:hover {
  background: var(--surface-active-overlay);
}

.workspace-card__toggle,
.workspace-card__text-button,
.workspace-card__count,
.workspace-card__chevron-button,
.workspace-card__edit {
  border: 0;
  background: transparent;
  color: inherit;
  font: inherit;
}

.workspace-card__toggle,
.workspace-card__count,
.workspace-card__chevron-button {
  border-radius: var(--radius-md);
}

.workspace-card__toggle,
.workspace-card__chevron-button {
  display: grid;
  place-items: center;
}

.workspace-card__text-button {
  min-width: 0;
  padding: 0;
  text-align: left;
}

.workspace-card__edit {
  display: grid;
  width: 1.65rem;
  height: 1.65rem;
  flex: 0 0 auto;
  place-items: center;
  border-radius: 999px;
  color: var(--color-text-muted);
}

.workspace-card__edit:hover {
  background: var(--color-surface-hover);
  color: var(--accent-ember-text-strong);
}

.workspace-card__icon {
  display: grid;
  width: 2.25rem;
  height: 2.25rem;
  place-items: center;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  color: var(--accent-ember-text-strong);
}

.workspace-card__title {
  display: grid;
  gap: 0.2rem;
  min-width: 0;
}

.workspace-card__editable-line {
  display: flex;
  min-width: 0;
  align-items: center;
  gap: var(--space-1);
}

.workspace-card__editable-line--description {
  align-items: start;
}

.workspace-card__title strong {
  color: var(--color-text);
  font-size: 1rem;
}

.workspace-card__title small,
.workspace-card__count,
.workspace-member-list small,
.workspace-modal__header p,
.workspace-modal__readonly span {
  color: var(--color-text-muted);
}

.workspace-card__count {
  padding: 0.45rem var(--space-2);
  font-size: 0.8125rem;
  font-weight: 700;
}

.workspace-card__chevron {
  color: var(--color-text-muted);
  transition: transform 140ms ease;
}

.workspace-card__chevron--open {
  transform: rotate(180deg);
}

.workspace-card__details {
  position: relative;
  z-index: 2;
  display: grid;
  gap: var(--space-3);
  overflow: visible;
  border-top: 1px solid var(--color-border);
  padding: var(--space-4);
}

.workspace-card__details-header {
  display: flex;
  align-items: center;
  justify-content: flex-start;
  gap: var(--space-3);
}

.workspace-member-list {
  display: grid;
  gap: var(--space-2);
  margin: 0;
  padding: 0;
  overflow: visible;
}

.workspace-member-list__row {
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(15rem, 18rem);
  align-items: center;
  gap: var(--space-3);
}

.workspace-member-list__row {
  position: relative;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  padding: var(--space-3);
}

.workspace-member-list__row:focus-within {
  z-index: 40;
}

.workspace-member-list__identity,
.workspace-member-list__role {
  display: grid;
  gap: 0.15rem;
  min-width: 0;
}

.workspace-member-list strong {
  display: flex;
  align-items: center;
  gap: var(--space-2);
}

.workspace-member-list__self {
  border: 1px solid var(--accent-primary-border);
  border-radius: 999px;
  padding: 0.05rem 0.4rem;
  color: var(--accent-primary-text);
  font-size: 0.6875rem;
}

.workspace-member-list__role {
  gap: var(--space-1);
}

.workspace-member-list__role-control {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  align-items: center;
  gap: var(--space-2);
  overflow: visible;
}

.workspace-member-list__delete {
  display: grid;
  width: 2.35rem;
  height: 2.35rem;
  place-items: center;
  border: 1px solid var(--state-danger-border);
  border-radius: var(--radius-md);
  background: var(--state-danger-bg);
  color: var(--state-danger);
}

.workspace-member-list__delete:hover {
  border-color: var(--state-danger-border-strong);
  background: var(--button-danger-bg);
  color: var(--text-on-danger);
}

.workspace-member-list__delete:focus-visible {
  outline: none;
  box-shadow: var(--focus-ring);
}

.workspace-role-filter {
  position: relative;
  z-index: 3;
  min-width: 12rem;
}

.workspace-role-filter :deep(.filter-select--open) {
  z-index: 80;
}

.workspace-role-filter--updating {
  pointer-events: none;
  opacity: 0.65;
}

.workspace-card__empty {
  margin: 0;
  color: var(--color-text-muted);
  font-size: 0.8125rem;
}

.workspace-modal {
  position: fixed;
  inset: 0;
  z-index: 80;
  display: grid;
  place-items: center;
  padding: var(--space-4);
  background: var(--theme-backdrop);
  backdrop-filter: blur(4px);
}

.workspace-modal__panel {
  display: grid;
  gap: var(--space-4);
  width: min(32rem, 100%);
  padding: var(--space-4);
}

.workspace-modal__header {
  display: flex;
  align-items: start;
  justify-content: space-between;
  gap: var(--space-3);
}

.workspace-modal__header h2,
.workspace-modal__header p {
  margin: 0;
}

.workspace-modal__header h2 {
  font-size: 1.125rem;
}

.workspace-modal__header p {
  margin-top: var(--space-1);
  font-size: 0.875rem;
}

.workspace-modal__form {
  display: grid;
  gap: var(--space-3);
}

.workspace-modal__form label {
  display: grid;
  gap: var(--space-1);
  color: var(--color-text-muted);
  font-size: 0.75rem;
  font-weight: 700;
}

.workspace-modal__form :is(.app-input, .app-textarea) {
  width: 100%;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--color-surface);
  padding: 0.65rem 0.8rem;
  color: var(--color-text);
  font: inherit;
  font-size: 0.875rem;
  font-weight: 600;
}

.workspace-modal__form :is(.app-input, .app-textarea)::placeholder {
  color: var(--color-text-muted);
  font-weight: 600;
  opacity: 0.7;
}

.workspace-modal__form :is(.app-input, .app-textarea):focus {
  outline: none;
  border-color: var(--accent-primary-border);
  box-shadow: var(--focus-ring);
}

.workspace-role-filter :deep(.filter-select__trigger) {
  min-height: 2.35rem;
}

.workspace-modal__form :deep(.filter-select__trigger) {
  min-height: 2.85rem;
}

.workspace-role-filter :deep(.filter-select__label),
.workspace-modal__form :deep(.filter-select__label) {
  text-transform: none;
}

.workspace-field-error {
  color: var(--state-danger);
  font-size: 0.75rem;
  font-weight: 700;
}

.workspace-modal__readonly {
  display: flex;
  align-items: baseline;
  gap: var(--space-2);
  color: var(--color-text-muted);
  font-size: 0.8125rem;
}

.workspace-modal__readonly strong {
  color: var(--color-text);
  font-size: 0.875rem;
}

.workspace-modal__error {
  margin: 0;
  border: 1px solid var(--state-danger-border);
  border-radius: var(--radius-md);
  background: var(--state-danger-bg);
  padding: var(--space-3);
  color: var(--state-danger);
  font-size: 0.8125rem;
}

.workspace-modal__confirm-text {
  margin: 0;
  color: var(--color-text);
  font-size: 0.95rem;
  line-height: 1.5;
}

.workspace-modal__actions {
  display: flex;
  justify-content: flex-end;
  gap: var(--space-2);
}

.workspace-modal__actions--split {
  justify-content: space-between;
}

.workspace-modal__actions-main {
  display: inline-flex;
  gap: var(--space-2);
}

@media (max-width: 720px) {
  .workspace-card__summary,
  .workspace-member-list__row,
  .workspace-card__details-header {
    align-items: stretch;
  }

  .workspace-card__summary {
    grid-template-columns: auto 1fr auto;
  }

  .workspace-card__count {
    grid-column: 2 / -1;
  }

  .workspace-member-list__row,
  .workspace-card__details-header {
    grid-template-columns: 1fr;
  }

  .workspace-modal__actions--split,
  .workspace-modal__actions-main {
    display: grid;
    width: 100%;
  }
}
</style>
