<script setup lang="ts">
import { computed } from 'vue';
import { useRoute } from 'vue-router';
import {
  Boxes,
  BookmarkCheck,
  LayoutDashboard,
  Radar,
  Settings,
  ShieldCheck,
  X
} from 'lucide-vue-next';
import { useAuthStore } from '@/features/auth/auth.store';

defineProps<{
  open: boolean;
}>();

defineEmits<{
  close: [];
}>();

type OrdersSection = 'assumed-orders' | 'new-products' | 'restocks' | 'availability';

const route = useRoute();
const auth = useAuthStore();
const workspaceMarketProductsPath = '/workspace/market-products';
const workspaceMarketProductCreatePath = '/workspace/market-products/create';
const workspaceMarketProductClustersPath = '/workspace/market-products/clusters';
const marketProductsPath = '/market/products';
const ruleConstructorPath = '/market/opportunities';
const marketTopForecastPath = '/market/intelligence/top-forecast';
const marketIntelligencePath = '/market/intelligence';
const marketIntelligenceConcentrationPath = '/market/intelligence/concentration';
const marketIntelligencePriceQualityHash = '#price-quality';
const ordersPath = '/orders';
const ordersAvailabilityPath = '/orders/availability';
const expensesPath = '/expenses';
const managementWorkspacesPath = '/management/workspaces';
const adminParserPath = '/admin/parser';
const adminCalculationsPath = '/admin/calculations';
const adminCatalogsPath = '/admin/catalogs';
const ordersSections: Array<{ key: OrdersSection; label: string }> = [
  { key: 'assumed-orders', label: 'Уменьшения остатков' },
  { key: 'new-products', label: 'Новые карточки' },
  { key: 'restocks', label: 'Пополнение остатков' },
  { key: 'availability', label: 'Доступность товара' }
];
const ordersSectionKeys = ordersSections.map((section) => section.key);

const primaryNavItems = [
  { to: '/overview', label: 'Обзор', icon: LayoutDashboard }
];

const isMarketIntelligenceRoute = computed(() =>
  route.path === ruleConstructorPath
  || route.path === marketProductsPath
  || route.path === marketTopForecastPath
  || route.path === marketIntelligencePath
  || route.path === marketIntelligenceConcentrationPath
);
const activeMarketIntelligenceSection = computed(() => {
  if (route.path === marketTopForecastPath) {
    return 'top-forecast';
  }

  if (route.path === ruleConstructorPath) {
    return 'rule-constructor';
  }

  if (route.path === marketProductsPath) {
    return 'market-products';
  }

  if (route.path === marketIntelligenceConcentrationPath) {
    return 'market-concentration';
  }

  if (route.path === marketIntelligencePath) {
    return 'price-quality';
  }

  return null;
});
const isWorkspaceMarketProductsRoute = computed(() => route.path.startsWith(workspaceMarketProductsPath));
const isWorkspaceMarketProductCreateRoute = computed(() => route.path === workspaceMarketProductCreatePath);
const isWorkspaceMarketProductClustersRoute = computed(() => route.path === workspaceMarketProductClustersPath);
const isOrdersRoute = computed(() => route.path === ordersPath || route.path === ordersAvailabilityPath);
const isAdmin = computed(() => auth.user?.roleName === 'Admin');
const isManagementRoute = computed(() =>
  route.path === expensesPath || route.path === managementWorkspacesPath
);
const isExpensesRoute = computed(() => route.path === expensesPath);
const isManagementWorkspacesRoute = computed(() => route.path === managementWorkspacesPath);
const isAdminRoute = computed(() =>
  route.path === adminParserPath || route.path === adminCalculationsPath || route.path === adminCatalogsPath
);
const isAdminParserRoute = computed(() => route.path === adminParserPath);
const isAdminCalculationsRoute = computed(() => route.path === adminCalculationsPath);
const isAdminCatalogsRoute = computed(() => route.path === adminCatalogsPath);
const activeOrdersSection = computed(() =>
  route.path === ordersAvailabilityPath
    ? 'availability'
    : isOrdersRoute.value
      ? normalizeOrdersSection(route.query.tab)
      : null
);

