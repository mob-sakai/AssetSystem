using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using System;

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

		public override string ToString()
		{
			string hash = (commitHash != null && 4 < commitHash.Length)
				? commitHash.Substring(0, 4)
				: commitHash;

			return string.Format("{0:MM/dd hh:mm} {1} {2}",
				DateTime.FromFileTime(deployTime).ToLocalTime(),
				hash,
				comment
			);
		}
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


	[System.Serializable]
	public class BuildManifest
	{
		public static BuildManifest Load()
		{
			var json = Resources.Load<TextAsset>("UnityCloudBuildManifest.json") ?? Resources.Load<TextAsset>("BuildManifest.json");
			if (json == null)
			{
				return new BuildManifest();
			}
			else
			{
				return JsonUtility.FromJson<BuildManifest>(json.text);
			}
		}

		public string scmCommitId;

		public string scmBranch;

		public string buildNumber;

		public string buildStartTime;

		public string projectId;

		public string bundleId;

		public string unityVersion;

		public string xcodeVersion;

		public string cloudBuildTargetName;
	}
}