<script setup lang="ts">
import { ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';

import { getProblemMessage } from '@/shared/api/problemDetails';
import EmptyState from '@/shared/ui/EmptyState.vue';
import LoadingState from '@/shared/ui/LoadingState.vue';
import PageHeader from '@/widgets/PageHeader.vue';

import ReviewDetailDrawer from './ReviewDetailDrawer.vue';
import { getReviews } from './reviews.api';
import ReviewsFilters from './ReviewsFilters.vue';
import {
  parseReviewsQuery,
  removeReviewQueryFilter,
  resetReviewQueryFilters,
  toReviewsApiParams,
  toReviewsRouteQuery,
  type ReviewQueryFilterKey
} from './reviewsQuery';
import ReviewsTable from './ReviewsTable.vue';
import type { ReviewListItem, ReviewQueryState } from './reviews.types';

const route = useRoute();
const router = useRouter();

const queryState = ref<ReviewQueryState>(parseReviewsQuery(route.query));
const reviews = ref<ReviewListItem[]>([]);
const totalCount = ref(0);
const loading = ref(false);
const error = ref('');
const selectedReview = ref<ReviewListItem | null>(null);

watch(
  () => route.query,
  async (query) => {
    queryState.value = parseReviewsQuery(query);
    await loadReviews();
  },
  { immediate: true }
);

async function loadReviews() {
  loading.value = true;
  error.value = '';

  try {
    const response = await getReviews(toReviewsApiParams(queryState.value));
    reviews.value = response.items;
    totalCount.value = response.totalCount;
  } catch (err) {
    reviews.value = [];
    totalCount.value = 0;
    error.value = getProblemMessage(err, 'Unable to load reviews.');
  } finally {
    loading.value = false;
  }
}

async function updateQuery(patch: Partial<ReviewQueryState>) {
  const nextState = {
    ...queryState.value,
    ...patch
  };

  await router.replace({
    query: toReviewsRouteQuery(nextState)
  });
}

function resetFilters() {
  void router.replace({
    query: toReviewsRouteQuery(resetReviewQueryFilters(queryState.value))
  });
}

function removeFilter(key: ReviewQueryFilterKey) {
  void router.replace({
    query: toReviewsRouteQuery(removeReviewQueryFilter(queryState.value, key))
  });
}

function openReview(row: ReviewListItem) {
  selectedReview.value = row;
}

function closeReview() {
  selectedReview.value = null;
}
</script>

<template>
  <div class="reviews-page">
    <PageHeader
      title="Отзывы моих товаров"
      description="Отзывы по товарам пользователя: идентификаторы маркетплейса, рейтинги, ответы и контекст товара."
    />

    <ReviewsFilters
      :state="queryState"
      @apply="updateQuery"
      @reset="resetFilters"
      @remove="removeFilter"
    />

    <LoadingState v-if="loading" class="app-surface" />

    <EmptyState
      v-else-if="error"
      class="app-surface"
      title="Не удалось загрузить отзывы моих товаров"
      :description="error"
    />

    <EmptyState
      v-else-if="reviews.length === 0"
      class="app-surface"
      title="Отзывы моих товаров не найдены"
      description="Измените фильтры или загрузите отзывы через существующий backend API."
    />

    <ReviewsTable
      v-else
      :rows="reviews"
      :page="queryState.page"
      :page-size="queryState.pageSize"
      :total-count="totalCount"
      :sort="queryState.sort"
      :selected-id="selectedReview?.id"
      @sort="updateQuery({ page: 1, sort: $event })"
      @page="updateQuery({ page: $event })"
      @open="openReview"
    />

    <ReviewDetailDrawer
      :open="Boolean(selectedReview)"
      :review="selectedReview"
      @close="closeReview"
    />
  </div>
</template>

<style scoped>
.reviews-page {
  display: grid;
  gap: var(--space-4);
}
</style>