function normalizeOrdersSection(value: unknown): OrdersSection {
  const rawValue = Array.isArray(value) ? value[0] : value;
  const raw = typeof rawValue === 'string' ? rawValue : 'assumed-orders';

  if (raw === 'stock-changes') {
    return 'restocks';
  }

  if (raw === 'all') {
    return 'assumed-orders';
  }

  return ordersSectionKeys.includes(raw as OrdersSection)
    ? raw as OrdersSection
    : 'assumed-orders';
}

function ordersSectionTo(section: OrdersSection) {
  if (section === 'availability') {
    return {
      path: ordersAvailabilityPath
    };
  }

  return {
    path: ordersPath,
    query: section === 'assumed-orders' ? {} : { tab: section }
  };
}
</script>

<template>
  <div v-if="open" class="sidebar__backdrop" @click="$emit('close')" />
  <aside class="sidebar" :class="{ 'sidebar--open': open }">
    <div class="sidebar__brand">
      <div class="sidebar__mark">
        <svg viewBox="0 0 96 150" aria-hidden="true" focusable="false">
          <path
            class="sidebar__flame-outer"
            d="M47 145C25 131 10 111 11 86c1-21 13-33 18-48 4-12 1-23-4-34 19 11 31 29 30 48 11-12 17-29 13-48 21 19 29 43 23 66 8-7 12-17 11-29 13 17 17 39 10 60-8 25-31 40-65 44Z"
          />
          <path
            class="sidebar__flame-middle"
            d="M49 132c-18-11-28-26-27-45 1-16 11-25 20-36 7-9 9-20 6-32 17 13 23 30 17 49 10-7 16-18 17-33 13 15 17 32 11 49 7-4 12-11 15-21 5 19 0 39-13 52-10 10-24 16-46 17Z"
          />
          <path
            class="sidebar__flame-inner"
            d="M50 126c-13-9-20-21-18-35 2-12 11-20 20-30 8-9 11-17 10-27 13 13 15 27 8 42 7-3 12-9 16-18 5 17 1 34-10 47-7 9-15 16-26 21Z"
          />
          <path
            class="sidebar__flame-core"
            d="M52 116c-8-7-11-15-8-25 2-8 9-14 15-21 4-5 7-11 7-18 8 10 8 21 2 32 5-2 9-6 12-12 1 16-9 34-28 44Z"
          />
        </svg>
      </div>
      <div>
        <strong>Ashmes</strong>
        <span>Marketplaces</span>
      </div>
      <button class="app-icon-button sidebar__close" type="button" @click="$emit('close')">
        <X :size="18" />
      </button>
    </div>

    <nav class="sidebar__nav">
      <RouterLink
        v-for="item in primaryNavItems"
        :key="item.to"
        class="sidebar__link"
        :to="item.to"
        @click="$emit('close')"
      >
        <component :is="item.icon" :size="17" />
        <span>{{ item.label }}</span>
      </RouterLink>

      <div class="sidebar__group">
        <RouterLink
          class="sidebar__link"
          :class="{ 'sidebar__link--active': isWorkspaceMarketProductsRoute }"
          :to="{ path: workspaceMarketProductsPath }"
          @click="$emit('close')"
        >
          <BookmarkCheck :size="17" />
          <span>Наблюдаемые товары</span>
        </RouterLink>

        <div class="sidebar__subnav" aria-label="Разделы наблюдаемых товаров">
          <RouterLink
            class="sidebar__sublink"
            :class="{ 'sidebar__sublink--active': isWorkspaceMarketProductCreateRoute }"
            :to="{ path: workspaceMarketProductCreatePath }"
            @click="$emit('close')"
          >
            Создать карточку
          </RouterLink>
          <RouterLink
            class="sidebar__sublink"
            :class="{ 'sidebar__sublink--active': isWorkspaceMarketProductClustersRoute }"
            :to="{ path: workspaceMarketProductClustersPath }"
            @click="$emit('close')"
          >
            Кластерный анализ
          </RouterLink>
        </div>
      </div>

      <div class="sidebar__group">
        <RouterLink
          class="sidebar__link"
          :class="{ 'sidebar__link--active': isMarketIntelligenceRoute }"
          :to="{ path: marketTopForecastPath }"
          @click="$emit('close')"
        >
          <Radar :size="17" />
          <span>Маркетинговая разведка</span>
        </RouterLink>

        <div class="sidebar__subnav" aria-label="Разделы маркетинговой разведки">
          <RouterLink
            class="sidebar__sublink"
            :class="{ 'sidebar__sublink--active': activeMarketIntelligenceSection === 'top-forecast' }"
            :to="{ path: marketTopForecastPath }"
            @click="$emit('close')"
          >
            Прогноз топа
          </RouterLink>
          <RouterLink
            class="sidebar__sublink"
            :class="{ 'sidebar__sublink--active': activeMarketIntelligenceSection === 'rule-constructor' }"
            :to="{ path: ruleConstructorPath }"
            @click="$emit('close')"
          >
            Конструктор правил
          </RouterLink>
          <RouterLink
            class="sidebar__sublink"
            :class="{ 'sidebar__sublink--active': activeMarketIntelligenceSection === 'market-products' }"
            :to="{ path: marketProductsPath }"
            @click="$emit('close')"
          >
            Аналитика рынка
          </RouterLink>
          <RouterLink
            class="sidebar__sublink"
            :class="{ 'sidebar__sublink--active': activeMarketIntelligenceSection === 'price-quality' }"
            :to="{ path: marketIntelligencePath, hash: marketIntelligencePriceQualityHash }"
            @click="$emit('close')"
          >
            Карта цены и качества
          </RouterLink>
          <RouterLink
            class="sidebar__sublink"
            :class="{ 'sidebar__sublink--active': activeMarketIntelligenceSection === 'market-concentration' }"
            :to="{ path: marketIntelligenceConcentrationPath }"
            @click="$emit('close')"
          >
            Концентрация рынка
          </RouterLink>
        </div>
      </div>

      <div class="sidebar__group">
        <RouterLink
          class="sidebar__link"
          :class="{ 'sidebar__link--active': isOrdersRoute }"
          :to="{ path: ordersPath }"
          @click="$emit('close')"
        >
          <Boxes :size="17" />
          <span>Логистика и спрос</span>
        </RouterLink>

        <div class="sidebar__subnav" aria-label="Разделы логистики и спроса">
          <RouterLink
            v-for="section in ordersSections"
            :key="section.key"
            class="sidebar__sublink"
            :class="{ 'sidebar__sublink--active': activeOrdersSection === section.key }"
            :to="ordersSectionTo(section.key)"
            @click="$emit('close')"
          >
            {{ section.label }}
          </RouterLink>
        </div>
      </div>

      <div class="sidebar__group">
        <RouterLink
          class="sidebar__link"
          :class="{ 'sidebar__link--active': isManagementRoute }"
          :to="{ path: expensesPath }"
          @click="$emit('close')"
        >
          <Settings :size="17" />
          <span>Управление</span>
        </RouterLink>

        <div class="sidebar__subnav" aria-label="Разделы управления">
          <RouterLink
            class="sidebar__sublink"
            :class="{ 'sidebar__sublink--active': isExpensesRoute }"
            :to="{ path: expensesPath }"
            @click="$emit('close')"
          >
            Расходы
          </RouterLink>
          <RouterLink
            class="sidebar__sublink"
            :class="{ 'sidebar__sublink--active': isManagementWorkspacesRoute }"
            :to="{ path: managementWorkspacesPath }"
            @click="$emit('close')"
          >
            Рабочие области
          </RouterLink>
        </div>
      </div>

      <div v-if="isAdmin" class="sidebar__group">
        <RouterLink
          class="sidebar__link"
          :class="{ 'sidebar__link--active': isAdminRoute }"
          :to="{ path: adminParserPath }"
          @click="$emit('close')"
        >
          <ShieldCheck :size="17" />
          <span>Администратор</span>
        </RouterLink>

        <div class="sidebar__subnav" aria-label="Разделы администратора">
          <RouterLink
            class="sidebar__sublink"
            :class="{ 'sidebar__sublink--active': isAdminParserRoute }"
            :to="{ path: adminParserPath }"
            @click="$emit('close')"
          >
            Парсинг
          </RouterLink>
          <RouterLink
            class="sidebar__sublink"
            :class="{ 'sidebar__sublink--active': isAdminCalculationsRoute }"
            :to="{ path: adminCalculationsPath }"
            @click="$emit('close')"
          >
            Расчеты
          </RouterLink>
          <RouterLink
            class="sidebar__sublink"
            :class="{ 'sidebar__sublink--active': isAdminCatalogsRoute }"
            :to="{ path: adminCatalogsPath }"
            @click="$emit('close')"
          >
            Справочники
          </RouterLink>
        </div>
      </div>
    </nav>
  </aside>
