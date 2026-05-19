<template>
  <main class="auth-layout">
    <RouterView />
  </main>
</template>

<style scoped>
.auth-layout {
  min-height: 100vh;
  display: grid;
  place-items: center;
  padding: var(--space-6);
  background:
    linear-gradient(180deg, rgb(255 255 255 / 0.7), rgb(246 247 249 / 0.92)),
    radial-gradient(circle at 50% 0%, #e6efff 0, transparent 38rem);
}
</style>
