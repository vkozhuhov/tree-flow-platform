# ThreeFlowPlatform

Микросервисная платформа на .NET 10.0 для асинхронной обработки заявок с распределением по приоритетам, построенная на корпоративном фреймворке **Design.App (MCDis256)**.

## Архитектура

Платформа состоит из трёх микросервисов:

### 1. Gateway
**API Gateway** для приёма и распределения заявок по каналам с приоритетами.

- **Тип**: Web API (ASP.NET Core + Design.App)
- **Функции**:
  - Приём заявок через REST API (`POST /application`)
  - Распределение по трём каналам: приоритетный (>80), основной (40-80), вторичный (<40)
  - Принцип TMO (Теория массового обслуживания) — приоритетный обслуживается первым без starvation
  - Отправка в Kafka → ApplicationProcessor
  - Отправка по gRPC → FileStorageService
- **Технологии**: Channel<T>, Kafka Producer, gRPC Client, Swagger

### 2. FileStorageService
**gRPC сервис** для работы с файлами и эмуляции S3-хранилища.

- **Тип**: gRPC Server (Design.App)
- **Методы**:
  - `healthCheck` — проверка работоспособности
  - `writeToTemporalStorage` — сохранение во временное хранилище
  - `moveToPermanentStorage` — перенос в постоянное хранилище + генерация PreSignedUrl
  - `PreSignedUrl` — восстановление ссылки на файл
- **Технологии**: gRPC, MinIO (S3-совместимое хранилище)

### 3. ApplicationProcessor
**Background Worker** для обработки заявок из Kafka.

- **Тип**: Worker Service (Design.App)
- **Функции**:
  - Потребление сообщений из Kafka
  - Параллельная обработка заявок (каждая в отдельной Task)
  - Валидация с отправкой ошибок обратно в Kafka
  - Сохранение в PostgreSQL (Npgsql, без EF)
  - Сбор статистики in-memory с периодическим сохранением
- **Технологии**: Kafka Consumer, PostgreSQL, Dapper

## Технологический стек

- **.NET**: 10.0
- **Фреймворк**: Design.App (MCDis256)
- **Messaging**: Apache Kafka
- **Storage**: MinIO (S3-compatible), PostgreSQL
- **RPC**: gRPC (Protobuf)
- **DI**: Custom DI контейнер Design.App (Lazy Singleton)
- **Конфигурация**: JSON + Reactive Extensions (hot-reload)

## Инфраструктура

Docker Compose включает:
- **Kafka + Zookeeper** (порты: 9092, 9093)
- **PostgreSQL** (порт: 5433)
- **MinIO** (порты: 9000, 9001)
- **Kafka UI** (порт: 8080) — мониторинг Kafka

## Быстрый старт

### 1. Запуск инфраструктуры

```bash
docker-compose up -d
```

### 2. Сборка решения

```bash
dotnet build ThreeFlowPlatform.sln
```

### 3. Запуск сервисов

В отдельных терминалах:

```bash
# Gateway
dotnet run --project Gateway/Gateway.csproj

# FileStorageService
dotnet run --project FileStorageService/FileStorageService.csproj

# ApplicationProcessor
dotnet run --project ApplicationProcessor/ApplicationProcessor.csproj
```

### 4. Проверка работоспособности

- **Gateway API**: http://localhost:5000/gateway/v0/api-docs (Swagger)
- **Kafka UI**: http://localhost:8080
- **MinIO Console**: http://localhost:9001 (admin/adminadmin)

## Конфигурация

### Локальная разработка
Конфигурации в папке `conf/` каждого сервиса:
- `Gateway/conf/gateway-config.json`
- `FileStorageService/conf/filestorage-config.json`
- `ApplicationProcessor/conf/processor-config.json`

### Production
Конфигурации монтируются Vault в `/vault/secrets/`

## Структура проекта

```
ThreeFlowPlatform/
├── Gateway/                # API Gateway
│   ├── Services/          # Бизнес-логика (Channels, Kafka, gRPC)
│   ├── Data/              # DTO
│   ├── Config/            # Конфигурация
│   └── Protos/            # gRPC клиент
├── FileStorageService/     # gRPC сервис хранилища
│   ├── Services/          # gRPC сервисы
│   ├── Data/              # DTO
│   ├── Config/            # Конфигурация
│   └── Protos/            # gRPC сервер
├── ApplicationProcessor/   # Kafka Consumer + БД
│   ├── Services/          # Обработка, валидация
│   ├── Repositories/      # PostgreSQL
│   ├── Data/              # DTO
│   ├── Database/          # SQL-скрипты
│   └── Config/            # Конфигурация
├── Gateway.Tests/          # Unit-тесты
├── docker-compose.yml      # Инфраструктура
└── ThreeFlowPlatform.sln
```

## Стандарты кодирования

- **Отступы**: 2 пробела
- **Nullable**: Включён во всех проектах
- **Именование**:
  - Приватные поля: `p_fieldName`
  - Аргументы: `_argName`
  - Публичные свойства: `PascalCase`
- **API Errors**: RFC 7807 (application/problem+json)

## Документация

- `CLAUDE.md` — руководство для AI-ассистентов
- `Guideline/` — стандарты разработки
- Swagger: `/gateway/v0/api-docs`

## Разработка

### Основные команды

```bash
# Сборка
dotnet build

# Тесты
dotnet test Gateway.Tests/

# Очистка
dotnet clean

# Форматирование (EditorConfig)
dotnet format
```

### gRPC

При изменении `.proto` файлов требуется пересборка:

```bash
dotnet build FileStorageService/FileStorageService.csproj
dotnet build Gateway/Gateway.csproj
```

## Мониторинг

- **Логи**: stdout (JSON в production, консоль локально)
- **Kafka UI**: http://localhost:8080
- **MinIO**: http://localhost:9001
- **PostgreSQL**: порт 5433 (логин: postgres/12341234)

## Лицензия

Корпоративный проект на базе Design.App (MCDis256).
