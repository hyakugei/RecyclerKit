using UnityEngine;
using System.Collections;
using System.Collections.Generic;



public partial class TrashMan : MonoBehaviour
{
	/// <summary>
	/// access to the singleton
	/// </summary>
	public static TrashMan instance;

	/// <summary>
	/// stores the recycle bins and is used to populate the Dictionaries at startup
	/// </summary>
	public List<TrashManRecycleBin> recycleBinCollection;

	/// <summary>
	/// uses the GameObject instanceId as its key for fast look-ups
	/// </summary>
	private static Dictionary<int,TrashManRecycleBin> _instanceIdToRecycleBin = new Dictionary<int,TrashManRecycleBin>();

	/// <summary>
	/// uses the pool name to find the GameObject instanceId
	/// </summary>
	private static Dictionary<string,int> _poolNameToInstanceId = new Dictionary<string,int>();
	
	private static Dictionary<TrashManRecycleBin, TrashMan> _binToTrashManInstance = new Dictionary<TrashManRecycleBin, TrashMan>();
	

	[HideInInspector]
	public new Transform transform;


	#region MonoBehaviour

	private void Awake()
	{
		if( instance != null )
		{
			//Destroy( gameObject );
		}
		else
		{
			transform = gameObject.transform;
			instance = this;
		}
		
		initializePrefabPools();
		StartCoroutine( cullExcessObjects() );
	}


	// TODO: perhaps make this configurable per pool then add DontDestroyOnLoad. Currently this does nothing.
//	private void OnLevelWasLoaded()
//	{}


	private void OnApplicationQuit()
	{
		instance = null;
	}
	
	
	private void OnDestroy()
	{
		// i need to get all the recycle bins associated with this TrashMan instance. 
		// ah, thats just the recyclebincoll!
		for(int i = recycleBinCollection.Count; i > 0; i--)
		{
			if(recycleBinCollection[i-1].prefab == null) continue;
			TrashMan.removeRecycleBin(recycleBinCollection[i-1].prefab);
		}
		
		if(TrashMan.instance == this)
		{
			// we have to reassign!
			TrashMan[] tm = GameObject.FindObjectsOfType<TrashMan>();
			foreach(var ins in tm)
			{
				if(ins != this)
				{
					Debug.Log("Reassigning TrashMan.instance...", ins.gameObject);
					TrashMan.instance = ins;
					TrashMan.instance.transform = ins.gameObject.transform;
					break;
				}
			}
		}
	}
	
	#endregion


	#region Private

	/// <summary>
	/// coroutine that runs every couple seconds and removes any objects created over the recycle bins limit
	/// </summary>
	/// <returns>The excess objects.</returns>
	private IEnumerator cullExcessObjects()
	{
		var waiter = new WaitForSeconds( 5f );

		while( true )
		{
			for( var i = 0; i < recycleBinCollection.Count; i++ )
				recycleBinCollection[i].cullExcessObjects();

			yield return waiter;
		}
	}


	/// <summary>
	/// populats the lookup dictionaries
	/// </summary>
	private void initializePrefabPools()
	{
		if( recycleBinCollection == null )
			return;

		foreach( var recycleBin in recycleBinCollection )
		{
			if( recycleBin == null || recycleBin.prefab == null )
				continue;

			recycleBin.initialize(this.gameObject.transform);
			_binToTrashManInstance.Add(recycleBin, this);
			_instanceIdToRecycleBin.Add( recycleBin.prefab.GetInstanceID(), recycleBin );
			_poolNameToInstanceId.Add( recycleBin.prefab.name, recycleBin.prefab.GetInstanceID() );
		}
	}


	/// <summary>
	/// internal method that actually does the work of grabbing the item from the bin and returning it
	/// </summary>
	/// <param name="gameObjectInstanceId">Game object instance identifier.</param>
	private static GameObject spawn( int gameObjectInstanceId, Vector3 position, Quaternion rotation )
	{
		if( _instanceIdToRecycleBin.ContainsKey( gameObjectInstanceId ) )
		{
			var newGo = _instanceIdToRecycleBin[gameObjectInstanceId].spawn();

			if( newGo != null )
			{
				var newTransform = newGo.transform;
				newTransform.parent = null;
				newTransform.position = position;
				newTransform.rotation = rotation;
			}

			return newGo;
		}

		return null;
	}


	/// <summary>
	/// internal coroutine for despawning after a delay
	/// </summary>
	/// <returns>The despawn after delay.</returns>
	/// <param name="go">Go.</param>
	/// <param name="delayInSeconds">Delay in seconds.</param>
	private IEnumerator internalDespawnAfterDelay( GameObject go, float delayInSeconds )
	{
		yield return new WaitForSeconds( delayInSeconds );
		despawn( go );
	}

	#endregion


	#region Public

