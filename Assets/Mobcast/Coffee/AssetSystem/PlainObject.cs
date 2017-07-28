using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Mobcast.Coffee.AssetSystem
{
	public class PlainObject : ScriptableObject
	{
		public static PlainObject Create(byte[] bytes)
		{
			var asset = ScriptableObject.CreateInstance<PlainObject>();
			asset.bytes = bytes;
			return asset;
		}

		public byte[] bytes { get; private set; }

		public string text { get { return m_Text ?? (m_Text = Encoding.UTF8.GetString(bytes)); } }

		string m_Text;

	}

	[System.Serializable]
	public struct Patch
	{
		public string comment;
		public string commitHash;
		public long deployTime;
	}


	[System.Serializable]
	public class PatchList :  ISerializationCallbackReceiver
	{
		public void OnBeforeSerialize()
		{
		}

		public void OnAfterDeserialize()
		{
			patchList = patchList
				.Where(x => 0 < x.deployTime)
				.OrderByDescending(x => x.deployTime)
				.ToArray();
			
			leatestPatch = patchList.FirstOrDefault();
		}

		public Patch[] patchList = new Patch[0];
		[System.NonSerialized] public Patch leatestPatch;
	}
}