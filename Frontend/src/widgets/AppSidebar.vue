<script setup lang="ts">
import { computed, ref } from 'vue';
import { useRoute } from 'vue-router';
import {
  BarChart3,
  Boxes,
  ChevronUp,
  CreditCard,
  LayoutDashboard,
  Moon,
  Radar,
  Settings,
  Sun,
  Truck,
  X
} from 'lucide-vue-next';

import { useThemeStore } from '@/features/theme/theme.store';

defineProps<{
  open: boolean;
}>();

defineEmits<{
  close: [];
}>();

type MarketIntelligenceSection = 'events' | 'weaknesses' | 'prices' | 'stock' | 'repeats';
type OrdersSection = 'all' | 'assumed-orders' | 'new-products' | 'restocks';

const route = useRoute();
const theme = useThemeStore();
const themeMenuOpen = ref(false);
const marketIntelligencePath = '/market/intelligence';
const ordersPath = '/orders';
const marketIntelligenceSections: Array<{ key: MarketIntelligenceSection; label: string }> = [
  { key: 'events', label: 'События' },
  { key: 'weaknesses', label: 'Зоны для проверки' },
  { key: 'prices', label: 'Скидки и цены' },
  { key: 'stock', label: 'Остатки' },
  { key: 'repeats', label: 'Повторы' }
];
const marketIntelligenceSectionKeys = marketIntelligenceSections.map((section) => section.key);
const ordersSections: Array<{ key: OrdersSection; label: string }> = [
  { key: 'all', label: 'Все события' },
  { key: 'assumed-orders', label: 'Предполагаемые заказы' },
  { key: 'new-products', label: 'Новые товары' },
  { key: 'restocks', label: 'Пополнения товаров' }
];
const ordersSectionKeys = ordersSections.map((section) => section.key);

const primaryNavItems = [
  { to: '/overview', label: 'Обзор', icon: LayoutDashboard },
  { to: '/market/products', label: 'Аналитика рынка', icon: BarChart3 }
];

const secondaryNavItems = [
  { to: '/logistics', label: 'Логистика', icon: Truck },
  { to: '/expenses', label: 'Расходы', icon: CreditCard },
  { to: '/settings/access', label: 'Доступы', icon: Settings }
];

const isMarketIntelligenceRoute = computed(() => route.path === marketIntelligencePath);
const isOrdersRoute = computed(() => route.path === ordersPath);
const activeMarketIntelligenceSection = computed(() =>
  isMarketIntelligenceRoute.value ? normalizeMarketIntelligenceSection(route.query.section) : null
);
const activeOrdersSection = computed(() =>
  isOrdersRoute.value ? normalizeOrdersSection(route.query.tab) : null
);

function normalizeMarketIntelligenceSection(value: unknown): MarketIntelligenceSection {
  const raw = Array.isArray(value) ? value[0] : value;
  return typeof raw === 'string' && marketIntelligenceSectionKeys.includes(raw as MarketIntelligenceSection)
    ? raw as MarketIntelligenceSection
    : 'events';
}

function marketIntelligenceSectionTo(section: MarketIntelligenceSection) {
  return {
    path: marketIntelligencePath,
    query: isMarketIntelligenceRoute.value
      ? { ...route.query, section }
      : { section }
  };
}

function normalizeOrdersSection(value: unknown): OrdersSection {
  const rawValue = Array.isArray(value) ? value[0] : value;
  const raw = typeof rawValue === 'string' ? rawValue : 'all';

  if (raw === 'stock-changes') {
    return 'restocks';
  }

  return ordersSectionKeys.includes(raw as OrdersSection)
    ? raw as OrdersSection
    : 'all';
}

function ordersSectionTo(section: OrdersSection) {
  return {
    path: ordersPath,
    query: section === 'all' ? {} : { tab: section }
  };
}

