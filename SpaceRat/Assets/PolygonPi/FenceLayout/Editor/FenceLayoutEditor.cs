using System.Collections.Generic;
using UnityEditor;
#if UNITY_2019_1_OR_NEWER && !UNITY_2021_2_OR_NEWER
using UnityEditor.Experimental.SceneManagement;
#endif
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[CustomEditor(typeof(FenceLayout)), CanEditMultipleObjects]
public class FenceLayoutEditor : Editor
{
	private FenceLayout m_fenceLayout;
	private Terrain[] m_terrains;

	private FenceLayout.EditMode m_editMode = FenceLayout.EditMode.None;
	private Vector3 m_mousePos;

	private bool ShowRaycastSettings = false;
	private bool ShowPoints = false;

	// --------------------------------------------------------------------------

	protected virtual void OnSceneGUI()
	{
		// Snapshot before handles/transform changes so they can be undone.
		Undo.RecordObject(m_fenceLayout, "Edit Fence Points");
		EditorGUI.BeginChangeCheck();

		bool changed = false;

		for (int i = 0; i < m_fenceLayout.FencePoints.Count; i++)
		{
			Vector3 worldPos = m_fenceLayout.FencePoints[i] + m_fenceLayout.transform.position;

			Handles.color = Color.white;
			Vector3 labelPos = worldPos;
			labelPos.y -= 1.0f;
			Handles.Label(labelPos, "Point " + i);

			if (m_editMode != FenceLayout.EditMode.Delete)
			{
				Vector3 newPos = Handles.PositionHandle(worldPos, Quaternion.identity);

				if (newPos != worldPos)
				{
					newPos.y = SampleTerrainHeight(newPos);
					m_fenceLayout.FencePoints[i] = newPos - m_fenceLayout.transform.position;
				}
			}
		}

		if (m_fenceLayout.transform.hasChanged)
		{
			// Move all points onto the ground.
			for (int i = 0; i < m_fenceLayout.FencePoints.Count; i++)
			{
				Vector3 worldPos = m_fenceLayout.FencePoints[i] + m_fenceLayout.transform.position;
				worldPos.y = SampleTerrainHeight(worldPos);
				m_fenceLayout.FencePoints[i] = worldPos - m_fenceLayout.transform.position;
			}

			changed = true;
			m_fenceLayout.transform.hasChanged = false;
		}

		changed |= EditorGUI.EndChangeCheck();
		if (changed)
		{
			EditorUtility.SetDirty(m_fenceLayout);
			CreateFences();
		}
	}

	// --------------------------------------------------------------------------

