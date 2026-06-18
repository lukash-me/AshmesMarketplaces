<script setup lang="ts">
import { computed, onBeforeUnmount, reactive, ref, watch } from 'vue';

import { recalculateWorkspaceOverview } from '@/features/overview/workspaceOverview.api';
import { getParserDemoCardCharacteristics } from '@/features/parser-products/parserProducts.api';
import type { ParserDemoCardOptions } from '@/features/parser-products/parserProducts.types';
import { getProblemMessage } from '@/shared/api/problemDetails';
import Button from '@/shared/ui/Button.vue';
import HelpTooltip from '@/shared/ui/HelpTooltip.vue';

import { addDemoWorkspaceMarketProduct } from './workspaceMarketProducts.api';
import type {
  CreateDemoWorkspaceMarketProductRequest,
  WorkspaceMarketProductDetail
} from './workspaceMarketProducts.types';

const props = defineProps<{
  workspaceId: string | null;
  options: ParserDemoCardOptions;
}>();

const emit = defineEmits<{
  created: [WorkspaceMarketProductDetail];
  cancel: [];
}>();

const creating = ref(false);
const fileInput = ref<HTMLInputElement | null>(null);
const characteristicValues = reactive<Record<string, string>>({});
const initialCategory = props.options.categories.length === 1 ? props.options.categories[0] : '';
const formError = ref<string | null>(null);
const formStatus = ref<string | null>(null);
const characteristicsLoading = ref(false);
const characteristicsError = ref<string | null>(null);
const loadedCharacteristicsBySubcategory = reactive<Record<string, string[]>>({});
let characteristicsRequestVersion = 0;

interface DemoMediaItem {
  id: string;
  file: File;
  previewUrl: string;
}

const form = reactive({
  name: '',
  sourceCategory: initialCategory,
  sourceSubcategory: '',
  price: '',
  costPrice: '',
  description: '',
  supplierName: '',
  supplierUrl: '',
  note: ''
});

const mediaItems = ref<DemoMediaItem[]>([]);

const subcategoryOptions = computed(() => {
  const category = form.sourceCategory.trim();
  if (!category) {
    return [];
  }

  return props.options.subcategoriesByCategory.find((item) => item.category === category)?.subcategories ?? [];
});

const selectedCharacteristics = computed(() => {
  const subcategory = form.sourceSubcategory.trim();
  if (!subcategory) {
    return [];
  }

  return loadedCharacteristicsBySubcategory[subcategory]
    ?? props.options.characteristicsBySubcategory.find((item) => item.subcategory === subcategory)?.characteristics
    ?? [];
});

watch(
  () => form.sourceCategory,
  () => {
    form.sourceSubcategory = '';
    resetCharacteristicValues();
  }
);

watch(
  () => form.sourceSubcategory,
  (subcategory) => {
    resetCharacteristicValues();
    void loadCharacteristicsForSubcategory(subcategory);
  }
);

async function loadCharacteristicsForSubcategory(subcategory: string): Promise<void> {
  const normalized = subcategory.trim();
  const version = ++characteristicsRequestVersion;
  characteristicsError.value = null;

  if (!normalized || loadedCharacteristicsBySubcategory[normalized]) {
    characteristicsLoading.value = false;
    return;
  }

  characteristicsLoading.value = true;
  try {
    const response = await getParserDemoCardCharacteristics(normalized);
    if (version === characteristicsRequestVersion) {
      loadedCharacteristicsBySubcategory[normalized] = response.characteristics;
    }
  } catch (error) {
    if (version === characteristicsRequestVersion) {
      characteristicsError.value = getProblemMessage(
        error,
        'Не удалось загрузить характеристики выбранной ниши.'
      );
    }
  } finally {
    if (version === characteristicsRequestVersion) {
      characteristicsLoading.value = false;
    }
  }
}

function resetCharacteristicValues(): void {
  for (const key of Object.keys(characteristicValues)) {
    delete characteristicValues[key];
  }
}

