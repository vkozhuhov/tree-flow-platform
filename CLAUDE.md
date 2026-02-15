# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Требования к проекту (из Tasks.md)

### Сервис 1 - Gateway
**Назначение**: Принимает заявки, распределяет по каналам, отправляет в сервисы 2 и 3

**Требования**:
- Три канала `Channel<T>`: приоритетный, основной, вторичный
- Распределение по весу заявки: >80 → приоритетный, 40-80 → основной, <40 → вторичный
- Принцип ТМО (приоритетный обслуживается первым, но без starvation)
- Имитация обработки: 1-2 секунды
- Отправка по Kafka → в сервис 3
- Отправка по gRPC → в сервис 2
- API: `POST /application` (возвращает 202 Accepted + ID)

### Сервис 2 - FileStorageService
**Назначение**: Эмуляция S3 и работа с файлами через gRPC

**Методы gRPC**:
- `healthCheck` - проверка работоспособности
- `writeToTemporalStorage` - сохранение во временное хранилище
- `moveToPermanentStorage` - перенос в постоянное хранилище + генерация PreSignedUrl
- `PreSignedUrl` - восстановление PreSignedUrl ссылки

**Взаимодействие**: Gateway → gRPC (заявка) → gRPC (файл) → PreSignedUrl

**Конфигурации**: Kafka/S3 через Vault (папка `/vault/secrets/`)

### Сервис 3 - ApplicationProcessor
**Назначение**: Обработка заявок, валидация, сохранение

**Требования**:
- Kafka consumer (слушает заявки от gateway)
- Параллельная обработка (каждая заявка - отдельная Task)
- Валидация с отправкой ошибок обратно в Kafka
- PostgreSQL + Npgsql (без EF)
- Статистика in-memory с периодическим сохранением
- Ответ в Kafka после обработки

**Конфигурации**: Kafka/PostgreSQL через Vault

---

## Архитектура проекта

ThreeFlowPlatform - это микросервисная платформа на .NET 10.0, использующая фреймворк **Design.App** (MCDis256).

### 1. Gateway
- **Тип**: Web API на Design.App с интеграцией ASP.NET Core
- **Расположение**: `/Gateway`
- **Назначение**: API Gateway с распределением заявок по каналам
- **Основные компоненты**:
  - `ApplicationChannelService` - управление Channel<T> (приоритетный, основной, вторичный)
  - `KafkaProducerService` - отправка в ApplicationProcessor
  - `GrpcFileStorageClient` - взаимодействие с FileStorageService
- **Точка входа**: Gateway/Program.cs:1 (должен использовать `AppHome.New()`)

### 2. FileStorageService
- **Тип**: gRPC сервис на Design.App
- **Расположение**: `/FileStorageService`
- **Назначение**: Эмуляция S3 через MinIO, работа с файлами
- **Протокол**: gRPC (Protobuf схемы в FileStorageService/Protos/)
- **Точка входа**: FileStorageService/Program.cs:1 (должен использовать `AppHome.New()`)

### 3. ApplicationProcessor
- **Тип**: Background Worker на Design.App
- **Расположение**: `/ApplicationProcessor`
- **Назначение**: Kafka consumer, валидация, сохранение в PostgreSQL
- **Точка входа**: ApplicationProcessor/Program.cs:1 (должен использовать `AppHome.New()`)

---

## Фреймворк Design.App (MCDis256)

### Основные концепции

Design.App - это корпоративный фреймворк для построения .NET приложений с модульной архитектурой.

**Ключевые принципы**:
1. **AppHome** - точка входа для создания приложения (`AppHome.New()`)
2. **Модульная система (Tiles)** - готовые блоки функциональности
3. **Dependency Injection** - собственный DI контейнер с Lazy-инициализацией (все зависимости - синглтоны)
4. **Reactive Extensions (Rx)** - для hot-reload конфигураций
5. **Интеграция с ASP.NET Core** - через модуль `UseWebServer()`

### Жизненный цикл приложения

1. **Configure** - настройка модулей (регистрация зависимостей)
2. **Export** - экспорт зависимостей в контейнер DI
3. **Wire** - связывание компонентов после экспорта
4. **PostWire** - финальная настройка после связывания
5. **PreStart** - подготовка перед запуском
6. **OnStarted** - выполнение после успешного запуска

### Базовая структура приложения на Design.App

