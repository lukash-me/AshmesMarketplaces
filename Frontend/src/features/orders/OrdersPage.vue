<script setup lang="ts">
import { ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';

import { getProblemMessage } from '@/shared/api/problemDetails';
import EmptyState from '@/shared/ui/EmptyState.vue';
import LoadingState from '@/shared/ui/LoadingState.vue';
import PageHeader from '@/widgets/PageHeader.vue';

import OrderDetailDrawer from './OrderDetailDrawer.vue';
import { getOrders } from './orders.api';
import OrdersFilters from './OrdersFilters.vue';
import {
  parseOrdersQuery,
  removeOrderQueryFilter,
  resetOrderQueryFilters,
  toOrdersApiParams,
  toOrdersRouteQuery,
  type OrderQueryFilterKey
} from './ordersQuery';
import OrdersTable from './OrdersTable.vue';
import type { OrderListItem, OrderQueryState } from './orders.types';

const route = useRoute();
const router = useRouter();

const queryState = ref<OrderQueryState>(parseOrdersQuery(route.query));
const orders = ref<OrderListItem[]>([]);
const totalCount = ref(0);
const loading = ref(false);
const error = ref('');
const selectedOrder = ref<OrderListItem | null>(null);

watch(
  () => route.query,
  async (query) => {
    queryState.value = parseOrdersQuery(query);
    await loadOrders();
  },
  { immediate: true }
);

async function loadOrders() {
  loading.value = true;
  error.value = '';

  try {
    const response = await getOrders(toOrdersApiParams(queryState.value));
    orders.value = response.items;
    totalCount.value = response.totalCount;
  } catch (err) {
    orders.value = [];
    totalCount.value = 0;
    error.value = getProblemMessage(err, 'Unable to load orders.');
  } finally {
    loading.value = false;
  }
}

async function updateQuery(patch: Partial<OrderQueryState>) {
  const nextState = {
    ...queryState.value,
    ...patch
  };

  await router.replace({
    query: toOrdersRouteQuery(nextState)
  });
}

function resetFilters() {
  void router.replace({
    query: toOrdersRouteQuery(resetOrderQueryFilters(queryState.value))
  });
}

function removeFilter(key: OrderQueryFilterKey) {
  void router.replace({
    query: toOrdersRouteQuery(removeOrderQueryFilter(queryState.value, key))
  });
}

function openOrder(row: OrderListItem) {
  selectedOrder.value = row;
}

function closeOrder() {
  selectedOrder.value = null;
}
</script>

<template>
  <div class="orders-page">
    <PageHeader
      title="Orders"
      description="Read-only order operations view with fulfillment route, status code, price, quantity and timeline filters."
    />

    <OrdersFilters
      :state="queryState"
      @apply="updateQuery"
      @reset="resetFilters"
      @remove="removeFilter"
    />

    <LoadingState v-if="loading" class="app-surface" />

    <EmptyState
      v-else-if="error"
      class="app-surface"
      title="Orders could not be loaded"
      :description="error"
    />

    <EmptyState
      v-else-if="orders.length === 0"
      class="app-surface"
      title="No orders found"
      description="Adjust filters or load orders through the existing backend API."
    />

    <OrdersTable
      v-else
      :rows="orders"
      :page="queryState.page"
      :page-size="queryState.pageSize"
      :total-count="totalCount"
      :sort="queryState.sort"
      :selected-id="selectedOrder?.id"
      @sort="updateQuery({ page: 1, sort: $event })"
      @page="updateQuery({ page: $event })"
      @open="openOrder"
    />

    <OrderDetailDrawer
      :open="Boolean(selectedOrder)"
      :order="selectedOrder"
      @close="closeOrder"
    />
  </div>
</template>

<style scoped>
.orders-page {
  display: grid;
  gap: var(--space-4);
}
</style>
