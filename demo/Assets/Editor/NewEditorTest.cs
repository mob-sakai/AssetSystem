using UnityEngine;
using UnityEditor;
using NUnit.Framework;

public class NewEditorTest {

	[Test]
	public void EditorTest() {
		Hash128 hash = new Hash128 (uint.MaxValue, 0, 0, 0);

		Debug.Log (hash.ToString());
		Debug.Log (Hash128.Parse("0cbe0f6edd536caea5cf5651cd4bf922"));
		Debug.Log (Hash128.Parse("0cbe0f6edd536caea5cf5651cd4bf922cb9110ac"));
		Debug.Log (Hash128.Parse("0cbe0f"));
	}
}
