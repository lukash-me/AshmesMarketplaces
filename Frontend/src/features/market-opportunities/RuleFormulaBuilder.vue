<script setup lang="ts">
import { computed } from 'vue';
import { X } from 'lucide-vue-next';

import type { RuleConstructorFilter, RuleGroupOperator } from '@/features/rule-constructor/ruleConstructor.types';
import type { RuleFormulaToken } from './ruleFormulaCompiler';

const props = defineProps<{
  tokens: RuleFormulaToken[];
  filtersById: Record<string, RuleConstructorFilter>;
  total: number;
  invalidReason: string | null;
  pendingRule: RuleConstructorFilter | null;
  bracketDraftBoundary: number | null;
}>();

const emit = defineEmits<{
  (event: 'set-operator', tokenId: string, operator: RuleGroupOperator): void;
  (event: 'remove-rule', tokenId: string): void;
  (event: 'remove-bracket', pairId: string): void;
  (event: 'bracket-boundary', boundary: number): void;
  (event: 'cancel-bracket-draft'): void;
  (event: 'commit-pending', operator: RuleGroupOperator): void;
  (event: 'cancel-pending'): void;
  (event: 'drop-rule', ruleId: string): void;
}>();

const hasTokens = computed(() => props.tokens.length > 0);
const ruleTokenCount = computed(() => props.tokens.filter((token) => token.kind === 'rule').length);
const canUseBrackets = computed(() => ruleTokenCount.value >= 2);

function ruleName(ruleId: string): string {
  return props.filtersById[ruleId]?.name ?? ruleId;
}

function ruleGroup(ruleId: string): string {
  return props.filtersById[ruleId]?.group ?? '';
}

function boundaryLabel(boundary: number): string {
  if (props.bracketDraftBoundary === null) {
    return '(';
  }

  return boundary > props.bracketDraftBoundary ? ')' : '(';
}

function boundaryTitle(boundary: number): string {
  if (props.bracketDraftBoundary === null) {
    return 'Поставить открывающую скобку';
  }

  return boundary > props.bracketDraftBoundary
    ? 'Поставить закрывающую скобку'
    : 'Выбрать новое место открывающей скобки';
}

function isClosingCandidate(boundary: number): boolean {
  return props.bracketDraftBoundary !== null && isValidClosingBoundary(boundary);
}

function shouldShowBoundary(boundary: number): boolean {
  if (!canUseBrackets.value) {
    return false;
  }

  if (props.bracketDraftBoundary !== null) {
    return isValidClosingBoundary(boundary);
  }

  return isValidOpeningBoundary(boundary);
}

function isValidOpeningBoundary(boundary: number): boolean {
  const tokenAtBoundary = props.tokens[boundary];
  if (!tokenAtBoundary || tokenAtBoundary.kind === 'operator' || isClosingParen(tokenAtBoundary)) {
    return false;
  }

  const previousToken = props.tokens[boundary - 1];
  if (previousToken && (previousToken.kind === 'rule' || isClosingParen(previousToken))) {
    return false;
  }

  return countRulesBetween(boundary, props.tokens.length) >= 2;
}

function isValidClosingBoundary(boundary: number): boolean {
  if (props.bracketDraftBoundary === null || boundary <= props.bracketDraftBoundary) {
    return false;
  }

  const previousToken = props.tokens[boundary - 1];
  if (!previousToken || previousToken.kind === 'operator' || isOpeningParen(previousToken)) {
    return false;
  }

  const tokenAtBoundary = props.tokens[boundary];
  if (tokenAtBoundary && (tokenAtBoundary.kind === 'rule' || isOpeningParen(tokenAtBoundary))) {
    return false;
  }

  return countRulesBetween(props.bracketDraftBoundary, boundary) >= 2;
}

function countRulesBetween(startBoundary: number, endBoundary: number): number {
  return props.tokens
    .slice(startBoundary, endBoundary)
    .filter((token) => token.kind === 'rule')
    .length;
}

function isOpeningParen(token: RuleFormulaToken): boolean {
  return token.kind === 'paren' && token.side === 'open';
}

function isClosingParen(token: RuleFormulaToken): boolean {
  return token.kind === 'paren' && token.side === 'close';
}

function onDrop(event: DragEvent) {
  const ruleId = event.dataTransfer?.getData('text/plain') || event.dataTransfer?.getData('application/x-rule-id');
  if (ruleId) {
    emit('drop-rule', ruleId);
  }
}
</script>