	public override void OnInspectorGUI()
	{
		// Unity only creates an undo entry if a field actually changes.
		Undo.RecordObject(m_fenceLayout, "Edit Fence Layout");
		EditorGUI.BeginChangeCheck();

		GUIStyle italic = new GUIStyle(GUI.skin.label);
		italic.fontStyle = FontStyle.Italic;
		GUILayout.Label("Ctrl-Click to add points", italic);
		GUILayout.Label("Ctrl-Shift-Click to remove points", italic);

		GUILayout.BeginVertical();
		int numPrefabs = EditorGUILayout.IntField("Num fence prefabs", m_fenceLayout.FencePrefabs.Length);
		numPrefabs = Mathf.Clamp(numPrefabs, 0, 50);

		if (numPrefabs != m_fenceLayout.FencePrefabs.Length)
		{
			System.Array.Resize(ref m_fenceLayout.FencePrefabs, numPrefabs);
		}

		for (int i = 0; i < m_fenceLayout.FencePrefabs.Length; i++)
		{
			m_fenceLayout.FencePrefabs[i] = EditorGUILayout.ObjectField("Fence prefab " + i, m_fenceLayout.FencePrefabs[i], typeof(Transform), true) as Transform;
		}

		if (!m_fenceLayout.ObjectMode)
		{
			m_fenceLayout.PostPrefab = EditorGUILayout.ObjectField("End post prefab", m_fenceLayout.PostPrefab, typeof(Transform), true) as Transform;
		}
		GUILayout.EndVertical();

		if (!m_fenceLayout.ObjectMode)
		{
			m_fenceLayout.FenceEndOffset = EditorGUILayout.Vector3Field("End offset", m_fenceLayout.FenceEndOffset);

			using (new EditorGUI.DisabledScope(m_fenceLayout.FencePrefabs.Length == 0 || m_fenceLayout.FencePrefabs[0] == null))
			{
				GUIContent calcLabel = new GUIContent("Calculate length & end offset from prefabs",
					"Measures 'Fence prefab 0' along Z (after Fence rotation).\n" +
					"With an end post: the panel is assumed to contain its own start post, so length = panel depth - post depth " +
					"and End offset Z centres that post on the layout point.\n" +
					"Without an end post: length = panel depth and End offset Z puts the panel's start edge on the point.\n" +
					"End offset X/Y are left unchanged.");
				if (GUILayout.Button(calcLabel))
				{
					CalculateFromPrefabs();
					GUI.changed = true; // trigger rebuild + undo via the change check below
				}
			}
		}
		m_fenceLayout.FenceRotation = EditorGUILayout.FloatField("Fence rotation", m_fenceLayout.FenceRotation);
		m_fenceLayout.FenceLength = EditorGUILayout.FloatField("Fence length", m_fenceLayout.FenceLength);
		m_fenceLayout.FenceScale = EditorGUILayout.Vector3Field("Fence scale", m_fenceLayout.FenceScale);
		m_fenceLayout.CompleteLoop = EditorGUILayout.Toggle("Complete loop", m_fenceLayout.CompleteLoop);
		m_fenceLayout.ObjectMode = EditorGUILayout.Toggle("Object mode", m_fenceLayout.ObjectMode);

		if (m_fenceLayout.ObjectMode)
		{
			m_fenceLayout.PositionVariation = EditorGUILayout.Vector2Field("Position variation", m_fenceLayout.PositionVariation);
			m_fenceLayout.RotationVariation = EditorGUILayout.Vector2Field("Rotation variation", m_fenceLayout.RotationVariation);
			m_fenceLayout.ScaleVariation = EditorGUILayout.Vector2Field("Scale variation", m_fenceLayout.ScaleVariation);
		}
		else
		{
			m_fenceLayout.UseShear = EditorGUILayout.Toggle("Use shear", m_fenceLayout.UseShear);
		}

		m_fenceLayout.UseRaycast = EditorGUILayout.Toggle("Use raycast", m_fenceLayout.UseRaycast);

		if (m_fenceLayout.UseRaycast)
		{
			m_fenceLayout.RaycastOffsetMax = EditorGUILayout.FloatField("Raycast offset max", m_fenceLayout.RaycastOffsetMax);
			m_fenceLayout.RaycastOffsetMin = EditorGUILayout.FloatField("Raycast offset min", m_fenceLayout.RaycastOffsetMin);

			ShowRaycastSettings = EditorGUILayout.Foldout(ShowRaycastSettings, "Layer settings");

			if (ShowRaycastSettings)
			{
				for (int i = 0; i < 31; i++)
				{
					string layerName = LayerMask.LayerToName(i);
					if (layerName.Length > 0)
					{
						bool wasSet = (m_fenceLayout.LayerMask & (1 << i)) != 0;
						if (EditorGUILayout.Toggle("Layer: " + layerName, wasSet))
						{
							m_fenceLayout.LayerMask |= 1 << i;
						}
						else
						{
							m_fenceLayout.LayerMask &= ~(1 << i);

						}
					}
				}
			}
		}

		ShowPoints = EditorGUILayout.Foldout(ShowPoints, "Manually edit points");

		if (ShowPoints)
		{
			for (int i = 0; i < m_fenceLayout.FencePoints.Count; i++)
			{
				GUILayout.BeginHorizontal();
				Vector3 newPos = EditorGUILayout.Vector3Field("Point" + i, m_fenceLayout.FencePoints[i]);

				if (newPos != m_fenceLayout.FencePoints[i])
				{
					Vector3 world = newPos + m_fenceLayout.transform.position;
					newPos.y = SampleTerrainHeight(world) - m_fenceLayout.transform.position.y;
					m_fenceLayout.FencePoints[i] = newPos;
				}

				if (GUILayout.Button("X", GUILayout.Width(15)))
				{
					m_fenceLayout.FencePoints.RemoveAt(i);
					EditorUtility.SetDirty(m_fenceLayout);

				}

				GUILayout.EndHorizontal();
			}

			if (GUILayout.Button("Add point"))
			{
				Vector3 point;
				if (m_fenceLayout.FencePoints.Count > 1)
				{
					Vector3 dir = m_fenceLayout.FencePoints[m_fenceLayout.FencePoints.Count - 1] - m_fenceLayout.FencePoints[m_fenceLayout.FencePoints.Count - 2];
					point = m_fenceLayout.FencePoints[m_fenceLayout.FencePoints.Count - 1] + dir.normalized * 3.0f;
				}
				else if (m_fenceLayout.FencePoints.Count > 0)
				{
					point = m_fenceLayout.FencePoints[m_fenceLayout.FencePoints.Count - 1];
					point.x += 3.0f;
				}
				else
				{
					point = new Vector3();
				}

				m_fenceLayout.FencePoints.Add(point);
				EditorUtility.SetDirty(m_fenceLayout);
			}
		}

		if (EditorGUI.EndChangeCheck())
		{
			// Force a save of the prefab.
			EditorUtility.SetDirty(m_fenceLayout);
			EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

			CreateFences();
		}
	}