function mediaKey(file: File): string {
  return `${file.name}:${file.size}:${file.lastModified}`;
}

function revokeMediaItem(item: DemoMediaItem): void {
  URL.revokeObjectURL(item.previewUrl);
}

function onFiles(event: Event): void {
  const input = event.target as HTMLInputElement;
  formError.value = null;
  const selectedFiles = Array.from(input.files ?? []).filter((file) => file.type.startsWith('image/'));
  if (!selectedFiles.length) {
    input.value = '';
    return;
  }

  const existingKeys = new Set(mediaItems.value.map((item) => mediaKey(item.file)));
  const availableSlots = Math.max(0, 10 - mediaItems.value.length);
  const nextItems = selectedFiles
    .filter((file) => !existingKeys.has(mediaKey(file)))
    .slice(0, availableSlots)
    .map((file) => ({
      id: `${mediaKey(file)}:${crypto.randomUUID()}`,
      file,
      previewUrl: URL.createObjectURL(file)
    }));

  if (nextItems.length > 0) {
    mediaItems.value = [...mediaItems.value, ...nextItems];
  }

  if (selectedFiles.length > nextItems.length && mediaItems.value.length >= 10) {
    formError.value = 'Можно загрузить не больше 10 изображений.';
  }

  input.value = '';
}

function removeMedia(id: string): void {
  const item = mediaItems.value.find((mediaItem) => mediaItem.id === id);
  if (!item) {
    return;
  }

  revokeMediaItem(item);
  mediaItems.value = mediaItems.value.filter((mediaItem) => mediaItem.id !== id);
  formError.value = null;
}

function clearMedia(): void {
  for (const item of mediaItems.value) {
    revokeMediaItem(item);
  }
  mediaItems.value = [];
}

function resetForm(): void {
  form.name = '';
  form.sourceCategory = initialCategory;
  form.sourceSubcategory = '';
  form.price = '';
  form.costPrice = '';
  form.description = '';
  form.supplierName = '';
  form.supplierUrl = '';
  form.note = '';
  clearMedia();
  resetCharacteristicValues();
  if (fileInput.value) {
    fileInput.value.value = '';
  }
}

function buildCharacteristicsJson(): string | null {
  const values = selectedCharacteristics.value
    .map((name) => ({ key: name, value: (characteristicValues[name] ?? '').trim() }))
    .filter((item) => item.value.length > 0);

  return values.length ? JSON.stringify(Object.fromEntries(values.map((item) => [item.key, item.value]))) : null;
}

function parseDecimalInput(value: string | number): number {
  return Number(String(value).replace(',', '.'));
}

async function createProduct(): Promise<void> {
  const workspaceId = props.workspaceId;
  if (creating.value) {
    return;
  }

  formError.value = null;
  formStatus.value = null;
  if (!workspaceId) {
    formError.value = 'Рабочая область недоступна. Обновите страницу и попробуйте снова.';
    return;
  }

  const price = parseDecimalInput(form.price);
  const costPrice = String(form.costPrice).trim() ? parseDecimalInput(form.costPrice) : null;
  if (
    !form.name.trim()
    || !form.sourceCategory.trim()
    || !form.sourceSubcategory.trim()
    || !Number.isFinite(price)
    || price <= 0
  ) {
    formError.value = 'Заполните название, категорию, нишу и положительную цену.';
    return;
  }

  if (costPrice !== null && (!Number.isFinite(costPrice) || costPrice < 0)) {
    formError.value = 'Себестоимость должна быть положительным числом.';
    return;
  }

  const request: CreateDemoWorkspaceMarketProductRequest = {
    name: form.name.trim(),
    sourceCategory: form.sourceCategory.trim(),
    sourceSubcategory: form.sourceSubcategory.trim(),
    price,
    costPrice,
    description: form.description.trim() || null,
    characteristicsJson: buildCharacteristicsJson(),
    supplierName: form.supplierName.trim() || null,
    supplierUrl: form.supplierUrl.trim() || null,
    note: form.note.trim() || null,
    media: mediaItems.value.map((item) => item.file)
  };

  creating.value = true;
  formStatus.value = 'Создаем карточку...';
  try {
    const created = await addDemoWorkspaceMarketProduct(workspaceId, request);
    formStatus.value = 'Карточка создана. Пересчитываем анализ в фоне...';
    resetForm();
    void recalculateWorkspaceOverview(workspaceId).catch((error) => {
      console.error('Failed to recalculate workspace overview after demo card creation.', error);
    });
    emit('created', created);
  } catch (requestError) {
    console.error('Failed to create demo market product.', requestError);
    formError.value = getProblemMessage(requestError, 'Не удалось создать карточку.');
    formStatus.value = null;
  } finally {
    creating.value = false;
  }
}

