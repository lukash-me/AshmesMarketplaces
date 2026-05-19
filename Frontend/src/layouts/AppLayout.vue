<script setup lang="ts">
import { ref } from 'vue';

import AppSidebar from '@/widgets/AppSidebar.vue';
import AppTopbar from '@/widgets/AppTopbar.vue';

const sidebarOpen = ref(false);
</script>

<template>
  <div class="app-layout">
    <AppSidebar :open="sidebarOpen" @close="sidebarOpen = false" />
    <div class="app-layout__main">
      <AppTopbar @menu="sidebarOpen = true" />
      <main class="app-layout__content">
        <RouterView />
      </main>
    </div>
  </div>
</template>

<style scoped>
.app-layout {
  min-height: 100vh;
  background: var(--background-app-grid);
  background-size: 44px 44px;
}

.app-layout__main {
  min-width: 0;
}

.app-layout__content {
  width: 100%;
  max-width: 1520px;
  margin: 0 auto;
  padding: var(--space-4);
}

@media (min-width: 1024px) {
  .app-layout {
    display: grid;
    grid-template-columns: 16rem minmax(0, 1fr);
  }

  .app-layout__content {
    padding: var(--space-5);
  }
}
</style>