	// --------------------------------------------------------------------------

	public void OnEnable()
	{
		m_fenceLayout = target as FenceLayout;
#if UNITY_6000_7_OR_NEWER
		m_terrains = FindObjectsByType<Terrain>();
#elif UNITY_2023_1_OR_NEWER
		m_terrains = FindObjectsByType<Terrain>(FindObjectsSortMode.None);
#else
		m_terrains = FindObjectsOfType<Terrain>();
#endif

#if UNITY_2019_1_OR_NEWER
		SceneView.duringSceneGui += InputUpdate;
#else
		SceneView.onSceneGUIDelegate += InputUpdate;
#endif
	}

	// --------------------------------------------------------------------------

	public void OnDisable()
	{
#if UNITY_2019_1_OR_NEWER
		SceneView.duringSceneGui -= InputUpdate;
#else
		SceneView.onSceneGUIDelegate -= InputUpdate;
#endif
	}

	// --------------------------------------------------------------------------

	void InputUpdate(SceneView sceneview)
	{
		Event e = Event.current;
		int controlID = GUIUtility.GetControlID(GetHashCode(), FocusType.Passive);

		if (e.control)
		{
			HandleUtility.AddDefaultControl(controlID);
		}

		if (e.type != EventType.Repaint && e.type != EventType.Layout)
		{
			GameObject fenceObj = Selection.activeObject as GameObject;
			if (fenceObj)
			{
				FenceLayout fence = fenceObj.GetComponent<FenceLayout>();

				if (fence && fence == m_fenceLayout)
				{
					m_editMode = FenceLayout.EditMode.None;
					fence.SetEditMode(FenceLayout.EditMode.None, new Vector3());

					if (e.control)
					{
						Ray r = HandleUtility.GUIPointToWorldRay(e.mousePosition);

						RaycastHit result;
						if (RaycastInActiveStage(r, out result, Mathf.Infinity, ~0))
						{
							m_editMode = (e.control && e.shift) ? FenceLayout.EditMode.Delete : (e.control ? FenceLayout.EditMode.Add : FenceLayout.EditMode.None);
							m_mousePos = result.point;
							fence.SetEditMode(m_editMode, result.point);

							if (e.control && e.button == 0)
							{
								if (e.type == EventType.MouseDown)
								{
									ApplyEdit();
									EditorUtility.SetDirty(fence);
									// Consume the click so other subscribed editors don't apply it again
									// (this caused duplicate points -> zero-length, zero-scale fences).
									e.Use();
								}
							}
						}
					}
				}
			}
		}
	}

	// --------------------------------------------------------------------------