onBeforeUnmount(() => {
  clearMedia();
});
</script>

<template>
  <form class="demo-product-form app-surface app-operator-panel" @submit.prevent>
    <label class="demo-product-form__field">
      <span class="demo-product-form__label">
        Название
        <HelpTooltip text="Название будущей карточки так, как вы хотите видеть его в аналитике и сравнении с конкурентами." />
      </span>
      <input v-model="form.name" class="demo-product-form__input" type="text" required />
    </label>

    <div class="demo-product-form__grid">
      <label class="demo-product-form__field">
        <span class="demo-product-form__label">
          Категория
          <HelpTooltip text="Категория маркетплейса из текущих выгрузок парсера. Она нужна, чтобы подобрать корректную нишу и конкурентов." />
        </span>
        <select v-model="form.sourceCategory" class="demo-product-form__input" required>
          <option value="" disabled>Выберите категорию</option>
          <option v-for="category in options.categories" :key="category" :value="category">
            {{ category }}
          </option>
        </select>
      </label>
      <label class="demo-product-form__field">
        <span class="demo-product-form__label">
          Ниша
          <HelpTooltip text="Ниша WB из выбранной категории. По ней карточка будет сравниваться с похожими товарами." />
        </span>
        <select
          v-model="form.sourceSubcategory"
          class="demo-product-form__input"
          required
          :disabled="!form.sourceCategory"
        >
          <option value="" disabled>Выберите нишу</option>
          <option v-for="subcategory in subcategoryOptions" :key="subcategory" :value="subcategory">
            {{ subcategory }}
          </option>
        </select>
      </label>
    </div>

    <div class="demo-product-form__grid">
      <label class="demo-product-form__field">
        <span class="demo-product-form__label">
          Цена
          <HelpTooltip text="Плановая цена продажи. По ней ИИ сравнит карточку с конкурентами и рассчитает ценовой коридор." />
        </span>
        <input v-model="form.price" class="demo-product-form__input" type="number" min="1" step="0.01" required />
      </label>
      <label class="demo-product-form__field">
        <span class="demo-product-form__label">
          Себестоимость
          <HelpTooltip text="Опционально. Если указать себестоимость, рекомендации по цене будут учитывать минимальную маржу." />
        </span>
        <input v-model="form.costPrice" class="demo-product-form__input" type="number" min="0" step="0.01" />
      </label>
    </div>

    <label class="demo-product-form__field">
      <span class="demo-product-form__label">
        Описание
        <HelpTooltip text="Черновик описания товара. ИИ проверит полноту текста и предложит улучшения без выдумывания свойств." />
      </span>
      <textarea v-model="form.description" class="demo-product-form__textarea" rows="5" />
    </label>

    <section class="demo-product-form__chars">
      <span class="demo-product-form__label">
        Характеристики
        <HelpTooltip text="Названия характеристик взяты из WB-выгрузок для выбранной ниши. Заполните только те значения, которые уже известны." />
      </span>
      <p v-if="!form.sourceSubcategory" class="demo-product-form__hint">Выберите нишу, чтобы увидеть характеристики WB.</p>
      <p v-else-if="characteristicsLoading" class="demo-product-form__hint">Загружаем характеристики выбранной ниши...</p>
      <p v-else-if="characteristicsError" class="demo-product-form__message demo-product-form__message--error">{{ characteristicsError }}</p>
      <p v-else-if="!selectedCharacteristics.length" class="demo-product-form__hint">
        Для выбранной ниши характеристики пока не найдены в выгрузках.
      </p>
      <div v-else class="demo-product-form__characteristics-grid">
        <label
          v-for="characteristic in selectedCharacteristics"
          :key="characteristic"
          class="demo-product-form__field demo-product-form__field--compact"
        >
          <span class="demo-product-form__label demo-product-form__label--plain">
            {{ characteristic }}
            <HelpTooltip :text="`Значение характеристики «${characteristic}» для будущей карточки. Название взято из WB-выгрузки выбранной ниши.`" />
          </span>
          <input
            v-model="characteristicValues[characteristic]"
            class="demo-product-form__input"
            type="text"
            placeholder="Значение"
          />
        </label>
      </div>
    </section>

    <div class="demo-product-form__grid">
      <label class="demo-product-form__field">
        <span class="demo-product-form__label">
          Поставщик
          <HelpTooltip text="Название поставщика или фабрики. Поле нужно для вашей внутренней проверки идеи." />
        </span>
        <input v-model="form.supplierName" class="demo-product-form__input" type="text" />
      </label>
      <label class="demo-product-form__field">
        <span class="demo-product-form__label">
          Ссылка на поставщика
          <HelpTooltip text="Ссылка на источник товара, поставщика или референс. Она хранится только в карточке наблюдения." />
        </span>
        <input v-model="form.supplierUrl" class="demo-product-form__input" type="url" />
      </label>
    </div>

    <label class="demo-product-form__field">
      <span class="demo-product-form__label">
        Фото / референсы
        <HelpTooltip text="До 10 изображений товара или референсов. Первое изображение станет обложкой demo-карточки." />
      </span>
      <input
        ref="fileInput"
        class="demo-product-form__file-input"
        type="file"
        accept="image/*"
        multiple
        @change="onFiles"
      />
      <div class="demo-product-form__upload-control">
        <button
          class="demo-product-form__upload-button"
          type="button"
          :disabled="mediaItems.length >= 10"
          @click="fileInput?.click()"
        >
          Выбрать файлы
        </button>
        <span class="demo-product-form__hint">Выбрано файлов: {{ mediaItems.length }} из 10</span>
      </div>
      <div v-if="mediaItems.length" class="demo-product-form__media-preview" aria-label="Предпросмотр выбранных изображений">
        <div
          v-for="(item, index) in mediaItems"
          :key="item.id"
          class="demo-product-form__media-item"
        >
          <div class="demo-product-form__media-thumb">
            <img :src="item.previewUrl" :alt="item.file.name" />
            <span v-if="index === 0" class="demo-product-form__media-cover">Обложка</span>
          </div>
          <div class="demo-product-form__media-meta">
            <span class="demo-product-form__media-name" :title="item.file.name">{{ item.file.name }}</span>
            <button
              class="demo-product-form__media-remove"
              type="button"
              :aria-label="`Удалить ${item.file.name}`"
              @click="removeMedia(item.id)"
            >
              Удалить
            </button>
          </div>
        </div>
      </div>
    </label>

    <label class="demo-product-form__field">
      <span class="demo-product-form__label">
        Заметка
        <HelpTooltip text="Внутренняя заметка по идее: условия поставщика, гипотезы, риски или что нужно проверить позже." />
      </span>
      <textarea v-model="form.note" class="demo-product-form__textarea" rows="2" />
    </label>

    <p v-if="formError" class="demo-product-form__message demo-product-form__message--error">{{ formError }}</p>
    <p v-else-if="formStatus" class="demo-product-form__message demo-product-form__message--status">{{ formStatus }}</p>

    <div class="demo-product-form__actions">
      <Button type="button" variant="secondary" :disabled="creating" @click="emit('cancel')">Отмена</Button>
      <button
        class="demo-product-form__create-button"
        type="button"
        :disabled="!workspaceId || creating"
        @click="createProduct"
      >
        <span v-if="creating" class="demo-product-form__create-spinner" />
        Создать
      </button>
    </div>
  </form>