function selectTheme(value: 'obsidian' | 'ash') {
  theme.setTheme(value);
  themeMenuOpen.value = false;
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
          :class="{ 'sidebar__link--active': isMarketIntelligenceRoute }"
          :to="{ path: marketIntelligencePath, query: { section: 'events' } }"
          @click="$emit('close')"
        >
          <Radar :size="17" />
          <span>Маркетинговая разведка</span>
        </RouterLink>

        <div class="sidebar__subnav" aria-label="Разделы маркетинговой разведки">
          <RouterLink
            v-for="section in marketIntelligenceSections"
            :key="section.key"
            class="sidebar__sublink"
            :class="{ 'sidebar__sublink--active': activeMarketIntelligenceSection === section.key }"
            :to="marketIntelligenceSectionTo(section.key)"
            @click="$emit('close')"
          >
            {{ section.label }}
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
          <span>Заказы</span>
        </RouterLink>

        <div class="sidebar__subnav" aria-label="Разделы заказов">
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

      <RouterLink
        v-for="item in secondaryNavItems"
        :key="item.to"
        class="sidebar__link"
        :to="item.to"
        @click="$emit('close')"
      >
        <component :is="item.icon" :size="17" />
        <span>{{ item.label }}</span>
      </RouterLink>
    </nav>

    <div class="sidebar__footer">
      <div class="sidebar__theme">
        <section v-if="themeMenuOpen" class="sidebar__theme-menu" role="menu" aria-label="Выбор темы">
          <button
            class="sidebar__theme-option"
            :class="{ 'sidebar__theme-option--active': theme.theme === 'ash' }"
            type="button"
            role="menuitemradio"
            :aria-checked="theme.theme === 'ash'"
            @click="selectTheme('ash')"
          >
            <Sun :size="16" />
            <span>Светлая</span>
          </button>
          <button
            class="sidebar__theme-option"
            :class="{ 'sidebar__theme-option--active': theme.theme === 'obsidian' }"
            type="button"
            role="menuitemradio"
            :aria-checked="theme.theme === 'obsidian'"
            @click="selectTheme('obsidian')"
          >
            <Moon :size="16" />
            <span>Темная</span>
          </button>
        </section>

        <button
          class="sidebar__theme-button"
          type="button"
          aria-haspopup="menu"
          :aria-expanded="themeMenuOpen"
          @click="themeMenuOpen = !themeMenuOpen"
        >
          <component :is="theme.isLight ? Sun : Moon" :size="17" />
          <span>Выбор темы</span>
          <ChevronUp class="sidebar__theme-chevron" :size="16" />
        </button>
      </div>
    </div>
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

.sidebar__footer {
  display: grid;
  gap: var(--space-2);
  padding: var(--space-3);
  border-top: 1px solid var(--color-border);
}

.sidebar__theme {
  position: relative;
}

.sidebar__theme-button {
  display: flex;
  align-items: center;
  justify-content: flex-start;
  gap: var(--space-3);
  width: 100%;
  min-height: 2.25rem;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--surface-control-raised);
  padding: 0 var(--space-3);
  color: var(--color-text-muted);
  font: inherit;
  font-size: 0.875rem;
  font-weight: 700;
  transition: background-color 140ms ease, border-color 140ms ease, color 140ms ease;
}

.sidebar__theme-button span {
  flex: 1;
  text-align: left;
}

.sidebar__theme-chevron {
  color: var(--color-text-subtle);
}

.sidebar__theme-button:hover {
  border-color: var(--accent-ember-border);
  background: var(--accent-ember-hover-bg);
  color: var(--accent-ember-text-strong);
}

.sidebar__theme-button:focus-visible {
  outline: none;
  box-shadow: var(--focus-ring);
}

.sidebar__theme-menu {
  position: absolute;
  right: 0;
  bottom: calc(100% + var(--space-2));
  left: 0;
  z-index: 45;
  display: grid;
  gap: var(--space-1);
  border: 1px solid var(--color-border-strong);
  border-radius: var(--radius-md);
  background: var(--surface-panel-raised);
  box-shadow: var(--shadow-panel);
  padding: var(--space-2);
}

.sidebar__theme-option {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  min-height: 2.15rem;
  width: 100%;
  border: 1px solid transparent;
  border-radius: var(--radius-sm);
  background: transparent;
  color: var(--color-text-muted);
  font: inherit;
  font-size: 0.84rem;
  font-weight: 720;
  padding: 0 var(--space-2);
  text-align: left;
  transition: background-color 140ms ease, border-color 140ms ease, color 140ms ease;
}

.sidebar__theme-option:hover,
.sidebar__theme-option--active {
  border-color: var(--color-border-strong);
  background: var(--surface-active-overlay);
  color: var(--color-text);
}

.sidebar__theme-option--active {
  box-shadow: inset 2px 0 0 var(--color-ember);
}

.sidebar__theme-option:focus-visible {
  outline: none;
  box-shadow: var(--focus-ring);
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