	public void ApplyEdit()
	{
		Undo.RecordObject(m_fenceLayout, m_editMode == FenceLayout.EditMode.Delete ? "Remove Fence Point" : "Add Fence Point");

		if (m_editMode == FenceLayout.EditMode.Add)
		{
			int idx = m_fenceLayout.GetAddPointIdx();
			Vector3 addPos = m_mousePos - m_fenceLayout.transform.position;

			// Don't add a point on top of an existing one (creates a zero-length segment).
			foreach (Vector3 existing in m_fenceLayout.FencePoints)
			{
				Vector3 d = existing - addPos;
				d.y = 0;
				if (d.sqrMagnitude < 0.01f)
				{
					return;
				}
			}

			if (idx >= m_fenceLayout.FencePoints.Count)
			{
				m_fenceLayout.FencePoints.Add(addPos);
			}
			else
			{
				m_fenceLayout.FencePoints.Insert(idx, addPos);
			}
		}
		else if (m_editMode == FenceLayout.EditMode.Delete)
		{
			int idx = m_fenceLayout.GetDeletePointIdx();
			if (idx >= 0)
			{
				m_fenceLayout.FencePoints.RemoveAt(idx);
			}
		}

		CreateFences();
	}

	// --------------------------------------------------------------------------

	public void CreateFences()
	{
		// Recreate fences.
		Vector3 lastPos = new Vector3();
		bool gotLast = false;

		for (int i = m_fenceLayout.transform.childCount - 1; i >= 0; i--)
		{
			Undo.DestroyObjectImmediate(m_fenceLayout.transform.GetChild(i).gameObject);
		}

		Quaternion rot = new Quaternion();
		float scale = 1.0f;

		if (m_fenceLayout.FencePrefabs.Length > 0 && m_fenceLayout.FencePrefabs[0] != null)
		{
			Random.InitState(123);

			int numLines = m_fenceLayout.FencePoints.Count + (m_fenceLayout.CompleteLoop ? 1 : 0);
			for (int p = 0; p < numLines; p++)
			{
				Vector3 point = m_fenceLayout.FencePoints[p % m_fenceLayout.FencePoints.Count] + m_fenceLayout.transform.position;

				if (gotLast)
				{
					// Fixed XZ length, and shear in Y.
					Vector3 toNext = point - lastPos;
					toNext.y = 0;

					// Skip degenerate (zero-length) segments - they would create zero-scale fences.
					if (toNext.sqrMagnitude < 0.0001f)
					{
						continue;
					}

					float scaledLength = m_fenceLayout.FenceLength * m_fenceLayout.FenceScale.z;
					float numFences = toNext.magnitude / Mathf.Max(scaledLength, 0.1f);

					// Stretch to fit.
					int numFencesInt = (int)Mathf.Max(1, Mathf.Round(numFences));
					float fenceLength = toNext.magnitude / numFencesInt;
					scale = fenceLength / scaledLength;

					float angle = Mathf.Atan2(toNext.z, toNext.x);
					float rotDeg = (-angle * Mathf.Rad2Deg) + 90.0f;

					Vector3 step = toNext.normalized * fenceLength;
					float stepMag = step.magnitude;

					Vector3 endOffset = Vector3.Scale(m_fenceLayout.FenceEndOffset, m_fenceLayout.FenceScale) * scale;

					if (m_fenceLayout.ObjectMode)
					{
						// Draw the final 'fence post' on the last stretch if not a complete loop.
						int fenceCount = numFencesInt;
						if (!m_fenceLayout.CompleteLoop && p == (m_fenceLayout.FencePoints.Count - 1))
						{
							fenceCount++;
						}

						Quaternion offsetRot = Quaternion.Euler(0, rotDeg, 0);

						for (int i = 0; i < fenceCount; i++)
						{
							// Pos variance is local to the line direction.
							Vector3 offset = new Vector3(Random.Range(-m_fenceLayout.PositionVariation.x, m_fenceLayout.PositionVariation.x), 0.0f, Random.Range(-m_fenceLayout.PositionVariation.y, m_fenceLayout.PositionVariation.y));
							offset = offsetRot * offset;

							float rotVariance = Random.Range(m_fenceLayout.RotationVariation.x, m_fenceLayout.RotationVariation.y);
							rot = Quaternion.Euler(0, m_fenceLayout.FenceRotation + rotDeg + rotVariance, 0);
							float scaleVariance = Random.Range(m_fenceLayout.ScaleVariation.x, m_fenceLayout.ScaleVariation.y);

							Vector3 createPos = lastPos + offset + step * i;
							float y1 = SampleTerrainHeight(createPos);

							createPos.y = y1;
							Transform fence = SpawnPrefab(GetRandomFencePrefab(), m_fenceLayout.transform);
							if (fence)
							{
								fence.position = createPos;
								fence.localRotation = rot;
								fence.localScale = m_fenceLayout.FenceScale * scaleVariance;
							}
						}
					}
					else
					{
						rot = Quaternion.Euler(0, rotDeg, 0);

						for (int i = 0; i < numFencesInt; i++)
						{
							// Find both ends.
							Vector3 createPos = lastPos + step * i;
							float y1 = SampleTerrainHeight(createPos);
							float y2 = SampleTerrainHeight(createPos + step);

							if (m_fenceLayout.UseShear)
							{
								createPos.y = y1;

								float shearAngleRad = (Mathf.PI * 0.5f) - Mathf.Atan2(y2 - y1, stepMag);

								// Avoid divide by zero on ridiculous angles.
								if (Mathf.Abs(shearAngleRad) > 0.01f)
								{
									// Build the shear.
									// (a) Rotate by theta/2 counter - clockwise.
									// (b) Scale with x - scaling factor = sin(theta / 2) and y-scaling factor = cos(theta / 2).
									// (c) Rotate by 45 degree clockwise.
									// (d) Scale with x-scaling factor = sqrt(2) / sin(theta) , and y-scaling factor = sqrt(2)
									GameObject t1 = new GameObject("Shear1");
									GameObject t2 = new GameObject("Shear2");
									t1.transform.parent = m_fenceLayout.transform;
									t2.transform.parent = t1.transform;

									float shearAngleDeg = shearAngleRad * Mathf.Rad2Deg;
									t1.transform.localRotation = Quaternion.Euler(shearAngleDeg * 0.5f, rotDeg, 0);
									t1.transform.localScale = new Vector3(1.0f, Mathf.Cos(shearAngleRad * 0.5f), Mathf.Sin(shearAngleRad * 0.5f));
									t2.transform.localRotation = Quaternion.Euler(-45.0f, 0, 0);
									t2.transform.localScale = new Vector3(1.0f, Mathf.Sqrt(2.0f), Mathf.Sqrt(2.0f) / Mathf.Sin(shearAngleRad));
									Transform fence = SpawnPrefab(GetRandomFencePrefab(), t2.transform);
									if (fence)
									{
										fence.localScale = new Vector3(m_fenceLayout.FenceScale.x, m_fenceLayout.FenceScale.y, m_fenceLayout.FenceScale.z * scale);
										fence.localRotation = Quaternion.Euler(0.0f, m_fenceLayout.FenceRotation, 0.0f);
										fence.localPosition = endOffset;
										t2.transform.position = createPos;
									}
								}
							}
							else
							{
								// Use the lowest point so it's definitely in the floor.
								createPos.y = Mathf.Min(y1, y2);

								// Apply the offset.
								// End offset is in the panel's local space (Z along the fence direction).
								// Same frame as the shear path: Z runs along the fence direction.
								createPos += rot * endOffset;

								Transform fence = SpawnPrefab(GetRandomFencePrefab(), m_fenceLayout.transform);
								if (fence)
								{
									fence.position = createPos;
									fence.rotation = rot * Quaternion.Euler(0.0f, m_fenceLayout.FenceRotation, 0.0f);
									fence.localScale = new Vector3(m_fenceLayout.FenceScale.x, m_fenceLayout.FenceScale.y, m_fenceLayout.FenceScale.z * scale);
								}
							}
						}
					}
				}

				lastPos = point;
				gotLast = true;
			}
		}

		CreateEndPost(rot, scale);
		RegisterCreatedChildren();
	}