</template>

<style scoped>
.demo-product-form {
  display: grid;
  width: min(100%, 62rem);
  margin-inline: auto;
  gap: var(--space-4);
  border-color: var(--operator-border-muted);
  padding: var(--space-4);
}

.demo-product-form__grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: var(--space-3);
}

.demo-product-form__field,
.demo-product-form__chars {
  display: grid;
  gap: var(--space-1);
  min-width: 0;
  color: var(--color-text-muted);
  font-size: var(--operator-meta-size);
  font-weight: 650;
}

.demo-product-form__chars {
  gap: var(--space-2);
}

.demo-product-form__label {
  display: inline-flex;
  align-items: center;
  gap: var(--space-1);
  width: fit-content;
  color: var(--color-text-muted);
  font-size: var(--operator-label-size);
  font-weight: 760;
  letter-spacing: 0.02em;
  line-height: 1.15;
  text-transform: uppercase;
}

.demo-product-form__label--plain {
  align-items: flex-start;
  width: auto;
  text-transform: none;
}

.demo-product-form__input,
.demo-product-form__textarea {
  width: 100%;
  border: 1px solid var(--select-control-border);
  border-radius: var(--radius-md);
  background: var(--select-control-bg);
  color: var(--select-control-text);
  outline: none;
  transition: background 140ms ease, border-color 140ms ease, box-shadow 140ms ease, color 140ms ease;
}

