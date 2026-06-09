import { computed, ref } from 'vue';
import { defineStore } from 'pinia';

export type AppTheme = 'obsidian' | 'ash';

const STORAGE_KEY = 'ashmes.theme';
const DEFAULT_THEME: AppTheme = 'obsidian';
const THEMES = new Set<AppTheme>(['obsidian', 'ash']);

export const useThemeStore = defineStore('theme', () => {
  const theme = ref<AppTheme>(readStoredTheme());
  const isLight = computed(() => theme.value === 'ash');
  const nextThemeLabel = computed(() => isLight.value ? 'Темная тема' : 'Светлая тема');

  applyTheme(theme.value);

  function toggleTheme(): void {
    setTheme(isLight.value ? 'obsidian' : 'ash');
  }

  function setTheme(value: AppTheme): void {
    theme.value = value;
    applyTheme(value);
    localStorage.setItem(STORAGE_KEY, value);
  }

  return {
    isLight,
    nextThemeLabel,
    setTheme,
    theme,
    toggleTheme
  };
});

export function applyStoredTheme(): AppTheme {
  const theme = readStoredTheme();
  applyTheme(theme);
  return theme;
}

function applyTheme(theme: AppTheme): void {
  if (typeof document === 'undefined') {
    return;
  }

  document.documentElement.dataset.theme = theme;
}

function readStoredTheme(): AppTheme {
  if (typeof localStorage === 'undefined') {
    return DEFAULT_THEME;
  }

  const stored = localStorage.getItem(STORAGE_KEY);
  return isAppTheme(stored) ? stored : DEFAULT_THEME;
}

function isAppTheme(value: unknown): value is AppTheme {
  return typeof value === 'string' && THEMES.has(value as AppTheme);
}
