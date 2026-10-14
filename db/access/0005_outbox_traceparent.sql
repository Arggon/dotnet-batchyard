-- W3C trace context of the request that wrote the event (ADR 0016), so the trace continues through RabbitMQ.
ALTER TABLE dbo.Outbox ADD TraceParent VARCHAR(55) NULL;
