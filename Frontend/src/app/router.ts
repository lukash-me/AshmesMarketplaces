import { createRouter, createWebHistory } from 'vue-router';

import AppLayout from '@/layouts/AppLayout.vue';
import AuthLayout from '@/layouts/AuthLayout.vue';
import { useAuthStore } from '@/features/auth/auth.store';
import LoginPage from '@/features/auth/LoginPage.vue';
import ParserProductsPage from '@/features/parser-products/ParserProductsPage.vue';
import ParserReviewsPage from '@/features/parser-reviews/ParserReviewsPage.vue';
import ProductsPage from '@/features/products/ProductsPage.vue';
import AccessSettingsPage from '@/pages/AccessSettingsPage.vue';
import CampaignsPage from '@/pages/CampaignsPage.vue';
import ExpensesPage from '@/pages/ExpensesPage.vue';
import LogisticsPage from '@/pages/LogisticsPage.vue';
import OrdersPage from '@/pages/OrdersPage.vue';
import OverviewPage from '@/pages/OverviewPage.vue';
import RecommendationsPage from '@/pages/RecommendationsPage.vue';
import ReviewsPage from '@/pages/ReviewsPage.vue';

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
        { path: 'market/products', name: 'market-products', component: ParserProductsPage },
        { path: 'products', name: 'products', component: ProductsPage },
        { path: 'parser/products', redirect: { name: 'market-products' } },
        { path: 'orders', name: 'orders', component: OrdersPage },
        { path: 'reviews', name: 'reviews', component: ReviewsPage },
        { path: 'parser/reviews', name: 'parser-reviews', component: ParserReviewsPage },
        { path: 'logistics', name: 'logistics', component: LogisticsPage },
        { path: 'campaigns', name: 'campaigns', component: CampaignsPage },
        { path: 'expenses', name: 'expenses', component: ExpensesPage },
        { path: 'recommendations', name: 'recommendations', component: RecommendationsPage },
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