	public static void manageRecycleBin( TrashManRecycleBin recycleBin )
	{
		// make sure we can safely add the bin!
		if( _poolNameToInstanceId.ContainsKey( recycleBin.prefab.name ) )
		{
			Debug.LogError( "Cannot manage the recycle bin because there is already a GameObject with the name (" + recycleBin.prefab.name + ") being managed" );
			return;
		}

		instance.recycleBinCollection.Add( recycleBin );
		recycleBin.initialize(instance.transform);
		_binToTrashManInstance.Add(recycleBin, instance);
		_instanceIdToRecycleBin.Add( recycleBin.prefab.GetInstanceID(), recycleBin );
		_poolNameToInstanceId.Add( recycleBin.prefab.name, recycleBin.prefab.GetInstanceID() );
	}

	public static void removeRecycleBin(GameObject prefab)
	{
		if(_poolNameToInstanceId.ContainsKey(prefab.name) == false)
		{
			Debug.LogError("No recycle bin for prefab named \"" + prefab.name + "\" to remove.");
			return;
		}
		
		TrashManRecycleBin bin = _instanceIdToRecycleBin[prefab.GetInstanceID()];
		TrashMan inst = _binToTrashManInstance[bin];
		
		inst.recycleBinCollection.Remove(bin);
		_instanceIdToRecycleBin.Remove(prefab.GetInstanceID());
		_poolNameToInstanceId.Remove(prefab.name);
		
		bin.removeAllPooledObjects();
	}

	/// <summary>
	/// pulls an object out of the recycle bin
	/// </summary>
	/// <param name="go">Go.</param>
	public static GameObject spawn( GameObject go, Vector3 position = default( Vector3 ), Quaternion rotation = default( Quaternion ) )
	{
		if( _instanceIdToRecycleBin.ContainsKey( go.GetInstanceID() ) )
		{
			return spawn( go.GetInstanceID(), position, rotation );
		}
		else
		{
			Debug.LogError( "attempted to spawn go (" + go.name + ") but there is no recycle bin setup for it. Falling back to Instantiate" );
			var newGo = GameObject.Instantiate( go, position, rotation ) as GameObject;
			newGo.transform.parent = null;
			
			return newGo;
		}
	}

	
	/// <summary>
	/// pulls an object out of the recycle bin using the bin's name
	/// </summary>
	public static GameObject spawn( string gameObjectName, Vector3 position = default( Vector3 ), Quaternion rotation = default( Quaternion ) )
	{
		int instanceId = -1;
		if( _poolNameToInstanceId.TryGetValue( gameObjectName, out instanceId ) )
		{
			return spawn( instanceId, position, rotation );
		}
		else
		{
			Debug.LogError( "attempted to spawn a GameObject from recycle bin (" + gameObjectName + ") but there is no recycle bin setup for it" );
			return null;
		}
	}
	
	
	/// <summary>
	/// sticks the GameObject back into it's recycle bin. If the GameObject has no bin it is destroyed.
	/// </summary>
	/// <param name="go">Go.</param>
	public static void despawn( GameObject go )
	{	
		if( go == null )
			return;

		var goName = go.name;
		if( !_poolNameToInstanceId.ContainsKey( goName ) )
		{
			Destroy( go );
		}
		else
		{
			var bin = _instanceIdToRecycleBin[_poolNameToInstanceId[goName]];
			bin.despawn(go);
			go.transform.parent = bin.parentTransform;
		}
	}
	
	
	/// <summary>
	/// sticks the GameObject back into it's recycle bin after a delay. If the GameObject has no bin it is destroyed.
	/// </summary>
	/// <param name="go">Go.</param>
	public static void despawnAfterDelay( GameObject go, float delayInSeconds )
	{	
		if( go == null )
			return;
		
		instance.StartCoroutine( instance.internalDespawnAfterDelay( go, delayInSeconds ) );
	}


	/// <summary>
	/// gets the recycle bin for the given GameObject name. Returns null if none exists.
	/// </summary>
	public static TrashManRecycleBin recycleBinForGameObjectName( string gameObjectName )
	{
		if( _poolNameToInstanceId.ContainsKey( gameObjectName ) )
		{
			var instanceId = _poolNameToInstanceId[gameObjectName];
			return _instanceIdToRecycleBin[instanceId];
		}
		return null;
	}


	/// <summary>
	/// gets the recycle bin for the given GameObject. Returns null if none exists.
	/// </summary>
	/// <returns>The bin for game object.</returns>
	/// <param name="go">Go.</param>
	public static TrashManRecycleBin recycleBinForGameObject( GameObject go )
	{
		if( _instanceIdToRecycleBin.ContainsKey( go.GetInstanceID() ) )
			return _instanceIdToRecycleBin[go.GetInstanceID()];
		return null;
	}


	#endregion

}
