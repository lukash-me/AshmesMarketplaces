import { computed, ref } from 'vue';
import { defineStore } from 'pinia';

export type AppTheme = 'obsidian' | 'ash';

const STORAGE_KEY = 'ashmes.theme';
const DEFAULT_THEME: AppTheme = 'ash';

export const useThemeStore = defineStore('theme', () => {
  const theme = ref<AppTheme>(DEFAULT_THEME);
  const isLight = computed(() => theme.value === 'ash');
  const nextThemeLabel = computed(() => isLight.value ? 'Темная тема' : 'Светлая тема');

  applyTheme(DEFAULT_THEME);

  function toggleTheme(): void {
    setTheme(DEFAULT_THEME);
  }

  function setTheme(_value: AppTheme = DEFAULT_THEME): void {
    theme.value = DEFAULT_THEME;
    applyTheme(DEFAULT_THEME);
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

function applyTheme(_theme: AppTheme = DEFAULT_THEME): void {
  if (typeof document === 'undefined') {
    return;
  }

  document.documentElement.dataset.theme = DEFAULT_THEME;

  if (typeof localStorage !== 'undefined') {
    localStorage.setItem(STORAGE_KEY, DEFAULT_THEME);
  }
}

function readStoredTheme(): AppTheme {
  return DEFAULT_THEME;
}
