import { createRouter, createWebHistory } from 'vue-router';

import AppLayout from '@/layouts/AppLayout.vue';
import AuthLayout from '@/layouts/AuthLayout.vue';
import { useAuthStore } from '@/features/auth/auth.store';
import LoginPage from '@/features/auth/LoginPage.vue';
import MarketIntelligencePage from '@/features/market-intelligence/MarketIntelligencePage.vue';
import MarketOpportunitiesPage from '@/features/market-opportunities/MarketOpportunitiesPage.vue';
import ParserProductsPage from '@/features/parser-products/ParserProductsPage.vue';
import ParserReviewsPage from '@/features/parser-reviews/ParserReviewsPage.vue';
import ProductsPage from '@/features/products/ProductsPage.vue';
import WorkspaceMarketProductClustersPage from '@/features/workspace-market-products/WorkspaceMarketProductClustersPage.vue';
import WorkspaceMarketProductsPage from '@/features/workspace-market-products/WorkspaceMarketProductsPage.vue';
import AccessSettingsPage from '@/pages/AccessSettingsPage.vue';
import ExpensesPage from '@/pages/ExpensesPage.vue';
import LogisticsPage from '@/pages/LogisticsPage.vue';
import OrdersPage from '@/pages/OrdersPage.vue';
import OverviewPage from '@/pages/OverviewPage.vue';

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/login',
      component: AuthLayout,
      children: [
        {
          path: '',
          name: 'login',
          component: LoginPage,
          meta: { public: true }
        }
      ]
    },
    {
      path: '/',
      component: AppLayout,
      meta: { requiresAuth: true },
      children: [
        { path: '', redirect: '/overview' },
        { path: 'overview', name: 'overview', component: OverviewPage },
        { path: 'workspace/market-products/clusters', name: 'workspace-market-product-clusters', component: WorkspaceMarketProductClustersPage },
        { path: 'workspace/market-products', name: 'workspace-market-products', component: WorkspaceMarketProductsPage },
        { path: 'market/products', name: 'market-products', component: ParserProductsPage },
        { path: 'market/opportunities', name: 'market-opportunities', component: MarketOpportunitiesPage },
        { path: 'market/intelligence', name: 'market-intelligence', component: MarketIntelligencePage },
        { path: 'products', name: 'products', component: ProductsPage },
        { path: 'parser/products', redirect: { name: 'market-products' } },
        { path: 'orders', name: 'orders', component: OrdersPage },
        { path: 'parser/reviews', name: 'parser-reviews', component: ParserReviewsPage },
        { path: 'logistics', name: 'logistics', component: LogisticsPage },
        { path: 'expenses', name: 'expenses', component: ExpensesPage },
        { path: 'settings/access', name: 'settings-access', component: AccessSettingsPage }
      ]
    },
    {
      path: '/:pathMatch(.*)*',
      redirect: '/overview'
    }
  ]
});

router.beforeEach(async (to) => {
  const auth = useAuthStore();

  auth.hydrate();

  if (to.meta.public) {
    return auth.isAuthenticated ? { name: 'overview' } : true;
  }

  await auth.bootstrap();

  if (!auth.isAuthenticated) {
    return {
      name: 'login',
      query: { redirect: to.fullPath }
    };
  }

  return true;
});
