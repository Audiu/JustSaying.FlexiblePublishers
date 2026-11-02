using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using JustSaying.FlexiblePublishers.IntegrationTests.Queued.Messages;
using JustSaying.FlexiblePublishers.Queued;
using JustSaying.Models;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace JustSaying.FlexiblePublishers.IntegrationTests.Queued;

[TestFixture]
public class SendOutcomeHookTests
{
    [Test]
    public async Task ProcessQueue_reports_each_message_that_was_sent_and_each_that_failed()
    {
        // The bus has a publication for MessagingTestMessage but not for UnpublishedMessage, so sending the second
        // one fails inside ProcessQueueAsync, which logs and swallows it.
        var publisher = new RecordingQueuedMessagePublisher(Bootstrapper.LoggerFactory);
        var sent = new MessagingTestMessage(Guid.NewGuid().ToString(), MessagingTestMessageResultsEnum.Success);
        var failed = new UnpublishedMessage();

        await publisher.PublishAsync(sent, CancellationToken.None);
        await publisher.PublishAsync(failed, CancellationToken.None);

        await publisher.ProcessQueueAsync(onlySendWhitelisted: false);

        publisher.Published.Should().ContainSingle().Which.Should().BeSameAs(sent);
        publisher.Failed.Should().ContainSingle().Which.Message.Should().BeSameAs(failed);
        publisher.Failed[0].Exception.Should().NotBeNull();
        publisher.QueuedItems.Should().Be(0);
    }

    [Test]
    public async Task A_message_dropped_by_the_whitelist_only_flush_is_neither_published_nor_failed()
    {
        var publisher = new RecordingQueuedMessagePublisher(Bootstrapper.LoggerFactory);

        await publisher.PublishAsync(new MessagingTestMessage(Guid.NewGuid().ToString(), MessagingTestMessageResultsEnum.Success), CancellationToken.None);

        await publisher.ProcessQueueAsync(onlySendWhitelisted: true);

        publisher.Published.Should().BeEmpty();
        publisher.Failed.Should().BeEmpty();
    }

    private sealed class UnpublishedMessage : Message
    {
    }

    private sealed class RecordingQueuedMessagePublisher : QueuedMessagePublisher
    {
        public RecordingQueuedMessagePublisher(ILoggerFactory loggerFactory)
            // The bus publisher is process-wide: the bootstrapper's first queued publisher already created it
            // (QueuedMessagePublisher keeps it in a static Lazy), so this factory is never invoked.
            : base(loggerFactory, () => throw new InvalidOperationException("The bootstrapper creates the bus publisher"))
        {
        }

        public List<Message> Published { get; } = new List<Message>();

        public List<(Message Message, Exception Exception)> Failed { get; } = new List<(Message, Exception)>();

        protected override void OnMessagePublished(Message message)
        {
            Published.Add(message);
        }

        protected override void OnMessagePublishFailed(Message message, Exception exception)
        {
            Failed.Add((message, exception));
        }
    }
}