</template>

<style scoped>
.sidebar {
  position: fixed;
  inset: 0 auto 0 0;
  z-index: 40;
  display: flex;
  flex-direction: column;
  width: 16rem;
  transform: translateX(-100%);
  border-right: 1px solid var(--color-border);
  background: var(--background-sidebar);
  backdrop-filter: blur(18px);
  transition: transform 180ms ease;
}

.sidebar--open {
  transform: translateX(0);
}

.sidebar__backdrop {
  position: fixed;
  inset: 0;
  z-index: 30;
  background: var(--theme-backdrop);
  backdrop-filter: blur(3px);
}

.sidebar__brand {
  display: grid;
  grid-template-columns: 2rem 1fr auto;
  align-items: center;
  gap: var(--space-3);
  padding: var(--space-4);
  border-bottom: 1px solid var(--color-border);
}

.sidebar__brand strong,
.sidebar__brand span {
  display: block;
}

.sidebar__brand span {
  color: var(--color-text-muted);
  font-size: 0.75rem;
  font-weight: 600;
}

.sidebar__mark {
  display: grid;
  height: 2rem;
  width: 2rem;
  place-items: center;
  filter: var(--shadow-brand-mark);
}

.sidebar__mark svg {
  width: 1.45rem;
  height: 2rem;
  overflow: visible;
}

