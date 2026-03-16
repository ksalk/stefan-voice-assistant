using Stefan.Server.Domain;
using Stefan.Server.Infrastructure;

namespace Stefan.Server.Application.Nodes;

public class RegisterNodeRequest
{
    public string NodeName { get; set; }
    public string SessionId { get; set; }
    public string IpAddress { get; set; }
    public int Port { get; set; }
}   

public class RegisterNode(StefanDbContext dbContext, INodePingScheduler pingScheduler)
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

        // Schedule recurring ping job for this node
        await pingScheduler.SchedulePingAsync(node.Id, cancellationToken);
    }
}