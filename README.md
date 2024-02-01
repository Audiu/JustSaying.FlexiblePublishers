# JustSaying.FlexiblePublishers

A set of [JustSaying](https://github.com/justeattakeaway/JustSaying) IMessagePublisher replacements to perform different functions

## QueuedMessagePublisher

A publisher that queues messages to be published at a later time. This is useful for when you want to publish messages in UoW functionality and you don't want to send the messages until the UoW is committed.

When publishing a message it can be optionally marked as `isWhitelisted`. When processing the queue, all messages can be sent or only the ones marked as `isWhitelisted`.

A middleware is provided which add this functionality onto `IHandlerAsync<>` handlers automatically.

### Usage

Either the `IQueuedMessagePublisher` interface can be registered into DI which wraps the `IMessagePublisher` instance, OR the `IQueuedMessagePublisher` can be registered for both `IMessagePublisher` resolutions and `IQueuedMessagePublisher` resolutions, which is better as 3rd party code messages will be queued into the UoW as expected.

These examples use SimpleInjector but the theory is the same for all.

Setting up:

```csharp
// Example for existing IMessagePublisher registration
// container.RegisterSingleton(() => builder.BuildPublisher());

// Approach to wrap the built IMessagePublisher into a QueuedMessagePublisher, then register this as both IMessagePublisher and IQueuedMessagePublisher
var messagingRegistration = Lifestyle.Scoped.CreateRegistration(
    () => new QueuedMessagePublisher(loggerFactory, () => builder.BuildPublisher()),
    container);

container.AddRegistration(typeof(IMessagePublisher), messagingRegistration);
container.AddRegistration(typeof(IQueuedMessagePublisher), messagingRegistration);
```

#### Configuring middleware:

```csharp
x.ForTopic<TestMessage>(
        cfg =>
        {
            cfg.WithMiddlewareConfiguration(m =>
            {
                m.UseQueuedMessagesMiddleware();
                m.UseDefaults<TestMessage>(typeof(TestMessageHandler)); // Add default middleware pipeline
            });
        });
```

#### Using in app:

Publish a message normally:

```csharp
private readonly IMessagePublisher _messagePublisher;

await _messagePublisher.PublishAsync(new TestMessage());
```

Publish a "isWhitelisted" message - this requires the IQueuedMessageInterface

```csharp
private readonly IQueuedMessagePublisher _messagePublisher;

await _messagePublisher.PublishAsync(new TestMessage(), isWhitelisted: true);
```

## Additional Notes

If using elsewhere (UoW approach on web controllers), then the queue must be processed at the end of the request handler.
If using with a fully manual implementation then the queue must be processed once ready.

There is an example middleware for just with JustSaying 7 in here, and the same approach can be provided to middlewares for Mediator, ASP.NET Core pipelines etc.