.sidebar__flame-outer {
  fill: var(--flame-outer-fill);
  stroke: var(--flame-outer-stroke);
  stroke-width: 2.5;
}

.sidebar__flame-middle {
  fill: var(--flame-middle-fill);
}

.sidebar__flame-inner {
  fill: var(--flame-inner-fill);
}

.sidebar__flame-core {
  fill: var(--flame-core-fill);
}

.sidebar__nav {
  display: grid;
  gap: var(--space-1);
  flex: 1;
  align-content: start;
  overflow-y: auto;
  padding: var(--space-3);
}

.sidebar__link {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  min-height: 2.25rem;
  border-radius: var(--radius-md);
  padding: 0 var(--space-3);
  color: var(--color-text-muted);
  font-size: 0.875rem;
  font-weight: 650;
  transition: background-color 140ms ease, color 140ms ease, box-shadow 140ms ease;
}

.sidebar__link:hover,
.sidebar__link.router-link-active,
.sidebar__link--active {
  background: var(--surface-active-overlay);
  color: var(--color-text);
}

.sidebar__link.router-link-active,
.sidebar__link--active {
  box-shadow: inset 2px 0 0 var(--color-ember);
  color: var(--accent-ember-text-strong);
}

.sidebar__group {
  display: grid;
  gap: 0.2rem;
}

.sidebar__subnav {
  display: grid;
  gap: 0.12rem;
  margin: -0.05rem 0 0.25rem 1.5rem;
  padding-left: var(--space-3);
  border-left: 1px solid var(--accent-ember-sidebar-border);
}

.sidebar__sublink {
  min-height: 1.8rem;
  border-radius: var(--radius-sm);
  padding: 0 var(--space-3);
  color: var(--color-text-muted);
  font-size: 0.78rem;
  font-weight: 700;
  line-height: 1.8rem;
  transition: background-color 140ms ease, color 140ms ease, box-shadow 140ms ease;
}

.sidebar__sublink:hover,
.sidebar__sublink--active {
  background: var(--accent-ember-hover-bg);
  color: var(--accent-ember-text-strong);
}

.sidebar__sublink--active {
  box-shadow: inset 2px 0 0 var(--color-ember);
}

@media (min-width: 1024px) {
  .sidebar {
    position: sticky;
    top: 0;
    height: 100vh;
    transform: none;
  }

  .sidebar__close,
  .sidebar__backdrop {
    display: none;
  }
}
</style>