```csharp
using MCDis256.Design.App;
using MCDis256.Design.App.Web;
using MCDis256.Design.App.Modules.Log;

public class Program
{
  static Task Main() =>
    AppHome.New()
      .UseLogAppInfo()              // Логирование информации о приложении
      .EnablePreparedConsoleLog()   // Консольное логирование
      .UseAppConfigDirectory()      // Конфигурации из папки conf/ (или /vault/secrets/ в production)
      .UseConfFile<AppConfig>()     // Загрузка конфигурации
      .UseExport<IMyService, MyService>()  // Регистрация сервисов
      .UseWebServer(_builder =>     // Веб-сервер (для Gateway и FileStorageService)
      {
        _builder
          .AddEndpoint(IPAddress.Any, 5000)
          .UseDependency<IMyService>()  // Интеграция AppHome DI с ASP.NET
          .Setup(_app =>
          {
            _app.MapPost("/api/endpoint", async (IMyService _service) =>
            {
              return await _service.Process();
            });
          });
      })
      .RunAsync();
}
```

### Dependency Injection в Design.App

| Метод | Назначение | Пример |
|-------|------------|--------|
| `UseExport<TType, TTarget>()` | Регистрация компонента через `IAppComponent<T>.Activate` | `UseExport<IMyService, MyService>()` |
| `UseExportFactory<TType>(factory)` | Регистрация через фабрику | `UseExportFactory<IKafka>(_ctx => new KafkaProducer(_ctx.Locate<ILog>()))` |
| `UseExportInstance<TType>(instance)` | Регистрация готового экземпляра | `UseExportInstance<IConfig>(config)` |
| `ctx.Locate<T>()` | Получение зависимости из контейнера | `var service = _ctx.Locate<IMyService>()` |

**Интеграция с ASP.NET**: Используйте `.UseDependency<T>()` в `UseWebServer()` для доступа к компонентам AppHome из ASP.NET endpoints.

### Конфигурации в Design.App

#### Определение конфигурации

```csharp
using MCDis256.Design.App.Conf.Interfaces.Attributes;

[AppConfig(Name = "gateway-config.json")]
public class GatewayConfig
{
  public string KafkaBroker { get; set; } = "";
  public string GrpcFileStorageUrl { get; set; } = "";
  public int TimeoutSeconds { get; set; } = 30;
}
```

#### Использование конфигураций

```csharp
AppHome.New()
  .UseAppConfigDirectory()  // Локально: conf/, Production: /vault/secrets/
  .OnStart(async (IAppContext _ctx, CancellationToken _ct) =>
  {
    var configProvider = _ctx.Locate<IAppConfigProvider>();
    var configResult = await configProvider.GetAsync<GatewayConfig>(_ct);

    if (configResult?.Status == AppConfigStatus.Ok && configResult.Value != null)
    {
      var config = configResult.Value;
      // Используем конфигурацию
    }
  })
  .RunAsync();
```

**Важно**:
- В локальной разработке конфигурации в папке `conf/`
- В production Vault подливает файлы в `/vault/secrets/`
- Используйте условную компиляцию:
  ```csharp
  DirectoryPath? appConfRoot = DirectoryPath.FromString("./conf/");
  #if !DEBUG
  appConfRoot = DirectoryPath.FromString("/vault/secrets/");
  #endif
  .UseAppConfigDirectory(appConfRoot)
  ```

### Логирование в Design.App

| Метод | Назначение |
|-------|------------|
| `UseLogAppInfo()` | Выводит информацию о приложении при старте |
| `EnablePreparedConsoleLog()` | Консольное логирование для разработки |
| `UseHourFileLog()` | Логирование в файлы с разбивкой по часам (папка `diag/logs/`) |
| `UseStdoutJsonLog()` | Логирование в stdout в формате JSON (для production) |

**Использование в коде**:
```csharp
.OnStart((IAppContext _ctx) =>
{
  _ctx.Log.Info("Приложение запущено");
  _ctx.Log.Warning("Предупреждение");
  _ctx.Log.Error("Ошибка");
})
```

---

## Структура C# проектов (Guidelines)

### Общая структура решения

