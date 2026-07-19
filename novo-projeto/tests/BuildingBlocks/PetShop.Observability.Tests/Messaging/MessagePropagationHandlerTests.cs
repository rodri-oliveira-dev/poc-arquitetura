using System.Diagnostics;

using PetShop.Observability.Context;
using PetShop.Observability.Messaging;
using PetShop.Observability.Propagation;

using Xunit;

namespace PetShop.Observability.Tests.Messaging;

public sealed class MessagePropagationHandlerTests
{
    private const string ActivitySourceName = "PetShop.Observability.Tests";

    [Fact]
    public void StartConsumerActivity_ShouldContinueReceivedTraceAndRestoreBaggage()
    {
        using ActivityListener listener = CreateListener();
        ActivitySource.AddActivityListener(listener);

        using var activitySource = new ActivitySource(ActivitySourceName);
        string correlationId = Guid.NewGuid().ToString();
        PropagationContextSnapshot receivedContext;
        ActivityTraceId expectedTraceId;
        ActivitySpanId expectedParentSpanId;

        using (var parent = new Activity("request").SetIdFormat(ActivityIdFormat.W3C).Start())
        {
            parent.SetBaggage("region", "south america");
            receivedContext = PropagationContextSnapshot.CaptureCurrent(correlationId, "tenant-a");
            expectedTraceId = parent.TraceId;
            expectedParentSpanId = parent.SpanId;
        }

        var handler = new MessagePropagationHandler(new ExecutionContextAccessor());
        using Activity? consumer = handler.StartConsumerActivity(
            activitySource,
            "appointment.process",
            "kafka",
            "appointments.v1",
            receivedContext);

        Assert.NotNull(consumer);
        Assert.Equal(expectedTraceId, consumer.TraceId);
        Assert.Equal(expectedParentSpanId, consumer.ParentSpanId);
        Assert.Equal("south america", consumer.GetBaggageItem("region"));
        Assert.Equal(correlationId, consumer.GetBaggageItem(PropagationHeaderNames.CorrelationId));
        Assert.Equal("tenant-a", consumer.GetBaggageItem(PropagationHeaderNames.TenantId));
        Assert.Equal("kafka", consumer.GetTagItem("messaging.system"));
        Assert.Equal("appointments.v1", consumer.GetTagItem("messaging.destination.name"));
    }

    [Fact]
    public void StartProducerActivity_ShouldPublishContextFromNewProducerSpan()
    {
        using ActivityListener listener = CreateListener();
        ActivitySource.AddActivityListener(listener);

        using var activitySource = new ActivitySource(ActivitySourceName);
        string correlationId = Guid.NewGuid().ToString();
        PropagationContextSnapshot persistedContext;
        ActivityTraceId expectedTraceId;
        ActivitySpanId expectedParentSpanId;

        using (var parent = new Activity("request").SetIdFormat(ActivityIdFormat.W3C).Start())
        {
            persistedContext = PropagationContextSnapshot.CaptureCurrent(correlationId, "tenant-a");
            expectedTraceId = parent.TraceId;
            expectedParentSpanId = parent.SpanId;
        }

        var handler = new MessagePropagationHandler(new ExecutionContextAccessor());
        using Activity? producer = handler.StartProducerActivity(
            activitySource,
            "outbox.publish",
            "kafka",
            "appointments.v1",
            persistedContext);

        Assert.NotNull(producer);
        PropagationContextSnapshot outgoing = handler.CaptureCurrent(
            persistedContext.CorrelationId,
            persistedContext.TenantId);

        Assert.Equal(expectedTraceId, producer.TraceId);
        Assert.Equal(expectedParentSpanId, producer.ParentSpanId);
        Assert.Equal(producer.Id, outgoing.TraceParent);
        Assert.NotEqual(persistedContext.TraceParent, outgoing.TraceParent);
        Assert.Equal(correlationId, outgoing.CorrelationId);
        Assert.Equal("tenant-a", outgoing.TenantId);
    }

    [Fact]
    public void CaptureCurrent_ShouldUseAmbientExecutionContextWhenArgumentsAreMissing()
    {
        var accessor = new ExecutionContextAccessor();
        var handler = new MessagePropagationHandler(accessor);
        var ambient = new PropagationContextSnapshot(
            Guid.NewGuid().ToString(),
            "tenant-b",
            null,
            null,
            null);

        using IDisposable scope = accessor.Push(ambient);
        PropagationContextSnapshot captured = handler.CaptureCurrent();

        Assert.Equal(ambient.CorrelationId, captured.CorrelationId);
        Assert.Equal(ambient.TenantId, captured.TenantId);
    }

    private static ActivityListener CreateListener()
    {
        return new ActivityListener
        {
            ShouldListenTo = source => source.Name == ActivitySourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = static (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded
        };
    }
}
