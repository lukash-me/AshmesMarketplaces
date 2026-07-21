import type { RuleExpression, RuleGroupOperator } from '@/features/rule-constructor/ruleConstructor.types';

export type RuleFormulaRuleToken = {
  id: string;
  kind: 'rule';
  ruleId: string;
};

export type RuleFormulaOperatorToken = {
  id: string;
  kind: 'operator';
  operator: RuleGroupOperator;
};

export type RuleFormulaParenToken = {
  id: string;
  kind: 'paren';
  side: 'open' | 'close';
  pairId: string;
};

export type RuleFormulaToken =
  | RuleFormulaRuleToken
  | RuleFormulaOperatorToken
  | RuleFormulaParenToken;

export type RuleFormulaCompileResult = {
  valid: boolean;
  expression: RuleExpression | null;
  error: string | null;
};

type ParserState = {
  tokens: RuleFormulaToken[];
  index: number;
};

const EMPTY_EXPRESSION: RuleExpression = {
  kind: 'group',
  operator: 'and',
  children: []
};

export function compileRuleFormula(tokens: RuleFormulaToken[]): RuleFormulaCompileResult {
  if (tokens.length === 0) {
    return {
      valid: true,
      expression: EMPTY_EXPRESSION,
      error: null
    };
  }

  const state: ParserState = {
    tokens,
    index: 0
  };

  const expression = parseOr(state);
  if (!expression.valid) {
    return expression;
  }

  if (state.index < tokens.length) {
    const token = tokens[state.index];
    if (token.kind === 'operator') {
      return failure('После логической операции должно быть правило.');
    }

    if (token.kind === 'paren' && token.side === 'close') {
      return failure('Закрывающая скобка стоит без открывающей.');
    }

    return failure('Между правилами нужно выбрать И или ИЛИ.');
  }

  return expression;
}

export function collectRuleIds(tokens: RuleFormulaToken[]): string[] {
  return tokens
    .filter((token): token is RuleFormulaRuleToken => token.kind === 'rule')
    .map((token) => token.ruleId);
}

export function insertBracketPair(
  tokens: RuleFormulaToken[],
  startBoundary: number,
  endBoundary: number,
  pairId: string
): RuleFormulaToken[] {
  const next = [...tokens];
  next.splice(endBoundary, 0, {
    id: `${pairId}-close`,
    kind: 'paren',
    side: 'close',
    pairId
  });
  next.splice(startBoundary, 0, {
    id: `${pairId}-open`,
    kind: 'paren',
    side: 'open',
    pairId
  });
  return next;
}

export function removeBracketPair(tokens: RuleFormulaToken[], pairId: string): RuleFormulaToken[] {
  return tokens.filter((token) => token.kind !== 'paren' || token.pairId !== pairId);
}

function parseOr(state: ParserState): RuleFormulaCompileResult {
  const first = parseAnd(state);
  if (!first.valid || !first.expression) {
    return first;
  }

  const children: RuleExpression[] = [first.expression];

  while (isOperator(state, 'or')) {
    state.index += 1;
    const next = parseAnd(state);
    if (!next.valid || !next.expression) {
      return failure('После ИЛИ должно быть правило или группа в скобках.');
    }

    children.push(next.expression);
  }

  return success(joinExpressions('or', children));
}

function parseAnd(state: ParserState): RuleFormulaCompileResult {
  const first = parsePrimary(state);
  if (!first.valid || !first.expression) {
    return first;
  }

  const children: RuleExpression[] = [first.expression];

  while (isOperator(state, 'and')) {
    state.index += 1;
    const next = parsePrimary(state);
    if (!next.valid || !next.expression) {
      return failure('После И должно быть правило или группа в скобках.');
    }

    children.push(next.expression);
  }

  return success(joinExpressions('and', children));
}

function parsePrimary(state: ParserState): RuleFormulaCompileResult {
  const token = state.tokens[state.index];
  if (!token) {
    return failure('Выражение не завершено.');
  }

  if (token.kind === 'rule') {
    state.index += 1;
    return success({
      kind: 'rule',
      ruleId: token.ruleId
    });
  }

  if (token.kind === 'operator') {
    return failure('Перед логической операцией должно быть правило.');
  }

  if (token.side === 'close') {
    return failure('Закрывающая скобка стоит без открывающей.');
  }

  state.index += 1;
  const nested = parseOr(state);
  if (!nested.valid || !nested.expression) {
    return nested;
  }

  const closing = state.tokens[state.index];
  if (!closing || closing.kind !== 'paren' || closing.side !== 'close') {
    return failure('Закройте скобку.');
  }

  if (closing.pairId !== token.pairId) {
    return failure('Скобки пересекаются. Закройте текущую скобку перед новой.');
  }

  state.index += 1;
  return nested;
}

function isOperator(state: ParserState, operator: RuleGroupOperator): boolean {
  const token = state.tokens[state.index];
  return token?.kind === 'operator' && token.operator === operator;
}

function joinExpressions(operator: RuleGroupOperator, children: RuleExpression[]): RuleExpression {
  if (children.length === 1) {
    return children[0];
  }

  return {
    kind: 'group',
    operator,
    children
  };
}

function success(expression: RuleExpression): RuleFormulaCompileResult {
  return {
    valid: true,
    expression,
    error: null
  };
}

function failure(error: string): RuleFormulaCompileResult {
  return {
    valid: false,
    expression: null,
    error
  };
}