.demo-product-form__input {
  min-height: 2.35rem;
  padding: 0.55rem 0.7rem;
}

.demo-product-form__textarea {
  min-height: 4.5rem;
  line-height: 1.45;
  padding: 0.6rem 0.75rem;
  resize: vertical;
}

.demo-product-form__input:hover,
.demo-product-form__textarea:hover {
  border-color: var(--select-control-border-hover);
  background: var(--select-control-bg-hover);
}

.demo-product-form__input:focus,
.demo-product-form__textarea:focus {
  border-color: var(--select-control-border-focus);
  background: var(--select-control-bg-focus);
  box-shadow: var(--focus-ring);
}

.demo-product-form__input:disabled {
  cursor: not-allowed;
  opacity: 0.62;
}

.demo-product-form__characteristics-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(16rem, 1fr));
  gap: var(--space-3);
}

.demo-product-form__field--compact {
  align-content: start;
}

.demo-product-form__hint {
  margin: 0;
  color: var(--color-text-muted);
  font-size: var(--operator-meta-size);
  font-weight: 650;
}

.demo-product-form__file-input {
  position: absolute;
  width: 1px;
  height: 1px;
  overflow: hidden;
  clip: rect(0 0 0 0);
  clip-path: inset(50%);
  white-space: nowrap;
}

.demo-product-form__upload-control {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--space-2);
}

.demo-product-form__upload-button {
  min-height: 2.25rem;
  border: 1px solid var(--select-control-border);
  border-radius: var(--radius-md);
  background: var(--select-control-bg);
  color: var(--select-control-text);
  cursor: pointer;
  font-size: var(--operator-meta-size);
  font-weight: 760;
  padding: 0 var(--space-3);
  transition: background 140ms ease, border-color 140ms ease, box-shadow 140ms ease;
}

.demo-product-form__upload-button:hover:not(:disabled) {
  border-color: var(--select-control-border-hover);
  background: var(--select-control-bg-hover);
}

.demo-product-form__upload-button:focus-visible {
  outline: none;
  box-shadow: var(--focus-ring);
}

.demo-product-form__upload-button:disabled {
  cursor: not-allowed;
  opacity: 0.62;
}

.demo-product-form__media-preview {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(7.5rem, 1fr));
  gap: var(--space-2);
}

