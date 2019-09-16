using UnityEngine;
using System.Collections;
using Unity.Collections;
using System.Collections.Generic;
using UnityEngine.Assertions;
using Unity.Networking.Transport;

public class ClientState
{
    public int Health;
    public bool HasFired;
    public Vector3 PlayerPosition;
    public Quaternion PlayerRotation;

    public ClientState()
    {
        HasFired = false;
    }
}

public class ServerHost : MonoBehaviour
{
    enum StateType
    {
        PlayerMove = 0,
        PlayerFired = 1,
        PlayerHurt = 2
    }

    enum ServerDataType
    {
        IdAssignment = 0,
        GameUpdate = 1
    }

	public UdpNetworkDriver Driver;
	private NativeList<NetworkConnection> Connections;
    public GameObject PlayerPreFab;
    public Dictionary<int, ClientState> ClientStates;

    ushort ServerPort = 9000;
    //string Ip = "";

    int MaxClientCount = 16;
    int IdCount = 0;

    // Use this for initialization
    void Start()
    {
		NetworkEndPoint endpoint = NetworkEndPoint.AnyIpv4;
        endpoint.Port = ServerPort;

		Driver = new UdpNetworkDriver(new INetworkParameter[0]);

		if (Driver.Bind(endpoint) != 0)
		{
			Debug.Log($"Server: Failed to bind to port {ServerPort}");
		}
		else
		{
			Driver.Listen();
		}

        Connections = new NativeList<NetworkConnection>(MaxClientCount, Allocator.Persistent);
        ClientStates = new Dictionary<int, ClientState>();
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

		CleanConnections();

		AcceptConnections();

        UpdateClients();

        HandleConnections();
    }

    void CleanConnections()
	{
        Debug.Log($"Server: Cleaning Connections");

        //  cleanes up old state Connections
        //  before processing new ones
        for (int connection_i = 0; connection_i < Connections.Length; connection_i++)
		{
			if (!Connections[connection_i].IsCreated)
			{
				Connections.RemoveAtSwapBack(connection_i);
				--connection_i;
			}
		}
	}

    void AcceptConnections()
	{
        Debug.Log($"Server: Accepting Connections");

        //  Accepts the new Connections
        NetworkConnection connection;
		while ((connection = Driver.Accept()) != default(NetworkConnection))
		{
			Connections.Add(connection);
			//players.Add(new ClientPlayer(connection.GetHashCode()));
			//Debug.Log($"Player Id: {connection.GetHashCode()}");
			Debug.Log("Server: Accepted a connection");

            using (DataStreamWriter writer = new DataStreamWriter(16, Allocator.Temp))
            {
                writer.Write((int)ServerDataType.IdAssignment);
                writer.Write(IdCount);
                Driver.Send(NetworkPipeline.Null, connection, writer);

                Debug.Log($"Server: Sent client id: {IdCount}");
            }

            Vector3 spawnPos = new Vector3(0.0f, 4.0f, 0.0f);
            Quaternion spawnRot = new Quaternion(0.0f, 0.0f, 0.0f, 0.0f);

            ClientState newState = new ClientState();
            newState.PlayerPosition = spawnPos;
            newState.PlayerRotation = spawnRot;
            newState.Health = 10;

            ClientStates.Add(IdCount, newState);

            IdCount++;
		}
	}

