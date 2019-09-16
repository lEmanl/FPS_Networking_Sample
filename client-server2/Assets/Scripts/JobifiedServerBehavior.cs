using UnityEngine;
using Unity.Networking.Transport;
using Unity.Collections;
using Unity.Jobs;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Assertions;
using System;
using System.Linq;

public struct ClientPlayer
{
    public float PosX, PosY, PosZ, RotX, RotY, RotZ;
	public int PlayerId;
	public byte Connected;

	public ClientPlayer(int id)
	{
		PlayerId = id;
        PosX = 0.0f;
        PosY = 0.0f;
        PosZ = 0.0f;
        RotX = 0.0f;
        RotY = 0.0f;
        RotZ = 0.0f;
        Connected = 1;
	}

    public void SetPosX(float x)
    {
        PosX = x;
    }

    public void SetConnected(byte connected)
    {
        Connected = connected;
    }
}

struct ServerUpdateConnectionsJob : IJob
{
    public UdpNetworkDriver driver;
    public NativeList<NetworkConnection> connections;
	public NativeList<ClientPlayer> players;

    public void Execute()
    {
        //  cleanes up old state connections
        //  before processing new ones
        for (int connection_i = 0; connection_i < connections.Length; connection_i++)
        {
            if (!connections[connection_i].IsCreated)
            {
                connections.RemoveAtSwapBack(connection_i);
                --connection_i;
            }
        }

        //  Accepts the new connections
        NetworkConnection connection;
        while ((connection = driver.Accept()) != default(NetworkConnection))
        {
			connections.Add(connection);
			players.Add(new ClientPlayer(connection.GetHashCode()));
            Debug.Log($"Player Id: {connection.GetHashCode()}");
            Debug.Log("Accepted a connection");
        }
    }
}

struct ServerUpdateJob : IJobParallelFor
{
    public UdpNetworkDriver.Concurrent driver;
    public NativeArray<NetworkConnection> connections;
	public NativeArray<ClientPlayer> players;

    enum packetTypes
    {
        Player_Moved = 0,
        Player_Fire = 1
    }

    int findIndexOfPlayerWithId(int id)
	{
        for(int player_i = 0; player_i < players.Length; player_i++)
		{
            if(players[player_i].PlayerId == id)
			{
				return player_i;
			}
		}

        //  if the player is not found, which is yikes
		return 0;
	}

    public void Execute(int index)
    {
        //  querying for events that might have happened sincd last event
        DataStreamReader stream;

        if (!connections[index].IsCreated)
        {
            Assert.IsTrue(true);
        }

        //  while there are more events needed to process, we will keep popping
        //  events for connection
        NetworkEvent.Type netEvent;
        while ((netEvent = driver.PopEventForConnection(connections[index], out stream)) != NetworkEvent.Type.Empty)
        {
            //  This is where we will process different types of events

            //  Date Event
            if (netEvent == NetworkEvent.Type.Data)
            {
                DataStreamReader.Context readerContext = default(DataStreamReader.Context);
                int dataSize = stream.ReadUShort(ref readerContext) + 1;
                int dataReadCounter = 0;
				float data;

                while( dataReadCounter < dataSize )
				{
                    data = stream.ReadFloat(ref readerContext);

                    dataReadCounter++;

                    switch ((int)data)
					{
						case (int)packetTypes.Player_Moved:
                            Debug.Log("Server got player move");

                            ClientPlayer player = new ClientPlayer(connections[index].InternalId);

                            player.PosX = stream.ReadFloat(ref readerContext);
                            player.PosY = stream.ReadFloat(ref readerContext);
                            player.PosZ = stream.ReadFloat(ref readerContext);
                            player.RotX = stream.ReadFloat(ref readerContext);
                            player.RotY = stream.ReadFloat(ref readerContext);
                            player.RotZ = stream.ReadFloat(ref readerContext);

                            players[findIndexOfPlayerWithId(connections[index].InternalId)] = player;

                            Debug.Log($"Player x: {player.PosX}, Player y: {player.PosY}, Player z: {player.PosZ}");

                            //Debug.Log($"x_pos read: {players[findIndexOfPlayerWithId(connections[index].InternalId)].PosX}, y_pos read: {player.PosY}, y_pos read: {player.PosY}");
                            //Debug.Log($"x_rot read: {player.RotX}, y_rot read: {player.RotY}, y_ros read: {player.RotY}");
                            dataReadCounter += 6;
							break;
						case (int)packetTypes.Player_Fire:
                            Debug.Log("Server got player fire");
                            dataReadCounter++;
                            break;
					}
				}

                Debug.Log("Server end of packet");

                // update player object positions

                /*
                uint number = stream.ReadUInt(ref readerContext);

                Debug.Log("Got " + number + " from the Client.");

                using (DataStreamWriter connectionWriter = new DataStreamWriter(4, Allocator.Temp))
                {
                    connectionWriter.Write(number);
                    driver.Send(NetworkPipeline.Null, connections[index], connectionWriter);
                }
                */
            }

            //  Disconnect Event
            else if (netEvent == NetworkEvent.Type.Disconnect)
            {
                Debug.Log("Client disconnected from server");
                connections[index] = default(NetworkConnection);
            }
        }
    }
}