.demo-product-form__media-item {
  display: grid;
  min-width: 0;
  gap: var(--space-1);
}

.demo-product-form__media-thumb {
  position: relative;
  overflow: hidden;
  aspect-ratio: 1;
  border: 1px solid var(--operator-border-muted);
  border-radius: var(--radius-sm);
  background: var(--color-surface-muted);
}

.demo-product-form__media-thumb img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.demo-product-form__media-cover {
  position: absolute;
  top: 0.35rem;
  left: 0.35rem;
  border: 1px solid color-mix(in srgb, var(--color-success) 42%, transparent);
  border-radius: var(--radius-sm);
  background: color-mix(in srgb, var(--color-success) 12%, var(--color-surface));
  color: var(--color-success);
  font-size: 0.68rem;
  font-weight: 800;
  line-height: 1;
  padding: 0.22rem 0.34rem;
}

.demo-product-form__media-meta {
  display: flex;
  align-items: center;
  min-width: 0;
  gap: var(--space-1);
}

.demo-product-form__media-name {
  min-width: 0;
  overflow: hidden;
  color: var(--color-text-muted);
  font-size: var(--operator-meta-size);
  font-weight: 650;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.demo-product-form__media-remove {
  flex: 0 0 auto;
  border: 0;
  background: transparent;
  color: var(--color-danger);
  cursor: pointer;
  font-size: var(--operator-meta-size);
  font-weight: 760;
  padding: 0;
}

.demo-product-form__media-remove:hover {
  color: var(--color-danger);
  text-decoration: underline;
}

.demo-product-form__message {
  margin: 0;
  border: 1px solid transparent;
  border-radius: var(--radius-sm);
  font-size: var(--operator-meta-size);
  font-weight: 700;
  line-height: 1.35;
  padding: 0.65rem 0.75rem;
}

.demo-product-form__message--error {
  border-color: color-mix(in srgb, var(--color-danger) 42%, transparent);
  background: color-mix(in srgb, var(--color-danger) 9%, var(--color-surface));
  color: var(--color-danger);
}

.demo-product-form__message--status {
  border-color: color-mix(in srgb, var(--color-success) 32%, transparent);
  background: color-mix(in srgb, var(--color-success) 8%, var(--color-surface));
  color: var(--color-success);
}

.demo-product-form__actions {
  display: flex;
  flex-wrap: wrap;
  justify-content: flex-end;
  gap: var(--space-2);
}

.demo-product-form__create-button {
  display: inline-flex;
  min-height: 2.125rem;
  align-items: center;
  justify-content: center;
  gap: var(--space-2);
  border: 1px solid var(--accent-primary-border);
  border-radius: var(--radius-md);
  background: var(--button-primary-bg);
  background-clip: padding-box;
  color: var(--text-on-fire);
  cursor: pointer;
  font-size: 0.8125rem;
  font-weight: 680;
  line-height: 1;
  padding: 0 var(--space-3);
  transition: background-color 140ms ease, border-color 140ms ease, color 140ms ease;
}

.demo-product-form__create-button:hover:not(:disabled) {
  border-color: var(--accent-primary-hover-border);
  background: var(--button-primary-bg-hover);
}

.demo-product-form__create-button:focus-visible {
  outline: none;
  box-shadow: var(--focus-ring);
}

.demo-product-form__create-button:disabled {
  cursor: not-allowed;
  opacity: 0.6;
}

.demo-product-form__create-spinner {
  height: 0.875rem;
  width: 0.875rem;
  border: 2px solid currentColor;
  border-right-color: transparent;
  border-radius: 999px;
  animation: demo-product-form-spin 700ms linear infinite;
}

@keyframes demo-product-form-spin {
  to {
    transform: rotate(360deg);
  }
}

@media (max-width: 760px) {
  .demo-product-form {
    padding: var(--space-3);
  }

  .demo-product-form__grid {
    grid-template-columns: 1fr;
  }
}
</style>
