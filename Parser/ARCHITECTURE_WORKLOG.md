# Parser Architecture Worklog

Этот файл обязателен к проверке перед любыми задачами, запуском или исправлениями,
связанными с parser-архитектурой. Цель файла - не решать повторно одни и те же
проблемы с кодировками, названиями ниш, proxy, мониторингом и batch-flow.

## Правила работы

- Перед parser-related задачей сначала прочитать этот файл.
- Если ошибка уже описана ниже, не подбирать новое случайное решение, а проверить
  прежнюю причину и исправить первопричину.
- После каждого устойчивого решения или повторной проблемы обновлять этот файл.
- UI мониторинга должен показывать реальные данные сервиса. Нельзя скрывать
  дубли running-процессов на frontend-е вместо исправления backend/parser-инварианта.
- Batch size для production-like smoke остается 100 карточек. Ограничивается
  число batch-ей или длительность smoke, а не размер batch-а.

## Текущие архитектурные решения

- Production parser запускается через supervisor: один child process на один
  enabled proxy/niche.
- Proxy-4 и proxy-5 могут быть в `proxy_mapping.local.json`, но остаются idle,
  пока за ними не закреплена enabled-ниша.
- Каждый proxy должен иметь отдельные:
  - Playwright browser profile;
  - cookie/token cache;
  - local durable outbox SQLite/payload directory;
  - per-proxy rate limiter и cooldown.
- Parser отправляет batch-и только через server queue:
  - `POST /api/v1/parser/batches`;
  - без прямого `--stage-to-db` в production/smoke пути.
- Local durable outbox удаляет payload только после server status `completed`.
- Для production parser выбран возврат к full price split:
  - сначала полный split диапазонов цен по нише;
  - потом обязательная пауза;
  - потом выгрузка карточек batch-ами по 100;
  - proxy работают параллельно, но внутри proxy этапы последовательны.

## Текущие production-ниши и proxy

Названия и иерархию нельзя угадывать. Их нужно брать из WB menu/tree.

- `proxy-1`:
  - root/path: `Женщинам / Платья и сарафаны`;
  - display/sourceSubcategory: `Платья и сарафаны`;
  - query: `menu_v3_8137 платье женские`.
- `proxy-2`:
  - correct path: `Обувь / Мужская / Кеды и кроссовки`;
  - display/sourceSubcategory: `Мужские кеды и кроссовки`;
  - query: `menu_redirect_subject_v2_8194 мужские кеды и кроссовки`.
- `proxy-3`:
  - root/path: `Красота / Органическая косметика`;
  - display/sourceSubcategory: `Органическая косметика`;
  - query: `menu_redirect_subject_v2_10012 органическая косметика`.

## Кодировки и конфиги

### UTF-8 BOM в JSON

Проблема уже возникала: PowerShell `Set-Content -Encoding UTF8` может записать
JSON с BOM, после чего Python JSON loader падает с ошибкой вида
`Unexpected UTF-8 BOM`.

Правило:

- Не переписывать JSON-конфиги parser-а дефолтным PowerShell `Set-Content`.
- Если нужно механически проверить файл, первые байты JSON должны начинаться с
  `{` (`123`) или `[` (`91`), а не с BOM `239,187,191`.
- Для правок предпочитать `apply_patch`.

### Mojibake

Проблема уже возникала: в конфигах и UI появлялись строки вида `Рџ...`.

Правило:

- Не копировать поврежденную кириллицу из логов/старых файлов.
- Источник истины по WB-названиям - WB category tree или explicit niches config.
- Если повторно появляется mojibake, искать место генерации/перезаписи файла, а
  не править только отображение.

## Local vs Docker URL

Локальный запуск приложения:

- API: `http://localhost:5019`;
- parser queue URL: `http://localhost:5019/api/v1/parser`.

Docker/prod запуск:

- parser queue URL: `http://api:8080/api/v1/parser`.

Важно: env в production preset может переопределять shell env. При странных
ошибках доставки batch-ей сначала проверить итоговый queue URL в логах.

