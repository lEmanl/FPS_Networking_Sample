using UnityEngine;
using System.Collections;
using Unity.Networking.Transport;
using Unity.Collections;
using Unity.Jobs;
using System.Collections.Generic;

public class ServerClient : MonoBehaviour
{
	public UdpNetworkDriver Driver;
	public NetworkConnection Connection;
	public bool Done;

	public ushort ServerPort = 9000;

    void Start()
    {
		Driver = new UdpNetworkDriver(new INetworkParameter[0]);
		Connection = default(NetworkConnection);

		NetworkEndPoint endpoint = NetworkEndPoint.LoopbackIpv4;
		endpoint.Port = ServerPort;

		Connection = Driver.Connect(endpoint);
    }

    public void OnDestroy()
	{
		Driver.Dispose();
	}

    void Update()
    {
		Driver.ScheduleUpdate().Complete();

		if(CheckConnection())
		{
            if (Connected && IdAssigned)
            {
                Debug.Log("Client: Sending player state");
                SendPlayerState();
            }

            Debug.Log("Client: Handling connection");
            HandleConnection();
        }
        else
		{
			return;
		}
    }

    bool CheckConnection()
	{
		if (!Connection.IsCreated)
		{
			if (!Done)
			{
				Debug.Log("Client: Something went wrong during connect");

				return false;
			}
            return false;
        }
		return true;
	}

    void SendPlayerState()
	{
        List<float> playerState = BuildPlayerState();

        if(playerState.Count == 0)
        {
            Debug.Log("Client: No change in player state");
            return;
        }

		using (DataStreamWriter connectionWriter = new DataStreamWriter(playerState.Count*16, Allocator.Temp))
		{
            //Debug.Log("Client: Player state count " + playerState.Count);
            connectionWriter.Write(ClientId);
            connectionWriter.Write(playerState.Count);

            foreach(float data in playerState)
            {
                connectionWriter.Write(data);
            }

			Connection.Send(Driver, connectionWriter);
		}
	}

    void HandleConnection()
    {
        NetworkEvent.Type netEvent;

        //  pops events for our single server connections
        while ((netEvent = Connection.PopEvent(Driver, out DataStreamReader stream)) != NetworkEvent.Type.Empty)
        {
            switch (netEvent)
            {
                case NetworkEvent.Type.Connect:
                    Debug.Log("Client: We are now connected to the server");
                    Connected = true;
                    break;
                case NetworkEvent.Type.Data:
                    DataStreamReader.Context readerContext = default(DataStreamReader.Context);
                    int DataType = stream.ReadInt(ref readerContext);

                    

                    break;
                case NetworkEvent.Type.Disconnect:
                    Debug.Log("Client: Client got disconnected from server");
                    Connection = default(NetworkConnection);
                    Connected = false;
                    break;
            }
        }
    }
}
