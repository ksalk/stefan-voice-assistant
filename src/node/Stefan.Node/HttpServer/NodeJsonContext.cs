using System.Text.Json.Serialization;

namespace Stefan.Node.HttpServer;

[JsonSerializable(typeof(NodeStatusResponse))]
internal partial class NodeJsonContext : JsonSerializerContext
{
}
