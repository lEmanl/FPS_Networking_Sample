using UnityEngine;
using System.Collections;
using Unity.Networking.Transport;
using Unity.Collections;

public class ClientBehavior : MonoBehaviour
{
    public UdpNetworkDriver Driver;
    public NetworkConnection Connection;
    public bool Done;

    // Use this for initialization
    void Start()
    {
        ushort serverPort = 9000;

        NetworkEndPoint endpoint = NetworkEndPoint.LoopbackIpv4;
        endpoint.Port = serverPort;

        Driver = new UdpNetworkDriver(new INetworkParameter[0]);
        Connection = default(NetworkConnection);

        Connection = Driver.Connect(endpoint);
    }

    public void OnDestroy()
    {
        Driver.Dispose();
    }

    // Update is called once per frame
    void Update()
    {
        Driver.ScheduleUpdate().Complete();

        if(!Connection.IsCreated)
        {
            if(!Done)
            {
                Debug.Log("Somthing went wront during connect");

                return;
            }
        }

        DataStreamReader stream;
        NetworkEvent.Type netEvent;

        //  pops events for our single server connections
        while((netEvent = Connection.PopEvent(Driver, out stream)) != NetworkEvent.Type.Empty)
        {
            //  NetworkEvent Connected
            //  Received a ConnectionAccepted message
            if(netEvent == NetworkEvent.Type.Connect)
            {
                Debug.Log("We are now connected to the server");

                uint value = 1;

                using (DataStreamWriter connectionWriter = new DataStreamWriter(4, Allocator.Temp))
                {
                    connectionWriter.Write(value);
                    Connection.Send(Driver, connectionWriter);
                }
            }

            //  NetworkEvent Data
            else if(netEvent == NetworkEvent.Type.Data)
            {
                DataStreamReader.Context readerContext = default(DataStreamReader.Context);
                uint value = stream.ReadUInt(ref readerContext);
                Debug.Log($"Got the value = {value} back from the server");
                Done = true;
                Connection.Disconnect(Driver);
                Connection = default(NetworkConnection);
            }

            //  Network Disconnect
            else if(netEvent == NetworkEvent.Type.Disconnect)
            {
                Debug.Log("Client got disconnected from server");
                Connection = default(NetworkConnection);
            }
        }
    }
}
