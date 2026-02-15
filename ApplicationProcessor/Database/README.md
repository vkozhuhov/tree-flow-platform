# Database Setup для ApplicationProcessor

## Инициализация базы данных

Для работы ApplicationProcessor необходимо выполнить SQL скрипт инициализации:

```bash
psql -h localhost -U postgres -d threeflow -f Database/init.sql
```

## Структура базы данных

### Схема: `processor`

### Таблица: `processor.applications`

| Колонка | Тип | Описание |
|---------|-----|----------|
| id | UUID | Уникальный идентификатор заявки (PRIMARY KEY) |
| weight | INT | Вес заявки (0-100) |
| data | TEXT | Данные заявки |
| files | TEXT[] | Массив имен файлов |
| channel | VARCHAR(50) | Канал обработки (Priority, Main, Secondary) |
| created_at | TIMESTAMP | Дата и время создания |
| processed_at | TIMESTAMP | Дата и время обработки |
| status | VARCHAR(50) | Статус (pending, processed, failed) |
| error_message | TEXT | Сообщение об ошибке (если есть) |

### Таблица: `processor.statistics`

| Колонка | Тип | Описание |
|---------|-----|----------|
| id | SERIAL | Автоинкремент ID |
| total_processed | BIGINT | Всего обработано заявок |
| total_failed | BIGINT | Всего ошибок |
| total_validation_errors | BIGINT | Всего ошибок валидации |
| channel_priority | BIGINT | Заявок из приоритетного канала |
| channel_main | BIGINT | Заявок из основного канала |
| channel_secondary | BIGINT | Заявок из вторичного канала |
| saved_at | TIMESTAMP | Время сохранения статистики |

## Функции PostgreSQL

### `processor.f_insert_application(...)`
Вставка новой заявки в БД.

**Параметры:**
- `p_id` (UUID) - ID заявки
- `p_weight` (INT) - Вес заявки
- `p_data` (TEXT) - Данные
- `p_files` (TEXT[]) - Файлы
- `p_channel` (VARCHAR) - Канал
- `p_created_at` (TIMESTAMP) - Время создания

**Возвращает:** BOOLEAN (TRUE при успехе)

### `processor.f_update_application_status(...)`
Обновление статуса заявки после обработки.

**Параметры:**
- `p_id` (UUID) - ID заявки
- `p_status` (VARCHAR) - Новый статус
- `p_error_message` (TEXT) - Сообщение об ошибке (может быть NULL)
- `p_processed_at` (TIMESTAMP) - Время обработки

**Возвращает:** BOOLEAN (TRUE если запись обновлена)

### `processor.f_get_application_by_id(...)`
Получение заявки по ID.

**Параметры:**
- `p_id` (UUID) - ID заявки

**Возвращает:** TABLE с данными заявки

### `processor.f_save_statistics(...)`
Сохранение статистики обработки.

**Параметры:**
- `p_total_processed` (BIGINT) - Всего обработано
- `p_total_failed` (BIGINT) - Всего ошибок
- `p_total_validation_errors` (BIGINT) - Ошибок валидации
- `p_channel_priority` (BIGINT) - Заявок Priority
- `p_channel_main` (BIGINT) - Заявок Main
- `p_channel_secondary` (BIGINT) - Заявок Secondary

**Возвращает:** BOOLEAN (TRUE при успехе)

## Тестирование

Для проверки работы функций:

```sql
-- Вставка тестовой заявки
SELECT processor.f_insert_application(
  gen_random_uuid(),
  75,
  'Тестовая заявка',
  ARRAY['file1.pdf', 'file2.docx'],
  'Main',
  NOW()
);

-- Проверка
SELECT * FROM processor.applications;
```
