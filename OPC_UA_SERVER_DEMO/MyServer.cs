using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Opc.Ua;
using Opc.Ua.Server;
using System.Collections.Generic;
namespace OPC_UA_SERVER_DEMO
{
    public class MyServer : StandardServer
    {
        protected override MasterNodeManager CreateMasterNodeManager(IServerInternal server, ApplicationConfiguration config)
        {
            var nodeManagers = new List<INodeManager>
        {
            new MyNodeManager(server, config)
        };

            return new MasterNodeManager(server, config, null, nodeManagers.ToArray());
        }
    }
}