```
ThreeFlowPlatform/
├── Gateway/
│   ├── Services/           (ApplicationChannelService, KafkaProducerService, GrpcFileStorageClient)
│   ├── Data/              (DTO, модели заявок)
│   ├── Config/            (GatewayConfig)
│   ├── Protos/            (Proto файлы для gRPC клиента)
│   ├── Program.cs
│   └── Gateway.csproj
├── FileStorageService/
│   ├── Services/          (gRPC сервисы: GreeterService → FileStorageService)
│   ├── Data/              (DTO)
│   ├── Config/            (FileStorageConfig)
│   ├── Protos/            (Proto файлы для gRPC сервера)
│   ├── Program.cs
│   └── FileStorageService.csproj
├── ApplicationProcessor/
│   ├── Services/          (KafkaConsumerService, ValidationService)
│   ├── Repositories/      (PostgreSQL репозитории через Npgsql)
│   ├── Data/              (DTO)
│   ├── Config/            (ProcessorConfig)
│   ├── Worker.cs
│   ├── Program.cs
│   └── ApplicationProcessor.csproj
├── Guideline/             (Документация и руководства)
├── ThreeFlowPlatform.sln
├── .editorconfig
├── .gitignore
└── CLAUDE.md
```

### Правила структуры проектов

1. **Папка Parts**: Используется только внутри модулей для скрытия внутренней логики (НЕ в корне проекта)
2. **Ограничение файлов**: Не более 7-10 файлов в одной папке
3. **Именование**: PascalCase для файлов и директорий в C#
4. **Термины предметной области**: Названия должны отражать бизнес-логику

### Стандартные папки

| Папка | Назначение |
|-------|------------|
| `Services` | Бизнес-логика, обработчики |
| `Data` | DTO, модели данных |
| `Config` | Конфигурации |
| `Repositories` | Работа с базами данных |
| `Extensions` | Методы расширения |
| `Interfaces` | Интерфейсы |
| `Parts` | Внутренние реализации (только внутри модулей!) |

---

## C# Coding Standards (Guidelines)

### Ключевые правила

#### 1. Отступы
- **2 пробела** на отступ (табуляция заменяется на пробелы)

#### 2. Именование

| Тип | Стиль | Пример |
|-----|-------|--------|
| Приватные поля | `p_` + camelCase | `private readonly ILog p_log;` |
| Публичные поля | PascalCase | `public string Name { get; }` |
| Локальные переменные | camelCase | `var result = ...;` |
| Аргументы функций | `_` + camelCase | `void Foo(int _value)` |
| Локальные функции | PascalCase | `void LocalFunction() { }` |
| Расширения (this) | `_this` | `public static void Ext(this IFoo _this)` |

#### 3. Использование `var`
- ✅ Используйте `var`, когда тип очевиден: `var list = new List<string>();`
- ❌ НЕ используйте `var`, когда тип неочевиден: `int? exitCode = ...;` (явно указать тип)

#### 4. Nullable Reference Types
- Включены во всех проектах (`<Nullable>enable</Nullable>`)
- Используйте `?` для nullable типов: `string? optionalName`

#### 5. Модификаторы доступа
- **Принцип минимальных разрешений**: Всегда используйте минимально необходимый уровень доступа
- По умолчанию: `private` для полей, `internal` для классов

#### 6. Async/Await
- Асинхронные методы должны заканчиваться на `Async`: `GetDataAsync()`
- Принимайте `CancellationToken` как последний параметр

---

## API Standards (RFC 7807)

### Единый формат ошибок (RFC 7807 + RFC 9457)

Все сервисы возвращают ошибки в формате `application/problem+json`:

```json
{
  "type": "https://docs.company.com/errors/user-not-found",
  "title": "User not found",
  "status": 404,
  "detail": "No user with id=123 exists.",
  "instance": "/api/v1/users/123",
  "correlationId": "b76aaf9e72d14d72a3dc6273b176e823",
  "errorCode": "USER_NOT_FOUND"
}
```

| Поле | Описание |
|------|----------|
| `type` | Ссылка на документацию ошибки |
| `title` | Краткое описание |
| `status` | HTTP-статус |
| `detail` | Детали для людей |
| `instance` | URL запроса |
| `correlationId` | ID для трассировки в логах |
| `errorCode` | Доменный код ошибки (для клиента) |

**Правила**:
- Любая бизнес-ошибка имеет уникальный `errorCode`
- `correlationId` обязателен для связи с логами
- Старые коды не переиспользуются

### Swagger/OpenAPI

**Установка**:
```bash
dotnet add package Swashbuckle.AspNetCore
```

**Настройка для Gateway**:
```csharp
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(_c =>
{
  _c.SwaggerDoc("v1", new OpenApiInfo
  {
    Version = "v1",
    Title = "Gateway API",
    Description = "API for ThreeFlowPlatform Gateway"
  });
});

app.UseSwagger(_options =>
  _options.RouteTemplate = "gateway/v0/api-docs/swagger/{documentname}/swagger.json");

app.UseSwaggerUI(_options =>
{
  _options.SwaggerEndpoint("/gateway/v0/api-docs/swagger/v1/swagger.json", "v1");
  _options.RoutePrefix = "gateway/v0/api-docs";
  _options.DisplayRequestDuration();
});
```

