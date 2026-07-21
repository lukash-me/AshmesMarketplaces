<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import { useRouter } from 'vue-router';

import AuthRequiredState from '@/features/auth/AuthRequiredState.vue';
import { useAuthStore } from '@/features/auth/auth.store';
import { getParserDemoCardOptions } from '@/features/parser-products/parserProducts.api';
import type { ParserDemoCardOptions } from '@/features/parser-products/parserProducts.types';
import { getProblemMessage } from '@/shared/api/problemDetails';
import Button from '@/shared/ui/Button.vue';
import EmptyState from '@/shared/ui/EmptyState.vue';
import LoadingState from '@/shared/ui/LoadingState.vue';
import PageHeader from '@/widgets/PageHeader.vue';

import DemoMarketProductForm from './DemoMarketProductForm.vue';
import { useActiveWorkspace } from './useActiveWorkspace';

const router = useRouter();
const auth = useAuthStore();
const workspace = useActiveWorkspace();
const options = ref<ParserDemoCardOptions | null>(null);
const optionsLoading = ref(false);
const optionsError = ref<string | null>(null);

const hasOptions = computed(() =>
  Boolean(options.value?.categories.length && options.value.subcategoriesByCategory.length)
);
const isGuest = computed(() => !auth.isAuthenticated);

onMounted(() => {
  if (!isGuest.value) {
    void loadOptions();
  }
});

watch(isGuest, (guest) => {
  if (!guest && !options.value && !optionsLoading.value) {
    void loadOptions();
  }
});

async function loadOptions(): Promise<void> {
  optionsLoading.value = true;
  optionsError.value = null;
  try {
    options.value = await getParserDemoCardOptions();
  } catch (error) {
    optionsError.value = getProblemMessage(error, 'Не удалось загрузить справочник категорий и характеристик.');
  } finally {
    optionsLoading.value = false;
  }
}

function goToList(): void {
  void router.push('/workspace/market-products');
}

function goToWorkspaces(): void {
  void router.push('/management/workspaces');
}
</script>

<template>
  <div class="workspace-product-create">
    <PageHeader
      title="Создать карточку"
      description="Создайте локальную аналитическую карточку продавца. Она сразу попадет в наблюдаемые товары и будет сравниваться с конкурентами из актуальных данных маркетплейса."
    />
    <AuthRequiredState
      v-if="isGuest"
      description="Создание карточки добавляет ваш товар в наблюдаемые и запускает сравнение с конкурентами. Войдите, чтобы сохранить карточку в своей рабочей области."
    />

    <template v-else>
    <EmptyState
      v-if="!workspace.activeWorkspaceId.value"
      title="Вне рабочей области"
      description="Создайте или выберите рабочую область, чтобы создать карточку."
    >
      <Button variant="primary" @click="goToWorkspaces">Рабочие области</Button>
    </EmptyState>
    <LoadingState v-else-if="optionsLoading" :rows="6" class="workspace-product-create__state" />
    <EmptyState
      v-else-if="optionsError"
      title="Справочник не загружен"
      :description="optionsError"
    >
      <Button type="button" variant="secondary" @click="loadOptions">Повторить</Button>
    </EmptyState>
    <EmptyState
      v-else-if="!options || !hasOptions"
      title="Нет данных выгрузки для выбора ниши"
      description="Сначала нужна успешная выгрузка товаров и характеристик по поддерживаемым нишам."
    />
    <DemoMarketProductForm
      v-else
      :workspace-id="workspace.activeWorkspaceId.value"
      :options="options"
      @created="goToList"
      @cancel="goToList"
    />
    </template>
  </div>
</template>

<style scoped>
.workspace-product-create {
  display: grid;
  gap: var(--space-4);
}

.workspace-product-create__field {
  display: grid;
  gap: var(--space-1);
  width: min(100%, 26rem);
  margin-inline: auto;
  color: var(--color-text-muted);
  font-size: var(--operator-meta-size);
  font-weight: 650;
}

.workspace-product-create__field > span {
  color: var(--color-text-muted);
  font-size: var(--operator-label-size);
  font-weight: 760;
  letter-spacing: 0.02em;
  line-height: 1.15;
  text-transform: uppercase;
}

.workspace-product-create__state {
  width: min(100%, 62rem);
  margin-inline: auto;
}
</style>