	void HandleConnections()
	{
        Debug.Log($"Server: Handling Connections");

        //  querying for events that might have happened sincd last event
        DataStreamReader stream;

		for (int connection_i = 0; connection_i < Connections.Length; connection_i++)
		{
			if (!Connections[connection_i].IsCreated)
			{
				Assert.IsTrue(true);
			}

			//  while there are more events needed to process, we will keep popping
			//  events for connection
			NetworkEvent.Type netEvent;
			while ((netEvent = Driver.PopEventForConnection(Connections[connection_i], out stream)) != NetworkEvent.Type.Empty)
			{
                //  This is where we will process different types of events

                //  Date Event
                switch (netEvent)
                {
                    case NetworkEvent.Type.Data:
                        DataStreamReader.Context readerContext = default(DataStreamReader.Context);
                        int clientId = stream.ReadInt(ref readerContext);
                        int dataSize = stream.ReadInt(ref readerContext);
                        int dataCount = 0;

                        //Debug.Log("Server: Receiving Data");
                        //Debug.Log("Server: Data size " + dataSize);

                        while(dataCount < dataSize)
                        {
                            int stateType = (int)stream.ReadFloat(ref readerContext);

                            switch (stateType)
                            {
                                case (int)StateType.PlayerMove:
                                    float posX = stream.ReadFloat(ref readerContext);
                                    float posY = stream.ReadFloat(ref readerContext);
                                    float posZ = stream.ReadFloat(ref readerContext);
                                    float rotX = stream.ReadFloat(ref readerContext);
                                    float rotY = stream.ReadFloat(ref readerContext);
                                    float rotZ = stream.ReadFloat(ref readerContext);

                                    if (ClientStates.TryGetValue(clientId, out ClientState client))
                                    {
                                        Vector3 newPosition = new Vector3(posX, posY, posZ);
                                        Vector3 newRotation = new Vector3(rotX, rotY, rotZ);
                                        //Vector3 newChildRotation = new Vector3(rotX, 0.0f, 0.0f);

                                        client.PlayerPosition = newPosition;
                                        client.PlayerRotation.eulerAngles = newRotation;

                                        //Client.transform.GetChild(0).gameObject.transform.eulerAngles = newChildRotation;
                                    }

                                    //Debug.Log("Server: Player pos_x: " + posX + "Player pos_y: " + posY + "Player pos_z: " + posZ);
                                    //Debug.Log("Server: Player rot_x: " + rotX + "Player rot_y: " + rotY + "Player rot_z: " + rotZ);

                                    dataCount += 7;
                                    break;
                                case (int)StateType.PlayerFired:
                                    if (ClientStates.TryGetValue(clientId, out ClientState clientFired))
                                    {
                                        clientFired.HasFired = true;
                                    }

                                    Debug.Log("Server: Player fired");

                                    dataCount++;
                                    break;
                            }
                        }
                        /*
					    using (DataStreamWriter connectionWriter = new DataStreamWriter(4, Allocator.Temp))
					    {
						    connectionWriter.Write(number);
						    Driver.Send(NetworkPipeline.Null, Connections[connection_i], connectionWriter);
					    }
                        */
                        break;
                    case NetworkEvent.Type.Disconnect:
                        //Debug.Log("Server: Client disconnected from server");
                        Connections[connection_i] = default(NetworkConnection);
                        break;
                }
            }
		}
    }

    void UpdateClients()
    {
        Debug.Log($"Server: Updating clients");

        if (ClientStates.Count == 0)
        {
            return;
        }

        for (int connection_i = 0; connection_i < Connections.Length; connection_i++)
        {
            if (!Connections[connection_i].IsCreated)
            {
                Assert.IsTrue(true);
                //return;
            }

            using (DataStreamWriter writer = new DataStreamWriter(ClientStates.Count*512, Allocator.Temp))
            {
                writer.Write((int)ServerDataType.GameUpdate);
                writer.Write(ClientStates.Count);
                Debug.Log($"Client State Count: {ClientStates.Count}");
                int kvpCount = 0;
                foreach( KeyValuePair<int, ClientState> kvpClient in ClientStates)
                {
                    kvpCount++;
                    writer.Write(kvpClient.Key);
                    writer.Write(kvpClient.Value.PlayerPosition.x);
                    writer.Write(kvpClient.Value.PlayerPosition.y);
                    writer.Write(kvpClient.Value.PlayerPosition.z);
                    writer.Write(kvpClient.Value.PlayerRotation.eulerAngles.x);
                    writer.Write(kvpClient.Value.PlayerRotation.eulerAngles.y);
                    writer.Write(kvpClient.Value.PlayerRotation.eulerAngles.z);

                    if (kvpClient.Value.HasFired)
                    {
                        Debug.Log("Server Player has fired sent");
                        writer.Write((uint)1);
                        kvpClient.Value.HasFired = false;
                    }
                    else
                    {
                        writer.Write((uint)0);
                    }

                    Driver.Send(NetworkPipeline.Null, Connections[connection_i], writer);
                }
                Debug.Log($"Client kvp count: {kvpCount}");
            }
        }
    }
}