public class JobifiedServerBehavior : MonoBehaviour
{
    public UdpNetworkDriver Driver;
    public NativeList<NetworkConnection> Connections;
	public NativeList<ClientPlayer> Players;
	public Dictionary<int, GameObject> PlayerObjects;
	public GameObject PlayerPreFab;

	private JobHandle ServerJobHandle;

    // Use this for initialization
    void Start()
    {
        ushort serverPort = 9000;
		int playerCount = 16;

        NetworkEndPoint endpoint = NetworkEndPoint.AnyIpv4;
        endpoint.Port = serverPort;

        Driver = new UdpNetworkDriver(new INetworkParameter[0]);

        if (Driver.Bind(endpoint) != 0)
        {
            Debug.Log($"Failed to bind to port {serverPort}");
        }
        else
        {
            Driver.Listen();
        }

        Connections = new NativeList<NetworkConnection>(playerCount, Allocator.Persistent);
		Players = new NativeList<ClientPlayer>(playerCount, Allocator.Persistent);
        PlayerObjects = new Dictionary<int, GameObject>();
    }

    public void OnDestroy()
    {
        ServerJobHandle.Complete();
        Connections.Dispose();
        Driver.Dispose();
        Players.Dispose();
    }

    // Update is called once per frame
    void Update()
    {
        ServerJobHandle.Complete();

        updatePlayers();

        ServerUpdateConnectionsJob connectionJob = new ServerUpdateConnectionsJob
        {
            driver = Driver,
            connections = Connections,
            players = Players
        };
        
        ServerUpdateJob serverUpdateJob = new ServerUpdateJob
        {
            driver = Driver.ToConcurrent(),
            connections = Connections.AsDeferredJobArray(),
            players = Players
        };

        ServerJobHandle = Driver.ScheduleUpdate();
        ServerJobHandle = connectionJob.Schedule(ServerJobHandle);
        ServerJobHandle = serverUpdateJob.Schedule(Connections, 1, ServerJobHandle);
    }

    void updatePlayers()
	{
        Debug.Log($"Amount of players: {Players.Length}");

		for (int player_i = 0; player_i < Players.Length; player_i++)
		{
            ClientPlayer player = Players[player_i];

            if (!PlayerObjects.ContainsKey(player.PlayerId))
            {
                Vector3 playerPos = new Vector3(player.PosX, player.PosY, player.PosZ);
                Quaternion playerRot = new Quaternion(0.0f, player.RotX, player.RotY, player.RotZ);

                Debug.Log($"Player id: {player.PlayerId}\nPlayer x:{player.PosX}");

                Debug.Log($"Player with id {player.PlayerId} added");
                PlayerObjects.Add(player.PlayerId, Instantiate(PlayerPreFab, playerPos, playerRot));

                //Debug.Log($"Player x: {player.PosX}, Player y: {player.PosY}, Player z: {player.PosZ}");
                Debug.Log($"Player id: {Players[player_i].PlayerId}");
            }
            else
			{
                Debug.Log($"Player already spawned, updating position of id {player.PlayerId}");
                updatePlayerPosition(player);
			}
		}
	}

    void updatePlayerPosition(ClientPlayer player)
	{
        foreach(int key in PlayerObjects.Keys)
        {
            Debug.Log($"Key found: {key}");
        }

        Debug.Log($"Trying Player id: {player.PlayerId}");

        if (PlayerObjects.TryGetValue(player.PlayerId, out GameObject playerObject))
		{
            Debug.Log("Found player by id");
            if(playerObject.transform.hasChanged)
			{
                Debug.Log($"Player x: {player.PosX}, Player y: {player.PosY}, Player z: {player.PosZ}");

                Vector3 playerPos = new Vector3(player.PosX, player.PosY, player.PosZ);
                Quaternion playerRot = new Quaternion(0.0f, player.RotX, player.RotY, player.RotZ);

                playerObject.transform.position = playerPos;
				playerObject.transform.rotation = playerRot;
			}
		}
	}
}
