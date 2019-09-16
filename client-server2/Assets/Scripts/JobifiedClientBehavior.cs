using UnityEngine;
using UnityEngine.Assertions;
using Unity.Networking.Transport;
using Unity.Collections;
using Unity.Jobs;
using System.Collections;
using System.Collections.Generic;
using System;

struct ClientUpdateJob : IJob
{
    public UdpNetworkDriver driver;
    public NativeArray<NetworkConnection> connection;
    public NativeArray<byte> done;

    public void Execute()
    {
        if (!connection[0].IsCreated)
        {
            if (done[0] != 1)
            {
                Debug.Log("Something went wront during connect");

                return;
            }
        }

        DataStreamReader stream;
        NetworkEvent.Type netEvent;

        //  pops events for our single server connections
        while ((netEvent = connection[0].PopEvent(driver, out stream)) != NetworkEvent.Type.Empty)
        {
            //  NetworkEvent Connected
            //  Received a ConnectionAccepted message
            if (netEvent == NetworkEvent.Type.Connect)
            {
                Debug.Log("We are now connected to the server");

                /*
                uint value = 1;

                using (DataStreamWriter connectionWriter = new DataStreamWriter(4, Allocator.Temp))
                {
                    connectionWriter.Write(value);
                    connection[0].Send(driver, connectionWriter);
                }
                */
            }

            //  NetworkEvent Data
            else if (netEvent == NetworkEvent.Type.Data)
            {
                DataStreamReader.Context readerContext = default(DataStreamReader.Context);

                uint value = stream.ReadUInt(ref readerContext);
                Debug.Log($"Got the value {value} from the server");

                /*
                done[0] = 1;
                connection[0].Disconnect(driver);
                connection[0] = default(NetworkConnection);
                */
            }

            //  Network Disconnect
            else if (netEvent == NetworkEvent.Type.Disconnect)
            {
                Debug.Log("Client got disconnected from server");
                connection[0] = default(NetworkConnection);
            }
        }
    }

}

public class JobifiedClientBehavior : MonoBehaviour
{
	enum packetTypes
	{
		Player_Moved = 0,
		Player_Fire = 1
	}

	public UdpNetworkDriver Driver;
    public NativeArray<NetworkConnection> Connection;
    public NativeArray<byte> Done;

    public JobHandle ClientJobHandle;

    // Use this for initialization
    void Start()
    {
        Driver = new UdpNetworkDriver(new INetworkParameter[0]);
        Connection = new NativeArray<NetworkConnection>(1, Allocator.Persistent);
        Done = new NativeArray<byte>(1, Allocator.Persistent);

        ushort serverPort = 9000;
        NetworkEndPoint endpoint = NetworkEndPoint.LoopbackIpv4;
        endpoint.Port = serverPort;

        Connection[0] = Driver.Connect(endpoint);
    }

    public void OnDestroy()
    {
        ClientJobHandle.Complete();
        Connection.Dispose();
        Driver.Dispose();
        Done.Dispose();
    }

    // Update is called once per frame
    void Update()
    {
        ClientJobHandle.Complete();

		ServerUpdate(packetMessageBuilder());

		ClientUpdateJob job = new ClientUpdateJob
        {
            driver = Driver,
            connection = Connection,
            done = Done
        };

        ClientJobHandle = Driver.ScheduleUpdate();
        ClientJobHandle = job.Schedule(ClientJobHandle);
    }

    void ServerUpdate(List<float> serverMessage)
	{
		if (Connection[0].GetState(Driver) != NetworkConnection.State.Connected)
		{
			return;
		}

		if (serverMessage.Count != 0)
		{
			Debug.Log("Sending packet to server");

			using (DataStreamWriter connectionWriter = new DataStreamWriter(512, Allocator.Temp))
			{
                /*
                connectionWriter.Write(serverMessage.Count);

                foreach(float serializedData in serverMessage)
				{
					connectionWriter.Write(serializedData);
				}
                */

				Connection[0].Send(Driver, connectionWriter);
			}

			Debug.Log("Packet sent to server");
		}
	}

	List<float> packetMessageBuilder()
	{
		List<float> packetMessageSerialized = new List<float>();

		if (transform.hasChanged)
		{
			Debug.Log("Player has moved");

			Vector3 Player_Position = transform.position;
			Quaternion Player_Rotation = transform.rotation;

			packetMessageSerialized.Add((float)0.0);
			packetMessageSerialized.Add((float)Player_Position.x);
			packetMessageSerialized.Add((float)Player_Position.y);
			packetMessageSerialized.Add((float)Player_Position.z);
			packetMessageSerialized.Add((float)Player_Rotation.x);
			packetMessageSerialized.Add((float)Player_Rotation.y);
			packetMessageSerialized.Add((float)Player_Rotation.z);
		}
        if (Input.GetButtonDown("Fire1"))
		{
			Debug.Log("Player fired");
			packetMessageSerialized.Add((float)1.0);
		}

        packetMessageSerialized.Add((float)2.0);

        return packetMessageSerialized;
    }
}
