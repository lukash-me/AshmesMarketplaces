import { createPinia } from 'pinia';
import { createApp } from 'vue';

import App from './App.vue';
import { router } from './app/router';
import './styles/index.css';

createApp(App).use(createPinia()).use(router).mount('#app');