<template>
  <section class="formula-builder" @dragover.prevent @drop.stop.prevent="onDrop">
    <header class="formula-builder__head">
      <div>
        <h2>Конструктор · {{ total.toLocaleString('ru-RU') }} карточек</h2>
        <p v-if="invalidReason">{{ invalidReason }}</p>
        <p v-else-if="!hasTokens">Кликните по правилу сверху, чтобы добавить его в выражение.</p>
        <p v-else-if="pendingRule">Выберите И или ИЛИ между правилами.</p>
        <p v-else-if="bracketDraftBoundary !== null">Выберите допустимое место закрывающей скобки или отмените выбор.</p>
        <p v-else>Кликните по следующему правилу сверху. Скобки доступны, когда в формуле есть минимум два правила.</p>
      </div>
      <button
        v-if="bracketDraftBoundary !== null"
        class="formula-builder__cancel-draft"
        type="button"
        @click="emit('cancel-bracket-draft')"
      >
        Отменить скобку
      </button>
    </header>

    <div
      class="formula-builder__canvas"
      :class="{ 'formula-builder__canvas--incomplete': Boolean(invalidReason || pendingRule) }"
    >
      <div v-if="!hasTokens && !pendingRule" class="formula-builder__empty">
        Кликните по правилу
      </div>

      <template v-for="(token, index) in tokens" :key="token.id">
        <button
          v-if="token.kind === 'paren'"
          class="formula-builder__paren"
          type="button"
          title="Убрать эту пару скобок"
          @click.stop="emit('remove-bracket', token.pairId)"
        >
          {{ token.side === 'open' ? '(' : ')' }}
        </button>

        <span v-else-if="token.kind === 'operator'" class="formula-builder__operator">
          <button
            class="formula-builder__operator-current"
            type="button"
            @click.stop="emit('set-operator', token.id, token.operator === 'and' ? 'or' : 'and')"
          >
            {{ token.operator === 'and' ? 'И' : 'ИЛИ' }}
          </button>
        </span>

        <span v-else class="formula-builder__rule-wrap">
          <button
            v-if="shouldShowBoundary(index)"
            class="formula-builder__bracket formula-builder__bracket--left"
            :class="{ 'formula-builder__bracket--candidate': isClosingCandidate(index) }"
            type="button"
            :title="boundaryTitle(index)"
            @click.stop="emit('bracket-boundary', index)"
          >
            {{ boundaryLabel(index) }}
          </button>
          <span class="formula-builder__rule">
            <span class="formula-builder__rule-text">
              <span class="formula-builder__rule-name">{{ ruleName(token.ruleId) }}</span>
              <span v-if="ruleGroup(token.ruleId)" class="formula-builder__rule-group">{{ ruleGroup(token.ruleId) }}</span>
            </span>
            <button type="button" title="Убрать правило" @click.stop="emit('remove-rule', token.id)">
              <X :size="14" />
            </button>
          </span>
          <button
            v-if="shouldShowBoundary(index + 1)"
            class="formula-builder__bracket formula-builder__bracket--right"
            :class="{ 'formula-builder__bracket--candidate': isClosingCandidate(index + 1) }"
            type="button"
            :title="boundaryTitle(index + 1)"
            @click.stop="emit('bracket-boundary', index + 1)"
          >
            {{ boundaryLabel(index + 1) }}
          </button>
        </span>
      </template>

      <span v-if="pendingRule" class="formula-builder__pending">
        <span class="formula-builder__pending-operator">
          <button type="button" @click.stop="emit('commit-pending', 'and')">И</button>
          <button type="button" @click.stop="emit('commit-pending', 'or')">ИЛИ</button>
        </span>
        <span class="formula-builder__pending-rule">
          <span class="formula-builder__rule-text">
            <span class="formula-builder__rule-name">{{ pendingRule.name }}</span>
            <span class="formula-builder__rule-group">{{ pendingRule.group }}</span>
          </span>
        </span>
        <span class="formula-builder__pending-actions">
          <button type="button" @click.stop="emit('cancel-pending')">Отмена</button>
        </span>
      </span>
      <span v-else-if="hasTokens" class="formula-builder__tail-hint">
        Выберите следующее правило сверху
      </span>
    </div>
  </section>
</template>

<style scoped>
.formula-builder {
  display: grid;
  gap: var(--space-3);
}

.formula-builder__head h2,
.formula-builder__head p {
  margin: 0;
}

.formula-builder__head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: var(--space-3);
}

.formula-builder__head h2 {
  font-size: 1rem;
}

.formula-builder__head p {
  margin-top: var(--space-1);
  color: var(--color-text-muted);
}

.formula-builder__cancel-draft {
  min-height: 2.1rem;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  background: var(--surface-card);
  color: var(--accent-ember-text-strong);
  padding: 0 var(--space-3);
  font-weight: 800;
  white-space: nowrap;
}

