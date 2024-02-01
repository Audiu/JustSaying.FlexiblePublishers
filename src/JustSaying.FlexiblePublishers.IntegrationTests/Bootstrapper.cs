using System;
using System.Threading;
using System.Threading.Tasks;
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

namespace JustSaying.FlexiblePublishers.IntegrationTests;

[SetUpFixture]
public class Bootstrapper
{
    public static ILoggerFactory LoggerFactory { get; private set; }

    public static Container Container { get; private set; }

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

        try
        {
            Container = new Container();
            ConfigureInjection(Container);

            Container.Verify();

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
    public void FixtureTearDown()
    {
        Container?.Dispose();
        LoggerFactory?.Dispose();
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

        var builder = container.AddJustSayingReturnBuilder(
            new AwsConfig(null, null, "eu-west-1", "http://localhost.localstack.cloud:4566"),
            null,
            null,
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
