import type { ParserAdminProxyRun, ParserAdminProxyRunJournal } from './parserAdminMonitoring.types';

export const parserAdminTexts = {
  pageTitle: 'Parser мониторинг',
  tabsLabel: 'Разделы мониторинга parser-ов',
  tabs: {
    instances: 'Парсеры',
    journal: 'Журнал',
    proxies: 'Прокси'
  },
  actions: {
    addInstance: 'Добавить инстанс',
    addProxy: 'Добавить прокси',
    refresh: 'Обновить',
    edit: 'Редактировать',
    disable: 'Отключить',
    deleteProxy: 'Удалить прокси',
    cancel: 'Отмена',
    cancelLaunch: 'Отменить запуск',
    save: 'Сохранить',
    start: 'Запустить',
    launchLimited: 'Запуск с лимитом',
    launchFull: 'Полная выгрузка',
    launchProxy: 'Проверить proxy',
    management: 'Управление',
    launch: 'Запуск',
    rollback: 'Откатить',
    details: 'Подробности',
    confirmRollback: 'Выполнить откат'
  },
  metrics: {
    runningProxies: 'Работающих прокси',
    progress: 'Прогресс',
    completed: 'Выполнено',
    runtime: 'Время работы',
    status: 'Статус',
    ranges: 'Диапазоны',
    lastRun: 'Последний запуск',
    lastActivity: 'Последняя активность',
    productsPerSecond: 'Карточек/сек',
    rangesPerSecond: 'Диапазонов/сек'
  },
  table: {
    proxy: 'Прокси',
    key: 'Ключ',
    start: 'Начало',
    finish: 'Завершение',
    instance: 'Инстанс',
    downloaded: 'Выгружено',
    created: 'Новых',
    updated: 'Обновлено',
    planned: 'Запланировано',
    time: 'Время',
    error: 'Ошибка',
    password: 'Пароль',
    niche: 'Ниша',
    login: 'Логин',
    status: 'Статус',
    actions: 'Действия'
  },
  form: {
    addProxyTitle: 'Добавить прокси',
    editProxyTitle: 'Редактировать прокси',
    addInstanceTitle: 'Добавить инстанс',
    editInstanceTitle: 'Редактировать инстанс',
    instanceName: 'Название инстанса',
    assignedProxies: 'Прокси инстанса',
    alreadyAssigned: 'Уже назначен',
    unassigned: 'Не назначен',
    login: 'Логин',
    password: 'Пароль',
    passwordPlaceholder: 'Оставьте пустым, чтобы сохранить текущий',
    niche: 'Ниша',
    nichePlaceholder: 'Найти leaf-нишу WB',
    noNiche: 'Без ниши'
  },
  launch: {
    limitedTitle: 'Запуск с лимитом',
    fullTitle: 'Полная выгрузка',
    proxyTitle: 'Проверить proxy',
    batchLimit: 'Количество batch-ей',
    proxy: 'Proxy',
    queued: 'Запуск поставлен в очередь',
    cancelConfirm: 'Отменить запуск до создания proxy-run?',
    cancelled: 'Запуск отменен.',
    fullConfirmation: 'Все назначенные proxy начнут выгружать все карточки своих ниш. Запуск может занять много времени.',
    alreadyRunning: 'Текущий запуск уже выполняется или ожидает выполнения.'
  },
  proxyDelete: {
    title: 'Удалить прокси',
    messagePrefix: 'Прокси',
    messageSuffix: 'будет полностью удален из сервиса и больше не будет использоваться в новых запусках.',
    note: 'Исторические записи журнала сохранятся, потому что они привязаны к proxy key, а не к записи прокси.'
  },
  rollback: {
    title: 'Откат запуска parser-а',
    previewLoading: 'Проверка возможности отката...',
    success: 'Откат выполнен.',
    created: 'Создано новых карточек',
    updated: 'Изменено существующих карточек',
    conflicts: 'Конфликтов',
    alreadyRolledBack: 'Уже откатано',
    deleted: 'Удалено карточек',
    restored: 'Восстановлено карточек'
  },
  details: {
    title: 'Подробности запуска parser-а',
    ip: 'IP',
    created: 'Новых карточек',
    updated: 'Обновлено'
  },
  empty: {
    proxyRuns: 'Proxy-процессы еще не запускались.',
    instances: 'Parser-инстансы еще не добавлены.',
    journal: 'Завершенных или упавших proxy-процессов пока нет.',
    proxies: 'Прокси еще не добавлены.',
    instanceProxies: 'Нет доступных proxy для назначения.'
  },
  loading: 'Загрузка мониторинга...',
  fallbackError: 'Не удалось загрузить мониторинг parser-ов.',
  validation: {
    instanceName: 'Укажите название инстанса.',
    ip: 'Укажите IP прокси.',
    httpPort: 'Укажите корректный HTTP port.',
    socksPort: 'Укажите корректный SOCKS5 port.',
    login: 'Укажите логин.',
    password: 'Укажите пароль.',
    niche: 'Выберите нишу из списка или вариант "Без ниши".',
    batchLimit: 'Укажите положительное количество batch-ей.',
    proxy: 'Выберите proxy для проверки.'
  },
  values: {
    dash: '-',
    passwordSet: 'Задан',
    passwordMissing: 'Не задан',
    active: 'Активен',
    disabled: 'Отключен',
    priceBounds: 'Границы цен',
    rangeChecks: 'Проверено',
    finalRanges: 'Сформировано',
    ipUnavailable: 'IP не определен'
  }
} as const;

export function parserStatusLabel(
  item: Pick<ParserAdminProxyRun | ParserAdminProxyRunJournal, 'status' | 'phase'>
): string {
  if (item.phase === 'launch_failed') {
    return 'Ошибка запуска';
  }
  if (item.phase === 'launch_cancelled') {
    return 'Запуск отменен';
  }
  if (item.status === 'running' && item.phase === 'launch') {
    return 'Запуск';
  }
  if (item.status === 'running' && item.phase === 'wb_preflight') {
    return 'Проверка WB';
  }
  if (item.status === 'running' && item.phase === 'ranges') {
    return 'Диапазоны';
  }
  if (item.status === 'running' && item.phase === 'download') {
    return 'Выгрузка';
  }

  const labels: Record<string, string> = {
    queued: 'В очереди',
    configured: 'Назначен',
    running: 'Работает',
    completed: 'Закончил',
    failed: 'Ошибка',
    interrupted: 'Прерван',
    cancelled: 'Отменен',
    cooldown: 'Cooldown'
  };

  return labels[item.status] ?? item.status;
}
