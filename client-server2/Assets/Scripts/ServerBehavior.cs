using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEngine;
using UnityEngine.Assertions;
using Unity.Networking.Transport;
using Unity.Collections;

public class ServerBehavior : MonoBehaviour
{ 
    public UdpNetworkDriver Driver;
    private NativeList<NetworkConnection> Connections;

    // Start is called before the first frame update
    void Start()
    {
        ushort serverPort = 9000;

        NetworkEndPoint endpoint = NetworkEndPoint.AnyIpv4;
        endpoint.Port = serverPort;

        Driver = new UdpNetworkDriver(new INetworkParameter[0]);

        if(Driver.Bind(endpoint) != 0)
        {
            Debug.Log($"Failed to bind to port {serverPort}");
        }
        else
        {
            Driver.Listen();
        }

        Connections = new NativeList<NetworkConnection>(16, Allocator.Persistent);
    }

    public void OnDestroy()
    {
        Driver.Dispose();
        Connections.Dispose();
    }

    // Update is called once per frame
    void Update()
    {
        Driver.ScheduleUpdate().Complete();

        //  cleanes up old state connections
        //  before processing new ones
        for(int connection_i = 0; connection_i < Connections.Length; connection_i++)
        {
            if (!Connections[connection_i].IsCreated)
            {
                Connections.RemoveAtSwapBack(connection_i);
                --connection_i;
            }
        }

        //  Accepts the new connections
        NetworkConnection connection;
        while((connection = Driver.Accept()) != default(NetworkConnection))
        {
            Connections.Add(connection);
            Debug.Log("Accepted a connection");
        }

        //  Connections are now up to date, so now we are
        //  querying for events that might have happened sincd last event
        DataStreamReader stream;
        for (int connection_i = 0; connection_i < Connections.Length; connection_i++)
        {
            if(!Connections[connection_i].IsCreated)
            {
                Assert.IsTrue(true);
            }

            //  while there are more events needed to process, we will keep popping
            //  events for connection
            NetworkEvent.Type netEvent;
            while((netEvent = Driver.PopEventForConnection(Connections[connection_i], out stream)) != NetworkEvent.Type.Empty)
            {
                //  This is where we will process different types of events

                //  Date Event
                if(netEvent == NetworkEvent.Type.Data)
                {
                    DataStreamReader.Context readerContext = default(DataStreamReader.Context);
                    uint number = stream.ReadUInt(ref readerContext);

                    Debug.Log("Got " + number + " from the Client adding + 2 to it.");
                    number += 2;

                    using (DataStreamWriter connectionWriter = new DataStreamWriter(4, Allocator.Temp))
                    {
                        connectionWriter.Write(number);
                        Driver.Send(NetworkPipeline.Null, Connections[connection_i], connectionWriter);
                    }
                }

                //  Disconnect Event
                else if(netEvent == NetworkEvent.Type.Disconnect)
                {
                    Debug.Log("Client disconnected from server");
                    Connections[connection_i] = default(NetworkConnection);
                }
            }
        }
    }
}
