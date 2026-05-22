<script setup lang="ts">
import {
  BarChart3,
  Boxes,
  CreditCard,
  LayoutDashboard,
  Megaphone,
  PackageSearch,
  Settings,
  Sparkles,
  Star,
  Truck,
  X
} from 'lucide-vue-next';

defineProps<{
  open: boolean;
}>();

defineEmits<{
  close: [];
}>();

const navItems = [
  { to: '/overview', label: 'Overview', icon: LayoutDashboard },
  { to: '/market/products', label: 'Аналитика рынка', icon: BarChart3 },
  { to: '/products', label: 'Мои товары', icon: PackageSearch },
  { to: '/orders', label: 'Orders', icon: Boxes },
  { to: '/reviews', label: 'Отзывы моих товаров', icon: Star },
  { to: '/logistics', label: 'Logistics', icon: Truck },
  { to: '/campaigns', label: 'Campaigns', icon: Megaphone },
  { to: '/expenses', label: 'Expenses', icon: CreditCard },
  { to: '/recommendations', label: 'Recommendations', icon: Sparkles },
  { to: '/settings/access', label: 'Access', icon: Settings }
];
</script>

<template>
  <div v-if="open" class="sidebar__backdrop" @click="$emit('close')" />
  <aside class="sidebar" :class="{ 'sidebar--open': open }">
    <div class="sidebar__brand">
      <div class="sidebar__mark">
        <BarChart3 :size="18" />
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
        v-for="item in navItems"
        :key="item.to"
        class="sidebar__link"
        :to="item.to"
        @click="$emit('close')"
      >
        <component :is="item.icon" :size="17" />
        <span>{{ item.label }}</span>
      </RouterLink>
    </nav>
  </aside>
</template>

<style scoped>
.sidebar {
  position: fixed;
  inset: 0 auto 0 0;
  z-index: 40;
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
  border-radius: var(--radius-md);
  border: 1px solid var(--accent-ember-border);
  background: var(--background-brand-mark);
  color: var(--accent-ember-text);
}

.sidebar__nav {
  display: grid;
  gap: var(--space-1);
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
.sidebar__link.router-link-active {
  background: var(--surface-active-overlay);
  color: var(--color-text);
}

.sidebar__link.router-link-active {
  box-shadow: inset 2px 0 0 var(--color-ember);
  color: var(--accent-ember-text-strong);
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