## Proxy и marketplace safety

- Marketplace не должен видеть IP машины сервиса/parser-а.
- При `PARSER_REQUIRE_PROXY_FOR_MARKETPLACE=1` любой WB HTTP client обязан идти
  через proxy.
- Последняя успешная preflight-проверка показывала разные egress IP:
  - `proxy-1`: `46.161.30.248`;
  - `proxy-2`: `194.226.247.162`;
  - `proxy-3`: `194.226.247.144`.
- Если два enabled proxy дают один external IP, production-cycle не должен
  стартовать.
- В логи нельзя выводить полный token, password или proxy credentials.

## Monitoring-инварианты

- Для одного `(parserInstanceId, proxyKey)` может быть только один running
  proxy-run.
- Backend должен запрещать второй running proxy-run, а не UI должен скрывать
  дубли.
- UI должен показывать реальные данные.
- Статусы proxy-run:
  - `ranges` -> `Диапазоны`;
  - `download` -> `Выгрузка`;
  - `completed` -> `Закончил`;
  - `failed` -> `Ошибка`;
  - cooldown/rate-limit должен быть виден как ошибка/причина.
- После full split product progress должен показывать полный план:
  - для smoke 3 batch: `0/300`, затем `100/300`, `200/300`, `300/300`;
  - не `0/100`, если уже известно, что запланировано 3 batch-а.

## Price split и пустые диапазоны

- Пустой диапазон, где WB явно вернул `total = 0`, не является ошибкой.
  Его нужно закрывать как successfully processed / skipped.
- Ответ без `total` или с битой metadata - не пустой диапазон, а некорректный
  ответ.
- `429/498` не является пустым диапазоном.
- После full split перед catalog fetch обязательна задержка
  `PARSER_SPLIT_TO_FETCH_DELAY_SECONDS`.

## Rate limit / 429

Проблема уже возникала при filter/full split.

Правила:

- Не уменьшать batch size, чтобы "обойти" 429.
- При 429 не продолжать долбить WB тем же proxy.
- Переводить текущий proxy-run в failed/cooldown с понятной причиной.
- Проверять, что задержки есть:
  - между filter/full split запросами;
  - между окончанием split и catalog fetch;
  - между catalog/enrichment запросами одного proxy.
- Разные proxy не должны блокировать друг друга глобальным lock-ом.

Последние значения, которые уже пробовали для борьбы с 429:

- `PARSER_FILTER_MIN_REQUEST_GAP_SECONDS=120`;
- `PARSER_FILTER_JITTER_SECONDS=30`;
- `PARSER_FILTERS_MAX_RETRYABLE_STATUSES=2`;
- `PARSER_MAX_RETRIES=3`.

Это не гарантия решения. Если 429 повторится, искать причину в proxy health,
token/session, WB response pattern и timing, а не повторять те же правки.

## Последнее известное состояние локальной среды

- EF migrations применялись до `20260701210801_ParserProxyRunPhaseProgress`.
- API/frontend/worker запускались локально; API health возвращал `200`.
- Parser smoke 3 proxy x 3 batch x 100 запускался, но ранее доходил до 429 на
  filter/full split и не создавал batch submissions.
- Backend tests проходили: `100 passed`.
- Parser tests по измененным зонам проходили: `14 passed` для
  `test_search_phrase_parser.py` и `test_runner_streaming.py`.

## Checklist перед следующим parser-запуском

1. Прочитать этот файл.
2. Проверить, что нет зависших local parser Python-процессов.
3. Проверить, что нет running `ParserProxyRuns` для тех же proxy.
4. Проверить UTF-8/no BOM у parser JSON-конфигов, если они менялись.
5. Проверить local vs Docker queue URL.
6. Проверить наличие `proxy_mapping.local.json`; credentials не коммитить.
7. Запустить proxy preflight: external IP уникальны, token/session по proxy
   отдельные.
8. Если появляется 429, остановить повторные запросы, закрыть proxy-run
   failed/cooldown и зафиксировать причину в этом файле.