	// --------------------------------------------------------------------------

	public void CalculateFromPrefabs()
	{
		Quaternion fenceRot = Quaternion.Euler(0.0f, m_fenceLayout.FenceRotation, 0.0f);

		Bounds panel;
		if (!TryGetLocalBounds(m_fenceLayout.FencePrefabs[0], fenceRot, out panel))
		{
			Debug.LogWarning("FenceLayout: 'Fence prefab 0' has no meshes to measure.", m_fenceLayout);
			return;
		}

		float length;
		float offsetZ;
		Bounds post;
		if (m_fenceLayout.PostPrefab != null && TryGetLocalBounds(m_fenceLayout.PostPrefab, fenceRot, out post))
		{
			// Panel = start post + rails reaching into the next panel's post.
			float postDepth = post.size.z;
			length = panel.size.z - postDepth;
			offsetZ = -(panel.min.z + postDepth * 0.5f);
		}
		else
		{
			length = panel.size.z;
			offsetZ = -panel.min.z;
		}

		length = Mathf.Max(0.01f, Mathf.Round(length * 1000.0f) / 1000.0f);
		offsetZ = Mathf.Round(offsetZ * 1000.0f) / 1000.0f;

		m_fenceLayout.FenceLength = length;
		m_fenceLayout.FenceEndOffset = new Vector3(m_fenceLayout.FenceEndOffset.x, m_fenceLayout.FenceEndOffset.y, offsetZ);
	}

