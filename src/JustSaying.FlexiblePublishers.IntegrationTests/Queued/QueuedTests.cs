using System;
using System.Threading.Tasks;
using FluentAssertions;
using JustSaying.FlexiblePublishers.IntegrationTests.Queued.Messages;
using JustSaying.FlexiblePublishers.Queued;
using JustSaying.Messaging;
using NUnit.Framework;

namespace JustSaying.FlexiblePublishers.IntegrationTests.Queued;

[TestFixture]
public class QueuedTests
{
    [SetUp]
    public void SetupTests()
    {
        MessagingTestMessageHandler.ResetIds();
        RelayMessageHandler.ResetIds();
        RelayWhitelistMessageHandler.ResetIds();
    }

    [Test]
    public async Task SuccessfulMessage_should_send_messages()
    {
        var id = Guid.NewGuid().ToString();

        await Bootstrapper.ExecuteInScopeAsync(async () =>
        {
            var messagePublisher = Bootstrapper.Container.GetInstance<IQueuedMessagePublisher>();

            await messagePublisher.PublishAsync(new MessagingTestMessage(id, MessagingTestMessageResultsEnum.Success));

            await messagePublisher.ProcessQueueAsync(onlySendWhitelisted: false);
        });

        await Task.Delay(2000);

        MessagingTestMessageHandler.LastUniqueId.Should().Be(id);
        RelayMessageHandler.LastUniqueId.Should().Be(MessagingTestMessageHandler.RelayUniqueId);
        RelayWhitelistMessageHandler.LastUniqueId.Should().Be(MessagingTestMessageHandler.RelayWhitelistUniqueId);
    }

    [Test]
    public async Task FailedMessage_should_send_whitelisted_messages()
    {
        var id = Guid.NewGuid().ToString();

        await Bootstrapper.ExecuteInScopeAsync(async () =>
        {
            var messagePublisher = Bootstrapper.Container.GetInstance<IQueuedMessagePublisher>();

            await messagePublisher.PublishAsync(new MessagingTestMessage(id, MessagingTestMessageResultsEnum.Fail));

            await messagePublisher.ProcessQueueAsync(onlySendWhitelisted: false);
        });

        await Task.Delay(2000);

        MessagingTestMessageHandler.LastUniqueId.Should().Be(id);
        RelayMessageHandler.LastUniqueId.Should().BeNull();
        RelayWhitelistMessageHandler.LastUniqueId.Should().Be(MessagingTestMessageHandler.RelayWhitelistUniqueId);
    }

    [Test]
    public async Task ExceptionMessage_should_send_whitelisted_messages()
    {
        var id = Guid.NewGuid().ToString();

        await Bootstrapper.ExecuteInScopeAsync(async () =>
        {
            var messagePublisher = Bootstrapper.Container.GetInstance<IQueuedMessagePublisher>();

            await messagePublisher.PublishAsync(new MessagingTestMessage(id,
                MessagingTestMessageResultsEnum.Exception));

            await messagePublisher.ProcessQueueAsync(onlySendWhitelisted: false);
        });

        await Task.Delay(2000);

        MessagingTestMessageHandler.LastUniqueId.Should().Be(id);
        RelayMessageHandler.LastUniqueId.Should().BeNull();
        RelayWhitelistMessageHandler.LastUniqueId.Should().Be(MessagingTestMessageHandler.RelayWhitelistUniqueId);
    }
}
