using System.Net;
using System.Reactive.Linq;
using Gateway.Config;
using Gateway.Data;
using Gateway.Interfaces;
using MCDis256.Design.App.Conf.Interfaces;
using MCDis256.Design.App.Interface;
using MCDis256.Design.App.Interface.Log;
using MCDis256.Design.App.Interface.Log.Toolkit;
using MCDis256.Design.Rx;
using MCDis256.Design.Rx.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.OpenApi;

namespace Gateway;

public interface IWebServer;

public class WebServer : IWebServer, IAppComponent<IWebServer>
{
  private readonly ILog p_log;
  private readonly ILifetime p_lifetime;
  private readonly IAppConfigProvider p_cfg;
  private readonly IApplicationChannelService p_channelService;

  public static IWebServer Activate(IAppContext _context) =>
    _context.Activate((ILog _log, ILifetime _lifetime, IAppConfigProvider _cfg,
        IApplicationChannelService _channelService) =>
      new WebServer(_log, _lifetime, _cfg, _channelService));

  private WebServer(ILog _log, ILifetime _lifetime, IAppConfigProvider _cfg,
    IApplicationChannelService _channelService)
  {
    p_log = _log["web-server"];
    p_lifetime = _lifetime;
    p_cfg = _cfg;
    p_channelService = _channelService;

    var gatewayConfig = p_cfg.ResolveValidValue<GatewayConfig>();

    gatewayConfig
      .Throttle(TimeSpan.FromSeconds(1), _lifetime.Scheduler)
      .AssignIndex()
      .HotAlive(_lifetime, (_indexedCfg, _life) =>
      {
        var index = _indexedCfg.Index;
        var config = _indexedCfg.Value;

        var serverTask = Task.Run(async () =>
        {
          try
          {
            p_log.Info($"[{index}] Веб-сервер Gateway запускается...");
            using var host = CreateWebHost(config);
            p_log.Info($"[{index}] Веб-сервер шлюза создан на порту {config.Port}");

            await host.RunAsync(_lifetime.Cancellation).ConfigureAwait(false);
            p_log.Info($"[{index}] Веб-сервер Gateway остановлен");
          }
          catch (Exception e)
          {
            p_log.Error($"[{index}] Ошибка в потоке веб-сервера Gateway: {e}");
          }
        });
      });
  }

  private IHost CreateWebHost(GatewayConfig _config)
  {
    var builder = WebApplication.CreateSlimBuilder();

    // DI
    builder.Services.AddSingleton<GatewayConfig>(_ => _config);
    builder.Services.AddSingleton<IApplicationChannelService>(_ => p_channelService);

    // Swagger
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(_opt =>
    {
      _opt.SwaggerDoc("v1", new OpenApiInfo
      {
        Title = "Gateway API",
        Version = "v1",
        Description = "API for ThreeFlowPlatform Gateway - прием и распределение заявок"
      });
    });

    builder.WebHost.ConfigureKestrel(_options =>
    {
      _options.Listen(IPAddress.Any, _config.Port, _listenOptions =>
      {
        _listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
      });
    });

    var app = builder.Build();

    // Swagger UI
    app.UseSwagger(_options =>
      _options.RouteTemplate = "gateway/v0/api-docs/swagger/{documentName}/swagger.json");

    app.UseSwaggerUI(_options =>
    {
      _options.SwaggerEndpoint("/gateway/v0/api-docs/swagger/v1/swagger.json", "v1");
      _options.RoutePrefix = "gateway/v0/api-docs";
      _options.DisplayRequestDuration();
    });

    // API группа
    var apiGroup = app.MapGroup("gateway/v0");

    apiGroup.MapPost("/application",
        async (
          [FromBody] ApplicationRequest _req,
          IApplicationChannelService _service,
          CancellationToken _ct) =>
        {
          // Валидация веса заявки
          if (_req.Weight < 0 || _req.Weight > 100)
          {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
              ["Weight"] = new[] { "Вес заявки должен быть в диапазоне от 0 до 100" }
            },
            detail: $"Получен некорректный вес: {_req.Weight}. Допустимый диапазон: 0-100",
            title: "Ошибка валидации заявки");
          }

          // Валидация данных
          if (string.IsNullOrWhiteSpace(_req.Data))
          {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
              ["Data"] = new[] { "Данные заявки не могут быть пустыми" }
            },
            detail: "Поле Data обязательно для заполнения",
            title: "Ошибка валидации заявки");
          }

          var response = await _service.SubmitApplicationAsync(_req, _ct);
          return Results.Accepted($"/gateway/v0/application/{response.Id}", response);
        })
      .WithName("SubmitApplication")
      .WithSummary("Принять заявку")
      .WithDescription(
        "Принимает заявку, рассчитывает вес, распределяет по каналам (приоритетный/основной/вторичный)")
      .WithTags("Applications")
      .Produces<ApplicationResponse>(StatusCodes.Status202Accepted)
      .Produces(StatusCodes.Status400BadRequest);

    apiGroup.MapGet("/",
        () => Results.Ok(new
        {
          service = "Gateway",
          status = "healthy",
          timestamp = DateTime.UtcNow
        }))
      .WithName("HealthCheck")
      .WithSummary("Проверка работоспособности")
      .WithTags("Health");

    return app;
  }
}
