<template>
  <section class="card">
    <slot />
  </section>
</template>

<style scoped>
.card {
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--color-surface);
  box-shadow: var(--shadow-subtle);
}
</style>
