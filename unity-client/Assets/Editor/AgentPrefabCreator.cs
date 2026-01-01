#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using TheIsland.Visual;

namespace TheIsland.Editor
{
    /// <summary>
    /// Editor utility for creating and updating Agent prefabs.
    /// Access via menu: The Island > Create Agent Prefab
    /// </summary>
    public class AgentPrefabCreator : EditorWindow
    {
        private Sprite characterSprite;
        private string prefabName = "AgentPrefab";
        private Color agentColor = new Color(0.3f, 0.6f, 0.9f);
        private bool createWithPlaceholder = true;

        [MenuItem("The Island/Create Agent Prefab")]
        public static void ShowWindow()
        {
            var window = GetWindow<AgentPrefabCreator>("Agent Prefab Creator");
            window.minSize = new Vector2(350, 300);
        }

        [MenuItem("The Island/Quick Create Agent Prefab")]
        public static void QuickCreatePrefab()
        {
            CreateAgentPrefab("AgentPrefab", null, true);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Agent Prefab Creator", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUILayout.HelpBox(
                "Creates a new Agent prefab with:\n" +
                "- 2D Sprite with Billboard\n" +
                "- Floating UI (Name, HP, Energy bars)\n" +
                "- Speech Bubble system\n" +
                "- Click interaction",
                MessageType.Info);

            EditorGUILayout.Space(10);

            // Prefab name
            prefabName = EditorGUILayout.TextField("Prefab Name", prefabName);

            EditorGUILayout.Space(5);

            // Sprite selection
            createWithPlaceholder = EditorGUILayout.Toggle("Use Placeholder Sprite", createWithPlaceholder);

            if (!createWithPlaceholder)
            {
                characterSprite = (Sprite)EditorGUILayout.ObjectField(
                    "Character Sprite",
                    characterSprite,
                    typeof(Sprite),
                    false
                );
            }
            else
            {
                agentColor = EditorGUILayout.ColorField("Placeholder Color", agentColor);
            }

            EditorGUILayout.Space(20);

            // Create button
            if (GUILayout.Button("Create Prefab", GUILayout.Height(35)))
            {
                CreateAgentPrefab(prefabName, characterSprite, createWithPlaceholder);
            }

            EditorGUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "Prefab will be saved to: Assets/Prefabs/\n\n" +
                "After creation:\n" +
                "1. Assign the prefab to GameManager.agentPrefab\n" +
                "2. (Optional) Replace placeholder sprite later",
                MessageType.None);
        }

        private static void CreateAgentPrefab(string name, Sprite sprite, bool usePlaceholder)
        {
            // Ensure Prefabs folder exists
            string prefabFolder = "Assets/Prefabs";
            if (!AssetDatabase.IsValidFolder(prefabFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            }

            // Create root GameObject
            GameObject agentRoot = new GameObject(name);

            // Add AgentVisual component - it will create all visuals automatically
            AgentVisual visual = agentRoot.AddComponent<AgentVisual>();

            // If sprite is provided, we need to set it via SerializedObject
            if (!usePlaceholder && sprite != null)
            {
                SerializedObject so = new SerializedObject(visual);
                SerializedProperty spriteProp = so.FindProperty("characterSprite");
                if (spriteProp != null)
                {
                    spriteProp.objectReferenceValue = sprite;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            // Save as prefab
            string prefabPath = $"{prefabFolder}/{name}.prefab";

            // Check if prefab already exists
            GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existingPrefab != null)
            {
                if (!EditorUtility.DisplayDialog("Prefab Exists",
                    $"A prefab named '{name}' already exists. Overwrite?",
                    "Overwrite", "Cancel"))
                {
                    DestroyImmediate(agentRoot);
                    return;
                }
            }

            // Save prefab
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(agentRoot, prefabPath);

            // Cleanup scene object
            DestroyImmediate(agentRoot);

            // Select the new prefab
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);

            Debug.Log($"[AgentPrefabCreator] Created prefab at: {prefabPath}");
            EditorUtility.DisplayDialog("Success",
                $"Agent prefab created at:\n{prefabPath}\n\n" +
                "Don't forget to assign it to GameManager!",
                "OK");
        }
    }

    /// <summary>
    /// Additional menu items for quick actions.
    /// </summary>
    public static class IslandEditorMenus
    {
        [MenuItem("The Island/Setup Scene")]
        public static void SetupScene()
        {
            // Check for Camera
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                mainCam = camObj.AddComponent<Camera>();
                camObj.tag = "MainCamera";
            }

            // Add CameraController if not present
            if (mainCam.GetComponent<TheIsland.Core.CameraController>() == null)
            {
                mainCam.gameObject.AddComponent<TheIsland.Core.CameraController>();
                Debug.Log("[Setup] Added CameraController to Main Camera");
            }

            // Set camera position and rotation for isometric view
            mainCam.transform.position = new Vector3(0, 15, -10);
            mainCam.transform.rotation = Quaternion.Euler(50, 0, 0);
            mainCam.backgroundColor = new Color(0.4f, 0.6f, 0.9f); // Sky blue
            mainCam.clearFlags = CameraClearFlags.SolidColor;

            // Create ground plane if not exists
            if (GameObject.Find("Ground") == null)
            {
                GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "Ground";
                ground.transform.position = Vector3.zero;
                ground.transform.localScale = new Vector3(5, 1, 5);

                // Set ground material color
                var renderer = ground.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Material mat = new Material(Shader.Find("Standard"));
                    mat.color = new Color(0.3f, 0.5f, 0.3f); // Grass green
                    renderer.material = mat;
                }

                Debug.Log("[Setup] Created Ground plane");
            }

            // Create Agent Container if not exists
            if (GameObject.Find("Agents") == null)
            {
                GameObject container = new GameObject("Agents");
                container.transform.position = Vector3.zero;
                Debug.Log("[Setup] Created Agents container");
            }

            Debug.Log("[Setup] Scene setup complete!");
            EditorUtility.DisplayDialog("Scene Setup",
                "Scene has been configured with:\n" +
                "- Camera with CameraController\n" +
                "- Sky blue background\n" +
                "- Ground plane\n" +
                "- Agents container",
                "OK");
        }

        [MenuItem("The Island/Setup Managers")]
        public static void SetupManagers()
        {
            // Create NetworkManager
            if (Object.FindFirstObjectByType<TheIsland.Network.NetworkManager>() == null)
            {
                GameObject netMgr = new GameObject("NetworkManager");
                netMgr.AddComponent<TheIsland.Network.NetworkManager>();
                Debug.Log("[Setup] Created NetworkManager");
            }

            // Create GameManager
            if (Object.FindFirstObjectByType<TheIsland.Core.GameManager>() == null)
            {
                GameObject gameMgr = new GameObject("GameManager");
                gameMgr.AddComponent<TheIsland.Core.GameManager>();
                Debug.Log("[Setup] Created GameManager");
            }

            // Create UIManager
            if (Object.FindFirstObjectByType<TheIsland.UI.UIManager>() == null)
            {
                GameObject uiMgr = new GameObject("UIManager");
                uiMgr.AddComponent<TheIsland.UI.UIManager>();
                Debug.Log("[Setup] Created UIManager");
            }

            Debug.Log("[Setup] Managers setup complete!");
            EditorUtility.DisplayDialog("Managers Setup",
                "Created/verified:\n" +
                "- NetworkManager\n" +
                "- GameManager\n" +
                "- UIManager\n\n" +
                "Don't forget to assign AgentPrefab to GameManager!",
                "OK");
        }

        [MenuItem("The Island/Documentation")]
        public static void OpenDocs()
        {
            EditorUtility.DisplayDialog("The Island - Quick Guide",
                "CONTROLS:\n" +
                "- WASD: Move camera\n" +
                "- Mouse Scroll: Zoom in/out\n" +
                "- Click Agent: Feed agent\n\n" +
                "COMMANDS (in input field):\n" +
                "- feed Jack/Luna/Bob: Feed agent\n" +
                "- check: Check status\n" +
                "- reset: Reset all agents\n\n" +
                "SETUP ORDER:\n" +
                "1. The Island > Setup Scene\n" +
                "2. The Island > Setup Managers\n" +
                "3. The Island > Create Agent Prefab\n" +
                "4. Assign prefab to GameManager",
                "OK");
        }
    }
}
#endif
