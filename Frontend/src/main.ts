import { createPinia } from 'pinia';
import { createApp } from 'vue';

import App from './App.vue';
import { router } from './app/router';
import { applyStoredTheme } from './features/theme/theme.store';
import './styles/index.css';

applyStoredTheme();

createApp(App).use(createPinia()).use(router).mount('#app');
