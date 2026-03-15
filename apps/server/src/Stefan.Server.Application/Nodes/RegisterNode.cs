namespace Stefan.Server.Application.Nodes;

public record RegisterNodeRequest(string NodeId, string SessionId);

public class RegisterNode
{
    public async Task Handle(RegisterNodeRequest request, CancellationToken cancellationToken)
    {
        
    }
}