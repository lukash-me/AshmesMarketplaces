<script setup lang="ts">
import ParserTestingPage from '@/features/parser-testing/ParserTestingPage.vue';
</script>

<template>
  <ParserTestingPage
    title="Доступность товара"
    description="Показываем сроки доставки и остатки по контрольным городам для актуальной выгрузки."
    empty-title="Товары с профилем доставки не найдены"
    empty-description="В актуальной выгрузке пока нет карточек с рассчитанным профилем доставки."
    error-title="Не удалось загрузить доступность товаров"
    error-message="Не удалось загрузить доступность товаров."
    :test-runs-only="false"
    :show-analysis-actions="false"
  />
</template>
