using UnityEngine;
using System.Collections;
using Unity.Collections;
using System.Collections.Generic;
using UnityEngine.Assertions;
using Unity.Networking.Transport;

public class FiredBool
{
	public bool HasFired;

	public FiredBool()
	{
		HasFired = false;
	}
}

public class TestProjectileServer : MonoBehaviour
{
	enum StateType
	{
		PlayerMove = 0,
		PlayerFired = 1
	}

	enum ServerDataType
	{
		IdAssignment = 0,
		GameUpdate = 1
	}

	public UdpNetworkDriver Driver;
	private NativeList<NetworkConnection> Connections;
	public GameObject PlayerPreFab;
	public Dictionary<int, GameObject> ClientObjects;
	public Dictionary<int, FiredBool> ClientObjectFired;

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
		ClientObjects = new Dictionary<int, GameObject>();
		ClientObjectFired = new Dictionary<int, FiredBool>();
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

		HandleConnections();

		UpdateClients();
	}

	void CleanConnections()
	{
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

			ClientObjects.Add(IdCount, Instantiate(PlayerPreFab, spawnPos, spawnRot));
			ClientObjectFired.Add(IdCount, new FiredBool());

			IdCount++;
		}
	}

	void HandleConnections()
	{
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

						Debug.Log("Server: Receiving Data");
						Debug.Log("Server: Data size " + dataSize);

						while (dataCount != dataSize)
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

									if (ClientObjects.TryGetValue(clientId, out GameObject Client))
									{
										Debug.Log("Server: Found client object, updating state");

										Vector3 newPosition = new Vector3(posX, posY, posZ);
										Vector3 newRotation = new Vector3(rotX, rotY, rotZ);
										//Vector3 newChildRotation = new Vector3(rotX, 0.0f, 0.0f);

										Client.transform.position = newPosition;
										Client.transform.eulerAngles = newRotation;

										//Client.transform.GetChild(0).gameObject.transform.eulerAngles = newChildRotation;
									}

									//Debug.Log("Server: Player pos_x: " + posX + "Player pos_y: " + posY + "Player pos_z: " + posZ);
									//Debug.Log("Server: Player rot_x: " + rotX + "Player rot_y: " + rotY + "Player rot_z: " + rotZ);
									break;
								case (int)StateType.PlayerFired:
									if (ClientObjectFired.TryGetValue(clientId, out FiredBool firedBool))
									{
										firedBool.HasFired = true;
									}

									Debug.Log("Server: Player fired");
									break;
							}

							dataCount++;
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
						Debug.Log("Server: Client disconnected from server");
						Connections[connection_i] = default(NetworkConnection);
						break;
				}
			}
		}
	}

	void UpdateClients()
	{
		for (int connection_i = 0; connection_i < Connections.Length; connection_i++)
		{
			if (!Connections[connection_i].IsCreated)
			{
				Assert.IsTrue(true);
			}

			using (DataStreamWriter writer = new DataStreamWriter(ClientObjects.Count * 64, Allocator.Temp))
			{
				writer.Write((int)ServerDataType.GameUpdate);
				writer.Write(ClientObjects.Count);
				foreach (KeyValuePair<int, GameObject> kvpClient in ClientObjects)
				{
					Debug.Log($"Server: Player {kvpClient.Key} kvp found");

					writer.Write(kvpClient.Key);
					writer.Write(kvpClient.Value.transform.position.x);
					writer.Write(kvpClient.Value.transform.position.y);
					writer.Write(kvpClient.Value.transform.position.z);
					writer.Write(kvpClient.Value.transform.eulerAngles.x);
					writer.Write(kvpClient.Value.transform.eulerAngles.y);
					writer.Write(kvpClient.Value.transform.eulerAngles.z);

					if (ClientObjectFired.TryGetValue(kvpClient.Key, out FiredBool firedBool))
					{
						Debug.Log($"Server: Player ObjectFired kvp found");

						if (firedBool.HasFired)
						{
							Debug.Log("Server: Player has fired sent");
							writer.Write((uint)1);
							firedBool.HasFired = false;
						}
						else
						{
							Debug.Log("Server: Player has not fired sent");
							writer.Write((uint)0);
						}
					}
					else
					{
						Debug.Log("Server: Player has not fired sent");
						writer.Write((uint)0);
					}

					Driver.Send(NetworkPipeline.Null, Connections[connection_i], writer);
				}

				Debug.Log($"Server: Sent client id: {IdCount}");
			}
		}
	}
}