.formula-builder__cancel-draft:hover {
  border-color: var(--color-ember);
  background: var(--accent-ember-soft);
}

.formula-builder__canvas {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
  align-items: center;
  min-height: 6rem;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: color-mix(in srgb, var(--surface-card) 76%, transparent);
  padding: var(--space-3);
}

.formula-builder__canvas--incomplete {
  border-color: var(--color-ember);
  box-shadow: 0 0 0 1px var(--accent-ember-soft);
}

.formula-builder__empty {
  border: 1px dashed var(--color-border);
  border-radius: var(--radius-sm);
  color: var(--color-text-muted);
  padding: var(--space-2) var(--space-3);
  font-weight: 700;
}

.formula-builder__rule-wrap {
  position: relative;
  display: inline-flex;
  align-items: center;
  gap: 0;
}

.formula-builder__rule {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
  min-height: 3rem;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  background: var(--surface-card);
  padding: var(--space-1) var(--space-2);
  font-weight: 800;
}

.formula-builder__rule-text {
  display: inline-grid;
  gap: 0.1rem;
  align-content: center;
  min-width: 0;
  line-height: 1.15;
}

.formula-builder__rule-name {
  color: var(--color-text);
}

.formula-builder__rule-group {
  color: var(--accent-ember-text-strong);
  font-size: 0.72rem;
  font-weight: 800;
}

.formula-builder__rule button,
.formula-builder__bracket,
.formula-builder__paren,
.formula-builder__operator-current,
.formula-builder__operator-menu button,
.formula-builder__pending-actions button {
  border: 0;
  background: transparent;
  color: var(--color-text);
  font: inherit;
}

.formula-builder__rule button {
  display: inline-grid;
  place-items: center;
  color: var(--color-text-muted);
}

.formula-builder__bracket {
  width: 1.3rem;
  min-height: 2.25rem;
  border-radius: var(--radius-sm);
  color: transparent;
  font-weight: 900;
}

.formula-builder__rule-wrap:hover .formula-builder__bracket,
.formula-builder__bracket--candidate {
  color: var(--accent-ember-text-strong);
  background: var(--accent-ember-soft);
}

.formula-builder__bracket--candidate {
  outline: 1px solid var(--color-ember);
}

.formula-builder__paren {
  min-width: 1.4rem;
  min-height: 2.25rem;
  border-radius: var(--radius-sm);
  background: var(--accent-ember-soft);
  color: var(--accent-ember-text-strong);
  font-weight: 900;
}

.formula-builder__operator {
  position: relative;
  display: inline-flex;
  min-height: 2.25rem;
  align-items: center;
}

.formula-builder__operator-current {
  min-width: 2.75rem;
  min-height: 2.25rem;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  background: var(--surface-card);
  color: var(--accent-ember-text-strong);
  font-weight: 900;
}

.formula-builder__pending-actions button {
  min-height: 1.8rem;
  border-radius: var(--radius-sm);
  padding: 0 var(--space-2);
  font-weight: 850;
}

.formula-builder__operator-current:hover,
.formula-builder__pending-actions button:hover {
  background: var(--accent-ember-soft);
  color: var(--accent-ember-text-strong);
}

.formula-builder__pending {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
  min-height: 2.25rem;
  font-weight: 800;
}

.formula-builder__pending-operator {
  display: inline-grid;
  gap: 0;
  overflow: hidden;
  border: 1px solid var(--color-ember);
  border-radius: var(--radius-sm);
  background: var(--surface-card);
}

.formula-builder__pending-operator button {
  min-width: 3.4rem;
  min-height: 1.65rem;
  border: 0;
  background: transparent;
  color: var(--accent-ember-text-strong);
  font-weight: 900;
  line-height: 1;
}

.formula-builder__pending-operator button + button {
  border-top: 1px solid var(--accent-ember-soft);
}

.formula-builder__pending-operator button:hover,
.formula-builder__pending-operator button:focus-visible {
  background: var(--accent-ember-soft);
}

.formula-builder__pending-rule {
  display: inline-flex;
  align-items: center;
  min-height: 3rem;
  border: 1px dashed var(--color-ember);
  border-radius: var(--radius-sm);
  background: var(--accent-ember-soft);
  padding: var(--space-1) var(--space-2);
}

.formula-builder__pending-actions {
  display: inline-flex;
  gap: var(--space-1);
}

.formula-builder__tail-hint {
  display: inline-flex;
  align-items: center;
  min-height: 2.25rem;
  border: 1px dashed var(--color-border);
  border-radius: var(--radius-sm);
  color: var(--color-text-muted);
  padding: 0 var(--space-3);
  font-weight: 750;
}
</style>