**Документирование endpoints**:
```csharp
apiGroup.MapPost("/application",
  async (ApplicationRequest _req, IApplicationService _service, CancellationToken _ct) =>
  {
    return await _service.SubmitApplication(_req, _ct);
  })
  .WithSummary("Принять заявку")
  .WithDescription("Принимает заявку, рассчитывает вес, распределяет по каналам")
  .WithTags("Applications");
```

---

## NuGet Пакеты

### Gateway

```bash
# Design.App
dotnet add Gateway/Gateway.csproj package MCDis256.Design.App
dotnet add Gateway/Gateway.csproj package MCDis256.Design.App.Web
dotnet add Gateway/Gateway.csproj package MCDis256.Design.App.Conf
dotnet add Gateway/Gateway.csproj package MCDis256.Design.App.Logging
dotnet add Gateway/Gateway.csproj package MCDis256.Design.App.CrashReporter

# Swagger
dotnet add Gateway/Gateway.csproj package Swashbuckle.AspNetCore

# Kafka
dotnet add Gateway/Gateway.csproj package Confluent.Kafka

# gRPC клиент
dotnet add Gateway/Gateway.csproj package Grpc.Net.Client
dotnet add Gateway/Gateway.csproj package Google.Protobuf
dotnet add Gateway/Gateway.csproj package Grpc.Tools
```

### FileStorageService

```bash
# Design.App
dotnet add FileStorageService/FileStorageService.csproj package MCDis256.Design.App
dotnet add FileStorageService/FileStorageService.csproj package MCDis256.Design.App.Web
dotnet add FileStorageService/FileStorageService.csproj package MCDis256.Design.App.Conf
dotnet add FileStorageService/FileStorageService.csproj package MCDis256.Design.App.Logging

# gRPC сервер
dotnet add FileStorageService/FileStorageService.csproj package Grpc.AspNetCore

# MinIO (эмуляция S3)
dotnet add FileStorageService/FileStorageService.csproj package Minio
```

### ApplicationProcessor

```bash
# Design.App
dotnet add ApplicationProcessor/ApplicationProcessor.csproj package MCDis256.Design.App
dotnet add ApplicationProcessor/ApplicationProcessor.csproj package MCDis256.Design.App.Conf
dotnet add ApplicationProcessor/ApplicationProcessor.csproj package MCDis256.Design.App.Logging

# Kafka
dotnet add ApplicationProcessor/ApplicationProcessor.csproj package Confluent.Kafka

# PostgreSQL
dotnet add ApplicationProcessor/ApplicationProcessor.csproj package Npgsql
dotnet add ApplicationProcessor/ApplicationProcessor.csproj package Dapper
```

---

## Команды для разработки

### Сборка проекта
```bash
# Сборка всего решения
dotnet build ThreeFlowPlatform.sln

# Сборка отдельного проекта
dotnet build Gateway/Gateway.csproj
dotnet build FileStorageService/FileStorageService.csproj
dotnet build ApplicationProcessor/ApplicationProcessor.csproj
```

### Запуск проектов
```bash
# Запуск Gateway
dotnet run --project Gateway/Gateway.csproj

# Запуск FileStorageService
dotnet run --project FileStorageService/FileStorageService.csproj

# Запуск ApplicationProcessor
dotnet run --project ApplicationProcessor/ApplicationProcessor.csproj
```

### Работа с gRPC
При изменении proto файлов требуется пересборка проекта для регенерации C# классов:
```bash
dotnet build FileStorageService/FileStorageService.csproj
```

---

## Важные ссылки

- **Guidelines**: `/Guideline/` - полная документация по стандартам разработки
- **Tasks**: `Gateway/Tasks.md` - официальное ТЗ "Три сервиса"
- **Design.App Guide**: `Gateway/design.app.md` - подробное руководство по фреймворку

---

## Технические детали

- **Целевой фреймворк**: .NET 10.0
- **Nullable reference types**: Включены во всех проектах
- **Implicit usings**: Включены во всех проектах
- **Фреймворк**: Design.App (MCDis256) для всех проектов
- **Конфигурации**:
  - Локально: `conf/` папка
  - Production: `/vault/secrets/` (Vault подливает файлы)
- **Логирование**:
  - Локально: консоль (`EnablePreparedConsoleLog()`)
  - Production: JSON в stdout (`UseStdoutJsonLog()`)