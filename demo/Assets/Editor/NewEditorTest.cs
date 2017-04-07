﻿using UnityEngine;
using UnityEditor;
using NUnit.Framework;

public class NewEditorTest {

	[Test]
	public void EditorTest() {
		Hash128 hash = new Hash128 (uint.MaxValue, 0, 0, 0);

		Debug.Log (hash.ToString());
		Debug.Log (Hash128.Parse("hogehoge"));
		Debug.Log (Hash128.Parse("////"));
		Debug.Log (Hash128.Parse("...."));
		Debug.Log (Hash128.Parse("http://mobcast.com/hogehoge/12318923498729479374923"));
	}
}
