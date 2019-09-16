using UnityEngine;
using System.Collections;
using Unity.Networking.Transport;
using Unity.Collections;
using Unity.Jobs;
using System.Collections.Generic;

public class TestProjectileClient : MonoBehaviour
{
	enum StateType
	{
		PlayerMove = 0,
		PlayerFired = 1
	}

	enum ServerDataType
	{
		IdAssignment = 0,
		GameUpdate = 1,
	}

	public UdpNetworkDriver Driver;
	public NetworkConnection Connection;
	public int ClientId;
	public bool Done;
	public bool Connected, IdAssigned;
	GameObject ChildFirstPersonCharacter;
	public GameObject PlayerPreFab;
	public Dictionary<int, GameObject> ClientObjects;
	public GameObject ProjectilePreFab;


	ushort ServerPort = 9000;
	//string Ip = "";

	// Use this for initialization
	void Start()
	{
		Driver = new UdpNetworkDriver(new INetworkParameter[0]);
		Connection = default(NetworkConnection);

		NetworkEndPoint endpoint = NetworkEndPoint.LoopbackIpv4;
		endpoint.Port = ServerPort;

		Connection = Driver.Connect(endpoint);
		ChildFirstPersonCharacter = gameObject.transform.GetChild(0).gameObject;
		ClientObjects = new Dictionary<int, GameObject>();
	}

	public void OnDestroy()
	{
		Driver.Dispose();
	}

	// Update is called once per frame
	void Update()
	{
		Driver.ScheduleUpdate().Complete();

		if (CheckConnection())
		{
			Debug.Log("Client: Handling connection");
			HandleConnection();

			if (Connected && IdAssigned)
			{
				Debug.Log("Client: Sending player state");
				SendPlayerState();
			}
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

		if (playerState.Count == 0)
		{
			Debug.Log("Client: No change in player state");
			return;
		}

		using (DataStreamWriter connectionWriter = new DataStreamWriter(playerState.Count * 16, Allocator.Temp))
		{
			Debug.Log("Client: Player state count " + playerState.Count);
			connectionWriter.Write(ClientId);
			connectionWriter.Write(playerState.Count);

			foreach (float data in playerState)
			{
				connectionWriter.Write(data);
			}

			Connection.Send(Driver, connectionWriter);
		}
	}

	List<float> BuildPlayerState()
	{
		List<float> playerState = new List<float>();

		if (transform.hasChanged)
		{
			playerState.Add((int)StateType.PlayerMove);
			playerState.Add(transform.position.x);
			playerState.Add(transform.position.y);
			playerState.Add(transform.position.z);
			playerState.Add(ChildFirstPersonCharacter.transform.rotation.eulerAngles.x);
			playerState.Add(transform.rotation.eulerAngles.y);
			playerState.Add(transform.rotation.eulerAngles.z);
		}

		if (Input.GetButtonDown("Fire1"))
		{
			playerState.Add((int)StateType.PlayerFired);
		}

		return playerState;
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

					switch (DataType)
					{
						case (int)ServerDataType.IdAssignment:
							ClientId = stream.ReadInt(ref readerContext);
							Debug.Log($"Client: Got the client id {ClientId}");
							IdAssigned = true;
							break;
						case (int)ServerDataType.GameUpdate:
							int clientObjectSize = stream.ReadInt(ref readerContext);
							int clientObjectCount = 0;
							int clientObjectId;

							while (clientObjectCount < clientObjectSize)
							{
								clientObjectId = stream.ReadInt(ref readerContext);
								if (clientObjectId != ClientId && !ClientObjects.ContainsKey(clientObjectId))
								{
									float posX = stream.ReadFloat(ref readerContext);
									float posY = stream.ReadFloat(ref readerContext);
									float posZ = stream.ReadFloat(ref readerContext);
									float rotX = stream.ReadFloat(ref readerContext);
									float rotY = stream.ReadFloat(ref readerContext);
									float rotZ = stream.ReadFloat(ref readerContext);
									uint hasFired = stream.ReadUInt(ref readerContext);

									Vector3 spawnPosition = new Vector3(posX, posY, posZ);
									Quaternion spawnRotation = new Quaternion(rotX, rotY, rotZ, 0.0f);

									ClientObjects.Add(clientObjectId, Instantiate(PlayerPreFab, spawnPosition, spawnRotation));
								}
								else
								{
									float posX = stream.ReadFloat(ref readerContext);
									float posY = stream.ReadFloat(ref readerContext);
									float posZ = stream.ReadFloat(ref readerContext);
									float rotX = stream.ReadFloat(ref readerContext);
									float rotY = stream.ReadFloat(ref readerContext);
									float rotZ = stream.ReadFloat(ref readerContext);
									uint hasFired = stream.ReadUInt(ref readerContext);

									Debug.Log($"Player has fired: {hasFired}");

									if (ClientObjects.TryGetValue(clientObjectId, out GameObject clientObject))
									{
										Vector3 newPosition = new Vector3(posX, posY, posZ);
										Vector3 newRotation = new Vector3(rotX, rotY, rotZ);

										clientObject.transform.position = newPosition;
										clientObject.transform.eulerAngles = newRotation;

										if (hasFired == 1)
										{
											GameObject projectile = Instantiate(ProjectilePreFab, clientObject.transform.position, clientObject.transform.rotation);

											projectile.GetComponent<Rigidbody>().AddForce(transform.forward * 2500);
										}
									}
								}

								clientObjectCount++;
							}

							break;
					}

					break;
				case NetworkEvent.Type.Disconnect:
					Debug.Log("Client: Client got disconnected from server");
					Connection = default(NetworkConnection);
					Connected = false;
					break;
			}
		}
		/*
                Done = 1;
                Connection.Disconnect(Driver);
                Connection = default(NetworkConnection);
                */
		/*
        uint value = 1;

        using (DataStreamWriter connectionWriter = new DataStreamWriter(4, Allocator.Temp))
        {
            connectionWriter.Write(value);
            Connection.Send(Driver, connectionWriter);
        }
        */
	}
}