	// --------------------------------------------------------------------------

	// Combined mesh bounds of a prefab in its root's local space, rotated by 'rot'.
	private static bool TryGetLocalBounds(Transform prefab, Quaternion rot, out Bounds bounds)
	{
		bounds = new Bounds();
		bool any = false;
		Matrix4x4 toRoot = Matrix4x4.Rotate(rot) * prefab.worldToLocalMatrix;

		foreach (MeshFilter mf in prefab.GetComponentsInChildren<MeshFilter>(true))
		{
			if (mf.sharedMesh != null)
			{
				Encapsulate(ref bounds, ref any, mf.sharedMesh.bounds, toRoot * mf.transform.localToWorldMatrix);
			}
		}
		foreach (SkinnedMeshRenderer smr in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
		{
			if (smr.sharedMesh != null)
			{
				Encapsulate(ref bounds, ref any, smr.sharedMesh.bounds, toRoot * smr.transform.localToWorldMatrix);
			}
		}
		return any;
	}

	private static void Encapsulate(ref Bounds bounds, ref bool any, Bounds local, Matrix4x4 m)
	{
		Vector3 c = local.center;
		Vector3 e = local.extents;
		for (int i = 0; i < 8; i++)
		{
			Vector3 corner = c + new Vector3((i & 1) != 0 ? e.x : -e.x, (i & 2) != 0 ? e.y : -e.y, (i & 4) != 0 ? e.z : -e.z);
			Vector3 p = m.MultiplyPoint3x4(corner);
			if (!any)
			{
				bounds = new Bounds(p, Vector3.zero);
				any = true;
			}
			else
			{
				bounds.Encapsulate(p);
			}
		}
	}

	// --------------------------------------------------------------------------

	// Register every generated child (and its hierarchy) with Undo so Ctrl+Z
	// removes it cleanly instead of leaving dangling objects.
	private void RegisterCreatedChildren()
	{
		Transform root = m_fenceLayout.transform;
		for (int i = 0; i < root.childCount; i++)
		{
			Undo.RegisterCreatedObjectUndo(root.GetChild(i).gameObject, "Create Fence");
		}
	}

	// --------------------------------------------------------------------------

	private void CreateEndPost(Quaternion rot, float scale)
	{
		// Create the final post, following the rot/scale of the fence.
		if (!m_fenceLayout.CompleteLoop && !m_fenceLayout.ObjectMode && m_fenceLayout.PostPrefab)
		{
			if (m_fenceLayout.FencePoints.Count > 0)
			{
				Transform post = SpawnPrefab(m_fenceLayout.PostPrefab, m_fenceLayout.transform);
				if (post)
				{
					Vector3 postPos = m_fenceLayout.FencePoints[m_fenceLayout.FencePoints.Count - 1] + m_fenceLayout.transform.position;
					postPos.y = SampleTerrainHeight(postPos);
					post.position = postPos;
					post.rotation = rot;
					post.localScale = m_fenceLayout.FenceScale * (scale > 0.0001f ? scale : 1.0f);
				}
			}
		}
	}

	// --------------------------------------------------------------------------

	private static Transform SpawnPrefab(Transform prefab, Transform parent)
	{
		if (prefab == null)
		{
			return null;
		}

		Transform t = null;
#if UNITY_2019_1_OR_NEWER
		// Only real prefab assets can be instantiated as linked prefabs; scene objects /
		// non-prefabs return null in newer Unity, so fall back to a plain clone.
		if (PrefabUtility.IsPartOfPrefabAsset(prefab))
		{
			t = PrefabUtility.InstantiatePrefab(prefab, parent) as Transform;
		}
#endif
		if (t == null)
		{
			t = Object.Instantiate(prefab, parent);
		}

		// Reset any transform baked into the prefab root; callers set the final values.
		t.localPosition = Vector3.zero;
		t.localRotation = Quaternion.identity;
		return t;
	}

	// --------------------------------------------------------------------------

	// Raycast against the physics scene of the active stage (prefab mode or main scene).
	private static bool RaycastInActiveStage(Ray ray, out RaycastHit hit, float maxDist, int layerMask)
	{
#if UNITY_2019_1_OR_NEWER
		var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
		Scene targetScene = prefabStage != null ? prefabStage.scene : SceneManager.GetActiveScene();
		PhysicsScene physicsScene = targetScene.GetPhysicsScene();
		Physics.SyncTransforms();
		return physicsScene.Raycast(ray.origin, ray.direction, out hit, maxDist, layerMask, QueryTriggerInteraction.Ignore);
#else
		return Physics.Raycast(ray, out hit, maxDist, layerMask, QueryTriggerInteraction.Ignore);
#endif
	}

	// --------------------------------------------------------------------------

	private Transform GetRandomFencePrefab()
	{
		int idx = Random.Range(0, m_fenceLayout.FencePrefabs.Length);
		return m_fenceLayout.FencePrefabs[idx] != null ? m_fenceLayout.FencePrefabs[idx] : m_fenceLayout.FencePrefabs[0];
	}

	// --------------------------------------------------------------------------

	private float SampleTerrainHeight(Vector3 pos)
	{
		if (m_fenceLayout.UseRaycast)
		{
			// Ray-cast against objects in the scene.
			RaycastHit hit;
			Vector3 rayStart = pos;
			rayStart.y += m_fenceLayout.RaycastOffsetMax;
			float rayLength = m_fenceLayout.RaycastOffsetMax - m_fenceLayout.RaycastOffsetMin;

			if (rayLength > 0)
			{
				// Raycast may hit a previously placed fence. Ignore hits on children.
				bool hitSelf;

				do
				{
					hitSelf = false;

					if (RaycastInActiveStage(new Ray(rayStart, Vector3.down), out hit, rayLength, m_fenceLayout.LayerMask))
					{
						if (hit.transform.IsChildOf(m_fenceLayout.transform))
						{
							// Start the test again from slightly further along.
							hitSelf = true;
							rayStart.y = hit.point.y - rayLength * 0.001f;
						}
						else
						{
							return hit.point.y;
						}
					}
				}
				while (hitSelf);
			}
		}
		else
		{
			// Find which terrain we're in. Assumes that no terrains overlap in XZ.
			foreach (Terrain terrain in m_terrains)
			{
				Vector3 minPos = terrain.GetPosition();
				Vector3 maxPos = minPos + terrain.terrainData.size;

				if (pos.x >= minPos.x && pos.x < maxPos.x && pos.z >= minPos.z && pos.z < maxPos.z)
				{
					// Ensure terrain Y position is included.
					return terrain.SampleHeight(pos) + terrain.GetPosition().y;
				}
			}
		}

		return pos.y;
	}
}
