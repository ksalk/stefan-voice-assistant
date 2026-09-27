# Node - dotnet

f**k it, we ball
rewriting into dotnet

## Logging to an OpenTelemetry collector (OTLP)

The node ships its Serilog logs to an OpenTelemetry collector when `Log:Otlp:Endpoint` is
configured. The transport is OTLP/HTTP with protobuf encoding; the sink appends the `/v1/logs`
path to the configured base URL. Whenever the endpoint is empty or missing, only the console
and file sinks are used.

```json
"Log": {
  "Otlp": {
    "Endpoint": "https://your-collector.example.com",
    "ServiceName": "",
    "Headers": {}
  }
}
```

- `ServiceName` (optional) overrides `service.name`; the default is `Node:Name`.
- `Headers` (optional) adds arbitrary OTLP request headers, e.g. basic auth credentials.
- In compose, set `OTLP_ENDPOINT` in the environment to enable the sink.