import { defineStore } from 'pinia';
import { ref } from 'vue';

export type AuthPromptMode = 'login' | 'access';

export const useAuthPromptStore = defineStore('authPrompt', () => {
  const open = ref(false);
  const mode = ref<AuthPromptMode>('login');
  const message = ref('');

  function showLogin(text = ''): void {
    mode.value = 'login';
    message.value = text;
    open.value = true;
  }

  function showAccess(text = ''): void {
    mode.value = 'access';
    message.value = text;
    open.value = true;
  }

  function close(): void {
    open.value = false;
    message.value = '';
  }

  return {
    open,
    mode,
    message,
    showLogin,
    showAccess,
    close
  };
});
