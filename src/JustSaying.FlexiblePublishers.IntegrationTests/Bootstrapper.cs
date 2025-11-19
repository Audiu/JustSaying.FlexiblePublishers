using System;
using System.Threading;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using JustSaying.Extensions.DependencyInjection.SimpleInjector;
using JustSaying.FlexiblePublishers.IntegrationTests.Queued.Messages;
using JustSaying.FlexiblePublishers.Queued;
using JustSaying.FlexiblePublishers.Queued.Middleware;
using JustSaying.Messaging;
using JustSaying.Messaging.MessageHandling;
using JustSaying.Messaging.Middleware;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Serilog;
using SimpleInjector;
using SimpleInjector.Lifestyles;
using Testcontainers.LocalStack;

namespace JustSaying.FlexiblePublishers.IntegrationTests;

[SetUpFixture]
public class Bootstrapper
{
    public static ILoggerFactory LoggerFactory { get; private set; }

    public static Container Container { get; private set; }

    private static LocalStackContainer _localStackContainer;
    private static string _localStackServiceUrl;

    [OneTimeSetUp]
    public async Task FixtureSetup()
    {
        LoggerFactory = new LoggerFactory();

        var logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .CreateLogger();

        LoggerFactory.AddSerilog(logger);

        Log.Logger = logger;

        logger.Information("Configured logging");
        TestContext.Progress.WriteLine("Configured logging");

        // Start LocalStack container
        logger.Information("Starting LocalStack container...");
        TestContext.Progress.WriteLine("Starting LocalStack container...");

        _localStackContainer = new LocalStackBuilder()
            .WithImage("localstack/localstack:latest")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPath("/_localstack/health").ForPort(4566)))
            .Build();

        await _localStackContainer.StartAsync();

        // Construct the service URL from the container's host and port
        var host = _localStackContainer.Hostname;
        var port = _localStackContainer.GetMappedPublicPort(4566);
        _localStackServiceUrl = $"http://{host}:{port}";

        logger.Information($"LocalStack container started at {_localStackServiceUrl}");
        TestContext.Progress.WriteLine($"LocalStack container started at {_localStackServiceUrl}");

        try
        {
            Container = new Container();
            ConfigureInjection(Container);

            // Set dummy AWS credentials to allow Container.Verify() to succeed
            // These won't be used because LocalStack is configured with anonymous credentials
            var originalAccessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
            var originalSecretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");
            try
            {
                Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", "test");
                Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", "test");

                Container.Verify();
            }
            finally
            {
                // Restore original values
                Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", originalAccessKey);
                Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", originalSecretKey);
            }

            logger.Information("Configured and verified runtime injection");
            TestContext.Progress.WriteLine("Configured and verified runtime injection");

            // Boot listener
            using (AsyncScopedLifestyle.BeginScope(Container))
            {
                var messagingPublisher = Container.GetInstance<IQueuedMessagePublisher>();
                await messagingPublisher.StartAsync(CancellationToken.None);
            }

            var messagingBus = Container.GetInstance<IMessagingBus>();
            await messagingBus.StartAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to bootstrap");
            TestContext.Progress.WriteLine($"Failed to bootstrap {ex.Message}");

            throw;
        }
    }

    [OneTimeTearDown]
    public async Task FixtureTearDown()
    {
        Container?.Dispose();
        LoggerFactory?.Dispose();

        if (_localStackContainer != null)
        {
            await _localStackContainer.StopAsync();
            await _localStackContainer.DisposeAsync();
        }
    }

    private static void ConfigureInjection(Container container)
    {
        container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();

        container.RegisterInstance(Log.Logger);

        ConfigureJustSaying(container);
    }

    private static void ConfigureJustSaying(Container container)
    {
        var loggerFactory = new LoggerFactory();
        loggerFactory.AddSerilog(Log.Logger);

        container.RegisterInstance<ILoggerFactory>(loggerFactory);

        container.Register<QueuedMessagesMiddleware>(Lifestyle.Transient);

        container.AddJustSayingNoOpMessageMonitor();

        var builder = container.AddJustSayingReturnBuilder(
            new AwsConfig(null, null, "eu-west-1", _localStackServiceUrl),
            new MessagingConfig{
                Region = "eu-west-1",
            },
            builder =>
            {
                builder.Subscriptions(
                    x =>
                    {
                        x.ForTopic<MessagingTestMessage>(
                            cfg =>
                            {
                                cfg.WithMiddlewareConfiguration(
                                    m =>
                                    {
                                        m.UseSimpleInjectorScope();
                                        m.UseQueuedMessagesMiddleware();
                                        m.UseDefaults<MessagingTestMessage>(
                                            typeof(MessagingTestMessageHandler)); // Add default middleware pipeline
                                    });
                            });

                        x.ForTopic<RelayMessage>(
                            cfg =>
                            {
                                cfg.WithMiddlewareConfiguration(
                                    m =>
                                    {
                                        m.UseSimpleInjectorScope();
                                        m.UseQueuedMessagesMiddleware();
                                        m.UseDefaults<RelayMessage>(
                                            typeof(RelayMessageHandler)); // Add default middleware pipeline
                                    });
                            });

                        x.ForTopic<RelayWhitelistMessage>(
                            cfg =>
                            {
                                cfg.WithMiddlewareConfiguration(
                                    m =>
                                    {
                                        m.UseSimpleInjectorScope();
                                        m.UseQueuedMessagesMiddleware();
                                        m.UseDefaults<RelayWhitelistMessage>(
                                            typeof(RelayWhitelistMessageHandler)); // Add default middleware pipeline
                                    });
                            });
                    }
                );

                builder.Publications(
                    x =>
                    {
                        x.WithTopic<MessagingTestMessage>();
                        x.WithTopic<RelayMessage>();
                        x.WithTopic<RelayWhitelistMessage>();
                    });
            });

        container.Register<IHandlerAsync<MessagingTestMessage>, MessagingTestMessageHandler>(Lifestyle.Scoped);
        container.Register<IHandlerAsync<RelayMessage>, RelayMessageHandler>(Lifestyle.Scoped);
        container.Register<IHandlerAsync<RelayWhitelistMessage>, RelayWhitelistMessageHandler>(Lifestyle.Scoped);

        // Final steps (we might want to override our publishers/subscribers)
        var messagingRegistration = Lifestyle.Scoped.CreateRegistration(
            () => new QueuedMessagePublisher(loggerFactory, () => builder.BuildPublisher()),
            container);

        container.AddRegistration(typeof(IMessagePublisher), messagingRegistration);
        container.AddRegistration(typeof(IQueuedMessagePublisher), messagingRegistration);

        container.RegisterSingleton(() => builder.BuildSubscribers());
    }

    public static void ExecuteInScope(Action action)
    {
        using (AsyncScopedLifestyle.BeginScope(Container))
        {
            action();
        }
    }

    public static async Task ExecuteInScopeAsync(Func<Task> action)
    {
        using (AsyncScopedLifestyle.BeginScope(Container))
        {
            await action();
        }
    }
}
