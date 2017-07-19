using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mobcast.Coffee.AssetSystem
{
	public class TextObject : ScriptableObject
	{
		public string text;
	}

	public class BytesObject : ScriptableObject
	{
		public byte[] bytes;
	}

	[System.Serializable]
	public class ResourceVersion
	{
		public string comment;
		public string commitHash;
		public long deployTime;
	}


	[System.Serializable]
	public class ResourceVersionList :  ISerializationCallbackReceiver
	{
		public void OnBeforeSerialize(){}

		public void OnAfterDeserialize()
		{
			patchList = patchList.Where(x=>0 < x.deployTime).OrderByDescending(x=>x.deployTime).ToArray();
			leastVersion = patchList.FirstOrDefault();
		}

		public ResourceVersion[] patchList = new ResourceVersion[0];
		[System.NonSerialized] public ResourceVersion leastVersion = new ResourceVersion();
	}
}