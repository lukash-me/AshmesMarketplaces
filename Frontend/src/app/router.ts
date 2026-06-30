import { createRouter, createWebHistory } from 'vue-router';

import AppLayout from '@/layouts/AppLayout.vue';
import { useAuthStore } from '@/features/auth/auth.store';
import MarketConcentrationPage from '@/features/market-intelligence/MarketConcentrationPage.vue';
import MarketIntelligencePage from '@/features/market-intelligence/MarketIntelligencePage.vue';
import MarketTopForecastPage from '@/features/market-intelligence/MarketTopForecastPage.vue';
import MarketOpportunitiesPage from '@/features/market-opportunities/MarketOpportunitiesPage.vue';
import ParserAdminMonitoringPage from '@/features/parser-admin-monitoring/ParserAdminMonitoringPage.vue';
import ParserTestingPage from '@/features/parser-testing/ParserTestingPage.vue';
import ParserProductsPage from '@/features/parser-products/ParserProductsPage.vue';
import ParserReviewsPage from '@/features/parser-reviews/ParserReviewsPage.vue';
import ProductsPage from '@/features/products/ProductsPage.vue';
import WorkspaceMarketProductCreatePage from '@/features/workspace-market-products/WorkspaceMarketProductCreatePage.vue';
import WorkspaceMarketProductClustersPage from '@/features/workspace-market-products/WorkspaceMarketProductClustersPage.vue';
import WorkspaceMarketProductsPage from '@/features/workspace-market-products/WorkspaceMarketProductsPage.vue';
import ExpensesPage from '@/pages/ExpensesPage.vue';
import OrdersAvailabilityPage from '@/pages/OrdersAvailabilityPage.vue';
import OrdersPage from '@/pages/OrdersPage.vue';
import OverviewPage from '@/pages/OverviewPage.vue';
import WorkspaceManagementPage from '@/pages/WorkspaceManagementPage.vue';

export const router = createRouter({
  history: createWebHistory(),
  scrollBehavior(to) {
    if (to.hash) {
      return {
        el: to.hash,
        top: 16,
        behavior: 'smooth'
      };
    }

    return { top: 0 };
  },
  routes: [
    {
      path: '/login',
      name: 'login',
      redirect: (to) => ({
        path: typeof to.query.redirect === 'string' ? to.query.redirect : '/overview',
        query: { auth: 'login' }
      })
    },
    {
      path: '/',
      component: AppLayout,
      children: [
        { path: '', redirect: '/overview' },
        { path: 'overview', name: 'overview', component: OverviewPage },
        { path: 'workspace/market-products/create', name: 'workspace-market-product-create', component: WorkspaceMarketProductCreatePage },
        { path: 'workspace/market-products/clusters', name: 'workspace-market-product-clusters', component: WorkspaceMarketProductClustersPage },
        { path: 'workspace/market-products', name: 'workspace-market-products', component: WorkspaceMarketProductsPage },
        { path: 'market/products', name: 'market-products', component: ParserProductsPage },
        { path: 'market/opportunities', name: 'market-opportunities', component: MarketOpportunitiesPage },
        { path: 'market/intelligence/top-forecast', name: 'market-intelligence-top-forecast', component: MarketTopForecastPage },
        { path: 'market/intelligence/concentration', name: 'market-intelligence-concentration', component: MarketConcentrationPage },
        { path: 'market/intelligence', name: 'market-intelligence', component: MarketIntelligencePage },
        { path: 'testing', name: 'parser-testing', component: ParserTestingPage },
        { path: 'products', name: 'products', component: ProductsPage },
        { path: 'parser/products', redirect: { name: 'market-products' } },
        { path: 'orders', name: 'orders', component: OrdersPage },
        { path: 'orders/availability', name: 'orders-availability', component: OrdersAvailabilityPage },
        { path: 'parser/reviews', name: 'parser-reviews', component: ParserReviewsPage },
        { path: 'expenses', name: 'expenses', component: ExpensesPage },
        { path: 'management/workspaces', name: 'management-workspaces', component: WorkspaceManagementPage },
        { path: 'admin/parser', name: 'admin-parser-monitoring', component: ParserAdminMonitoringPage },
        { path: 'settings/access', redirect: { name: 'management-workspaces' } }
      ]
    },
    {
      path: '/:pathMatch(.*)*',
      redirect: '/overview'
    }
  ]
});

router.beforeEach(async (to) => {
  if (to.path === '/market/intelligence' && to.hash === '#market-concentration') {
    return {
      path: '/market/intelligence/concentration',
      query: to.query
    };
  }

  const auth = useAuthStore();

  auth.hydrate();

  await auth.bootstrap();

  return true;
});
