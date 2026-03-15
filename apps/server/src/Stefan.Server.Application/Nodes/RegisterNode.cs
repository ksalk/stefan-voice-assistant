using Stefan.Server.Domain;
using Stefan.Server.Infrastructure;

namespace Stefan.Server.Application.Nodes;

public record RegisterNodeRequest(string NodeName, string SessionId, string IpAddress, int Port);

public class RegisterNode(StefanDbContext dbContext)
{
    public async Task Handle(RegisterNodeRequest request, CancellationToken cancellationToken)
    {
        var node = dbContext.Nodes.FirstOrDefault(n => n.Name == request.NodeName);
        if (node == null)
        {
            node = Node.Create(request.NodeName, request.SessionId, request.IpAddress, request.Port);
            dbContext.Nodes.Add(node);
        }
        else
        {
            node.Connect(request.SessionId, request.IpAddress, request.Port);
            dbContext.Nodes.Update(node);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}