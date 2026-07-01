using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEditorInternal;
using UnityEngine.SceneManagement;
#if UNITY_2021_2_OR_NEWER
using UnityEditor.SceneManagement;
#else
using UnityEditor.Experimental.SceneManagement;
#endif

public class HierarchyBookmarks : EditorWindow
{
    [System.Serializable]
    private class SceneBookmarkCollection
    {
        public List<SceneBookmarkEntry> scenes = new List<SceneBookmarkEntry>();
    }

    [System.Serializable]
    private class SceneBookmarkEntry
    {
        public string sceneKey;
        public List<string> bookmarkIds = new List<string>();
        public bool showHierarchyPaths;
    }

    private sealed class BookmarkUndoState : ScriptableObject
    {
        public string serializedData;
    }

    private const string WindowTitle = "Hierarchy Bookmarks";
    private static GUIContent bookmarkMarkerIcon;
    private static Texture2D tintedBookmarkMarkerTexture;
    private static Texture2D tintedTitleIconTexture;
    
    // Bookmark Icon Settings
    #if UNITY_6000_3_OR_NEWER
    private const string BookmarkIconName = "d_Texture3D Icon";
    #else
    private const string BookmarkIconName = "myassets-selected-focused@2x";
    #endif

    private const float BookmarkIconSize = 16f;
    private static readonly Vector2 BookmarkIconOffset = new Vector2(0f, 0f);
    private static readonly Color BookmarkMarkerTint = new Color(1.0f, 0.70f, 0.30f, 1f);
    private static readonly bool BookmarkStrongTint = true;

    // Title Icon Settings
    #if UNITY_6000_3_OR_NEWER
    private const string TitleIconName = "UnityRegistry@2x";
    #else
    private const string TitleIconName = "d_signalasset icon";
    #endif
    private static readonly Color TitleIconTint = new Color(1.0f, 0.70f, 0.30f, 1f);
    private static readonly bool TitleStrongTint = true;

    private enum SelectionBookmarkOperation
    {
        Add,
        Remove
    }

    private static List<GameObject> bookmarks = new List<GameObject>();
    private static List<string> activeSceneBookmarkIds = new List<string>();
    private Vector2 scrollPosition;
    private ReorderableList bookmarksReorderableList;
    private bool suppressSelectFromHandleClick = false;
    private static SceneBookmarkCollection sceneBookmarkCollection;
    private static BookmarkUndoState undoState;
    private static string activeSceneKey = string.Empty;
    private static bool activeSceneDataLoaded;
    private static bool showHierarchyPaths;
    private static bool staticCallbacksRegistered;
    private static GUIStyle headerToggleLabelStyle;
    private const string ShowPathsLabel = "Show Hierarchy Paths";
    private const string BookmarksKey = "HierarchyBookmarks";
    private static Dictionary<string, GameObject> playModeResolvedInstances = new Dictionary<string, GameObject>();
    private static Dictionary<string, string> playModeFallbackNames = null;

    [MenuItem("Tools/Hierarchy Bookmarks")]
    public static void ShowWindow()
    {
        HierarchyBookmarks window = GetWindow<HierarchyBookmarks>();
        window.RefreshWindowTitle();
        window.Show();
    }

    [MenuItem("GameObject/Toggle Hierarchy Bookmark", false, 49)]
    private static void ToggleBookmarkFromMenu(MenuCommand command)
    {
        if (IsReadOnlyMode())
            return;

        EnsureActiveSceneDataLoaded();

        if (Selection.gameObjects == null || Selection.gameObjects.Length == 0)
            return;

        int selectedCount;
        SelectionBookmarkOperation operation = GetSelectionBookmarkOperation(out selectedCount);

        if (operation == SelectionBookmarkOperation.Add)
        {
            AddSelectedObjects();
            PersistActiveSceneDataWithUndo("Add Bookmark");
        }
        else
        {
            RemoveSelectedObjects();
            PersistActiveSceneDataWithUndo("Remove Bookmark");
        }

        RefreshBookmarkVisualsGlobal();
    }

    [MenuItem("GameObject/Toggle Hierarchy Bookmark", true)]
    private static bool ValidateToggleBookmarkFromMenu()
    {
        if (IsReadOnlyMode())
            return false;

        EnsureActiveSceneDataLoaded();

        return Selection.gameObjects != null && Selection.gameObjects.Length > 0 && GetFirstValidSelectedTarget() != null;
    }

    private void OnEnable()
    {
        RegisterStaticCallbacks();
        EnsureActiveSceneDataLoaded();
        RefreshWindowTitle();
        EnsureBookmarksReorderableList();
    }

    private void OnDisable()
    {
        SaveBookmarks();
    }

    private void OnGUI()
    {
        EnsureActiveSceneDataLoaded();
        RebuildVisibleBookmarksFromIds();

        EnsureBookmarksReorderableList();
        bool isPrefabIsolationMode = IsInPrefabIsolationMode();
        bool isReadOnlyMode = IsReadOnlyMode();

        bookmarksReorderableList.draggable = !isReadOnlyMode;
        bookmarksReorderableList.displayRemove = !isReadOnlyMode;

        scrollPosition = GUILayout.BeginScrollView(scrollPosition);
        EditorGUI.BeginDisabledGroup(isPrefabIsolationMode);
        if (bookmarksReorderableList != null)
        {
            bookmarksReorderableList.DoLayoutList();
        }
        EditorGUI.EndDisabledGroup();
        GUILayout.EndScrollView();
    }

    private static SelectionBookmarkOperation GetSelectionBookmarkOperation(out int selectedCount)
    {
        EnsureActiveSceneDataLoaded();
        GameObject[] selectedObjects = Selection.gameObjects;
        selectedCount = 0;

        bool hasBookmarkedSelection = false;
        bool hasUnbookmarkedSelection = false;

        foreach (GameObject selectedObject in selectedObjects)
        {
            if (!IsValidTargetForActiveScene(selectedObject))
                continue;

            selectedCount++;

            string id;
            if (!TryGetGlobalObjectIdString(selectedObject, out id))
                continue;

            if (activeSceneBookmarkIds.Contains(id))
                hasBookmarkedSelection = true;
            else
                hasUnbookmarkedSelection = true;

            // Mixed selection defaults to add.
            if (hasBookmarkedSelection && hasUnbookmarkedSelection)
                return SelectionBookmarkOperation.Add;
        }

        if (hasBookmarkedSelection)
            return SelectionBookmarkOperation.Remove;

        return SelectionBookmarkOperation.Add;
    }

    private static void AddSelectedObjects()
    {
        foreach (var selectedObject in Selection.gameObjects)
        {
            if (!IsValidTargetForActiveScene(selectedObject))
                continue;

            string id;
            if (TryGetGlobalObjectIdString(selectedObject, out id) && !activeSceneBookmarkIds.Contains(id))
                activeSceneBookmarkIds.Add(id);
        }

        RebuildVisibleBookmarksFromIds();
    }

    private static void RemoveSelectedObjects()
    {
        foreach (var selectedObject in Selection.gameObjects)
        {
            if (!IsValidTargetForActiveScene(selectedObject))
                continue;

            string id;
            if (TryGetGlobalObjectIdString(selectedObject, out id))
                activeSceneBookmarkIds.Remove(id);
        }

        RebuildVisibleBookmarksFromIds();
    }

    private static GameObject GetFirstValidSelectedTarget()
    {
        foreach (GameObject selectedObject in Selection.gameObjects)
        {
            if (IsValidTargetForActiveScene(selectedObject))
                return selectedObject;
        }

        return null;
    }

    [InitializeOnLoadMethod]
    private static void InitializeHierarchyOverlay()
    {
        RegisterStaticCallbacks();
        EnsureActiveSceneDataLoaded();
        EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;
    }

    private static void OnHierarchyGUI(int instanceID, Rect selectionRect)
    {
        EnsureActiveSceneDataLoaded();
        GameObject obj = ResolveGameObjectFromHierarchyId(instanceID);
        if (obj == null)
            return;

        if (bookmarks.Contains(obj))
        {
            // Draw over the default object-type icon area (cube) on the left.
            Rect r = new Rect(selectionRect.x + BookmarkIconOffset.x, selectionRect.y + BookmarkIconOffset.y, BookmarkIconSize, BookmarkIconSize);
            Texture markerTexture = GetTintedBookmarkMarkerTexture();
            if (markerTexture != null)
                GUI.DrawTexture(r, markerTexture, ScaleMode.ScaleToFit, true);
        }

        Event e = Event.current;
        if (e != null && e.alt && e.type == EventType.MouseDown && e.button == 0 && selectionRect.Contains(e.mousePosition))
        {
            if (IsReadOnlyMode())
                return;

            if (IsValidTargetForActiveScene(obj))
            {
                string globalId;
                if (TryGetGlobalObjectIdString(obj, out globalId))
                {
                    if (activeSceneBookmarkIds.Contains(globalId))
                    {
                        activeSceneBookmarkIds.Remove(globalId);
                        PersistActiveSceneDataWithUndo("Remove Bookmark");
                    }
                    else
                    {
                        activeSceneBookmarkIds.Add(globalId);
                        PersistActiveSceneDataWithUndo("Add Bookmark");
                    }

                    RebuildVisibleBookmarksFromIds();
                    RefreshBookmarkVisualsGlobal();
                    e.Use();
                }
            }
        }
    }

    private static void RegisterStaticCallbacks()
    {
        if (staticCallbacksRegistered)
            return;

        staticCallbacksRegistered = true;
        EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
        Undo.undoRedoPerformed += OnUndoRedoPerformed;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            if (playModeResolvedInstances != null) playModeResolvedInstances.Clear();
            if (playModeFallbackNames != null) playModeFallbackNames.Clear();
            playModeFallbackNames = null;

            List<string> mappings = new List<string>();
            for (int i = 0; i < activeSceneBookmarkIds.Count; i++)
            {
                string id = activeSceneBookmarkIds[i];
                if (TryResolveGameObjectFromGlobalId(id, out GameObject resolved) && resolved != null)
                {
                    mappings.Add(id + "::" + BuildHierarchyPath(resolved.transform));
                }
            }
            SessionState.SetString("HierarchyBookmarks_FallbackNames", string.Join("||", mappings.ToArray()));
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            if (playModeResolvedInstances != null) playModeResolvedInstances.Clear();
            if (playModeFallbackNames != null) playModeFallbackNames.Clear();
            playModeFallbackNames = null;
            
            SessionState.EraseString("HierarchyBookmarks_FallbackNames");
            // Discard play mode modifications by reloading from EditorPrefs
            sceneBookmarkCollection = LoadCollection();
            activeSceneDataLoaded = false;
            EnsureActiveSceneDataLoaded();
            RefreshBookmarkVisualsGlobal();
        }
    }

    private static void OnActiveSceneChanged(Scene _, Scene __)
    {
        activeSceneDataLoaded = false;
        EnsureActiveSceneDataLoaded();
        RefreshBookmarkVisualsGlobal();
    }

    private static void OnUndoRedoPerformed()
    {
        if (undoState != null && !string.IsNullOrEmpty(undoState.serializedData))
            sceneBookmarkCollection = DeserializeCollection(undoState.serializedData);

        activeSceneDataLoaded = false;
        EnsureActiveSceneDataLoaded();
        RefreshBookmarkVisualsGlobal();
    }

    private static void EnsureActiveSceneDataLoaded()
    {
        string sceneKey = GetActiveSceneKey();
        if (activeSceneDataLoaded && activeSceneKey == sceneKey)
            return;

        if (sceneBookmarkCollection == null)
            sceneBookmarkCollection = LoadCollection();

        activeSceneKey = sceneKey;
        SceneBookmarkEntry entry = GetOrCreateSceneEntry(sceneBookmarkCollection, sceneKey);

        activeSceneBookmarkIds.Clear();
        if (entry.bookmarkIds != null)
            activeSceneBookmarkIds.AddRange(entry.bookmarkIds);

        showHierarchyPaths = entry.showHierarchyPaths;
        activeSceneDataLoaded = true;
        RebuildVisibleBookmarksFromIds();
    }

    private static bool IsReadOnlyMode()
    {
        return IsInPrefabIsolationMode();
    }

    private static string GetActiveSceneKey()
    {
        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (!activeScene.IsValid())
            return "__NO_SCENE__";

        string underlyingPath = activeScene.path;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            // In Play Mode, the scene path might be empty or a temp path, 
            // but activeScene.name generally matches the original scene name.
            // When started from the Editor, the original scene path is often still registered inside EditorBuildSettings or can be matched.
            // For simplicity, we just fallback to the scene name in playmode for unsaved situations 
            // but if the path is populated we use it. Often it's empty in play mode for unnamed new scenes.
            if (string.IsNullOrEmpty(underlyingPath))
                 return activeScene.name;
            return underlyingPath;
        }

        if (!string.IsNullOrEmpty(underlyingPath))
            return underlyingPath;

        return "__UNSAVED__/" + activeScene.name;
    }

    private static SceneBookmarkCollection LoadCollection()
    {
        string json = EditorPrefs.GetString(BookmarksKey, string.Empty);
        if (string.IsNullOrEmpty(json))
            return new SceneBookmarkCollection();

        SceneBookmarkCollection parsed;
        if (TryDeserializeCollection(json, out parsed))
            return parsed;

        if (TryMigrateLegacyInstanceIdList(json, out parsed))
        {
            EditorPrefs.SetString(BookmarksKey, JsonUtility.ToJson(parsed));
            return parsed;
        }

        string extractedJson;
        if (TryExtractFirstJsonObject(json, out extractedJson) && TryDeserializeCollection(extractedJson, out parsed))
        {
            EditorPrefs.SetString(BookmarksKey, JsonUtility.ToJson(parsed));
            return parsed;
        }

        SceneBookmarkCollection empty = new SceneBookmarkCollection();
        EditorPrefs.SetString(BookmarksKey, JsonUtility.ToJson(empty));
        return empty;
    }

    private static SceneBookmarkCollection DeserializeCollection(string json)
    {
        SceneBookmarkCollection collection;
        if (TryDeserializeCollection(json, out collection))
            return collection;

        return new SceneBookmarkCollection();
    }

    private static bool TryDeserializeCollection(string json, out SceneBookmarkCollection collection)
    {
        collection = null;
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            SceneBookmarkCollection parsed = JsonUtility.FromJson<SceneBookmarkCollection>(json);
            if (parsed == null)
                return false;

            if (parsed.scenes == null)
                parsed.scenes = new List<SceneBookmarkEntry>();

            for (int i = parsed.scenes.Count - 1; i >= 0; i--)
            {
                SceneBookmarkEntry entry = parsed.scenes[i];
                if (entry == null)
                {
                    parsed.scenes.RemoveAt(i);
                    continue;
                }

                if (entry.bookmarkIds == null)
                    entry.bookmarkIds = new List<string>();
            }

            collection = parsed;
            return true;
        }
        catch (System.Exception)
        {
            return false;
        }
    }

    private static bool TryMigrateLegacyInstanceIdList(string rawData, out SceneBookmarkCollection migrated)
    {
        migrated = null;
        if (string.IsNullOrWhiteSpace(rawData) || rawData.IndexOf('{') >= 0 || rawData.IndexOf('[') >= 0)
            return false;

        string[] parts = rawData.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return false;

        SceneBookmarkCollection collection = new SceneBookmarkCollection();
        string sceneKey = GetActiveSceneKey();
        SceneBookmarkEntry entry = GetOrCreateSceneEntry(collection, sceneKey);

        for (int i = 0; i < parts.Length; i++)
        {
            int instanceId;
            if (!int.TryParse(parts[i], out instanceId))
                return false;

            GameObject go = ResolveGameObjectFromHierarchyId(instanceId);
            if (!IsValidTargetForActiveScene(go))
                continue;

            string globalId;
            if (!TryGetGlobalObjectIdString(go, out globalId))
                continue;

            if (!entry.bookmarkIds.Contains(globalId))
                entry.bookmarkIds.Add(globalId);
        }

        migrated = collection;
        return true;
    }

    private static bool TryExtractFirstJsonObject(string rawData, out string jsonObject)
    {
        jsonObject = string.Empty;
        if (string.IsNullOrWhiteSpace(rawData))
            return false;

        int start = rawData.IndexOf('{');
        if (start < 0)
            return false;

        int depth = 0;
        for (int i = start; i < rawData.Length; i++)
        {
            char c = rawData[i];
            if (c == '{')
                depth++;
            else if (c == '}')
                depth--;

            if (depth == 0)
            {
                jsonObject = rawData.Substring(start, i - start + 1);
                return true;
            }
        }

        return false;
    }

    private static SceneBookmarkEntry GetOrCreateSceneEntry(SceneBookmarkCollection collection, string sceneKey)
    {
        for (int i = 0; i < collection.scenes.Count; i++)
        {
            SceneBookmarkEntry existing = collection.scenes[i];
            if (existing != null && existing.sceneKey == sceneKey)
            {
                if (existing.bookmarkIds == null)
                    existing.bookmarkIds = new List<string>();
                return existing;
            }
        }

        SceneBookmarkEntry created = new SceneBookmarkEntry();
        created.sceneKey = sceneKey;
        created.bookmarkIds = new List<string>();
        created.showHierarchyPaths = false;
        collection.scenes.Add(created);
        return created;
    }

    private static void PersistActiveSceneData()
    {
        EnsureActiveSceneDataLoaded();

        if (sceneBookmarkCollection == null)
            sceneBookmarkCollection = new SceneBookmarkCollection();

        SceneBookmarkEntry entry = GetOrCreateSceneEntry(sceneBookmarkCollection, activeSceneKey);
        entry.bookmarkIds.Clear();
        entry.bookmarkIds.AddRange(activeSceneBookmarkIds);
        entry.showHierarchyPaths = showHierarchyPaths;

        string serialized = JsonUtility.ToJson(sceneBookmarkCollection);
        
        if (!EditorApplication.isPlaying)
        {
            EditorPrefs.SetString(BookmarksKey, serialized);
        }

        if (undoState != null)
            undoState.serializedData = serialized;
    }

    private static void PersistActiveSceneDataWithUndo(string actionName)
    {
        EnsureUndoState();
        Undo.RegisterCompleteObjectUndo(undoState, actionName);
        PersistActiveSceneData();
    }

    private static void EnsureUndoState()
    {
        if (undoState != null)
            return;

        undoState = CreateInstance<BookmarkUndoState>();
        undoState.hideFlags = HideFlags.HideAndDontSave;
        undoState.serializedData = EditorPrefs.GetString(BookmarksKey, string.Empty);
    }

    private static void RebuildVisibleBookmarksFromIds()
    {
        bookmarks.Clear();
        for (int i = 0; i < activeSceneBookmarkIds.Count; i++)
        {
            string id = activeSceneBookmarkIds[i];
            GameObject resolved;
            if (TryResolveGameObjectFromGlobalId(id, out resolved) && IsValidTargetForActiveScene(resolved))
            {
                bookmarks.Add(resolved);
            }
            else if (EditorApplication.isPlaying)
            {
                bool recovered = false;
                
                if (playModeFallbackNames == null)
                {
                    playModeFallbackNames = new Dictionary<string, string>();
                    string fallbackStr = SessionState.GetString("HierarchyBookmarks_FallbackNames", string.Empty);
                    string[] pairs = fallbackStr.Split(new string[] { "||" }, System.StringSplitOptions.RemoveEmptyEntries);
                    for (int p = 0; p < pairs.Length; p++)
                    {
                        int sepIndex = pairs[p].IndexOf("::");
                        if (sepIndex > 0)
                        {
                            playModeFallbackNames[pairs[p].Substring(0, sepIndex)] = pairs[p].Substring(sepIndex + 2);
                        }
                    }
                }

                if (playModeFallbackNames.TryGetValue(id, out string fallbackPath) && !string.IsNullOrEmpty(fallbackPath))
                {
                    if (playModeResolvedInstances == null)
                        playModeResolvedInstances = new Dictionary<string, GameObject>();

                    if (!playModeResolvedInstances.TryGetValue(id, out GameObject foundByPath) || foundByPath == null)
                    {
                        foundByPath = FindGameObjectByPathIncludingInactive(fallbackPath);
                        playModeResolvedInstances[id] = foundByPath;
                    }

                    if (foundByPath != null && IsValidTargetForActiveScene(foundByPath))
                    {
                        bookmarks.Add(foundByPath);
                        recovered = true;
                    }
                }

                if (!recovered)
                {
                    // In Play Mode, DontDestroyOnLoad objects might fail to resolve via their original Edit Mode GlobalObjectId.
                    // Or they might resolve to null because they moved to the DontDestroyOnLoad scene and got a new instance ID.
                    // We'll preserve them in the list as missing/null so they don't disappear from the UI,
                    // and they won't be stripped because we don't save to EditorPrefs during Play Mode.
                    bookmarks.Add(null);
                }
            }
        }
    }

    private static GameObject FindGameObjectByPathIncludingInactive(string path)
    {
        GameObject activeObj = GameObject.Find(path);
        if (activeObj != null)
            return activeObj;

        // Optimization: Rather than building paths for thousands of GameObjects,
        // we extract the final leaf name and quickly filter the initial loop.
        int lastSlash = path.LastIndexOf('/');
        string leafName = lastSlash >= 0 ? path.Substring(lastSlash + 1) : path;

        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < allObjects.Length; i++)
        {
            GameObject go = allObjects[i];
            // Match the leaf name and filter out assets / hidden internal items
            if (go != null && go.name == leafName && !EditorUtility.IsPersistent(go) && (go.hideFlags & HideFlags.HideInHierarchy) == 0)
            {
                // Full path structure verification
                if (BuildHierarchyPath(go.transform) == path)
                {
                    return go;
                }
            }
        }
        return null;
    }

    private static bool TryResolveGameObjectFromGlobalId(string idString, out GameObject go)
    {
        go = null;
        if (string.IsNullOrEmpty(idString))
            return false;

        GlobalObjectId globalId;
        if (!GlobalObjectId.TryParse(idString, out globalId))
            return false;

        UnityEngine.Object resolved = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId);
        go = resolved as GameObject;
        return go != null;
    }

    private static bool TryGetGlobalObjectIdString(GameObject go, out string id)
    {
        id = string.Empty;
        if (go == null)
            return false;

        GlobalObjectId globalId = GlobalObjectId.GetGlobalObjectIdSlow(go);
        id = globalId.ToString();
        return !string.IsNullOrEmpty(id);
    }

    private static bool IsValidTargetForActiveScene(GameObject go)
    {
        if (go == null)
            return false;

        if (PrefabStageUtility.GetPrefabStage(go) != null)
            return false;

        if (EditorUtility.IsPersistent(go))
            return false;

        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (!activeScene.IsValid())
            return false;

        if (go.scene == activeScene)
            return true;

        if (EditorApplication.isPlaying && go.scene.name == "DontDestroyOnLoad")
            return true;

        return false;
    }

    private static void SyncIdsFromVisibleBookmarksPreservingUnresolved()
    {
        List<string> visibleIds = new List<string>();
        for (int i = 0; i < bookmarks.Count; i++)
        {
            string id;
            if (TryGetGlobalObjectIdString(bookmarks[i], out id) && !visibleIds.Contains(id))
                visibleIds.Add(id);
        }

        List<string> unresolvedIds = new List<string>();
        for (int i = 0; i < activeSceneBookmarkIds.Count; i++)
        {
            string id = activeSceneBookmarkIds[i];
            GameObject resolved;
            if (!TryResolveGameObjectFromGlobalId(id, out resolved))
                unresolvedIds.Add(id);
        }

        activeSceneBookmarkIds.Clear();
        activeSceneBookmarkIds.AddRange(visibleIds);
        activeSceneBookmarkIds.AddRange(unresolvedIds);
    }

    private void EnsureBookmarksReorderableList()
    {
        if (bookmarksReorderableList != null)
            return;

        bookmarksReorderableList = new ReorderableList(bookmarks, typeof(GameObject), true, true, false, true);
        bookmarksReorderableList.elementHeightCallback = _ =>
        {
            return EditorGUIUtility.singleLineHeight + 2f;
        };

        bookmarksReorderableList.drawHeaderCallback = rect =>
        {
            EditorGUI.LabelField(rect, "Current Bookmarks: (" + (bookmarks.Count == 0 ? "None" : bookmarks.Count.ToString()) + ")");

            if (headerToggleLabelStyle == null)
            {
                headerToggleLabelStyle = new GUIStyle(EditorStyles.miniLabel);
                headerToggleLabelStyle.alignment = TextAnchor.MiddleRight;
                headerToggleLabelStyle.normal.textColor = new Color(0.72f, 0.72f, 0.72f, 1f);
            }

            float toggleWidth = 16f;
            float spacing = 4f;
            Vector2 labelSize = headerToggleLabelStyle.CalcSize(new GUIContent(ShowPathsLabel));
            Rect toggleRect = new Rect(rect.xMax - toggleWidth, rect.y + 1f, toggleWidth, EditorGUIUtility.singleLineHeight);
            Rect labelRect = new Rect(toggleRect.x - spacing - labelSize.x, rect.y + 1f, labelSize.x, EditorGUIUtility.singleLineHeight);

            EditorGUI.LabelField(labelRect, ShowPathsLabel, headerToggleLabelStyle);
            bool newShowHierarchyPaths = EditorGUI.Toggle(toggleRect, showHierarchyPaths);
            if (newShowHierarchyPaths != showHierarchyPaths)
            {
                showHierarchyPaths = newShowHierarchyPaths;
                PersistActiveSceneDataWithUndo("Toggle Bookmark Paths");
                RefreshBookmarkVisualsGlobal();
            }
        };

        bookmarksReorderableList.drawElementCallback = (rect, index, isActive, isFocused) =>
        {
            if (index < 0 || index >= bookmarks.Count)
                return;

            bool isPrefabIsolationMode = IsInPrefabIsolationMode();

            Rect rowRect = new Rect(rect.x, rect.y + 1f, rect.width, EditorGUIUtility.singleLineHeight);
            Rect clickRect = new Rect(rect.x, rect.y + 1f, Mathf.Max(0f, rect.width), EditorGUIUtility.singleLineHeight);
            GameObject currentObj = bookmarks[index];

            string displayText = "<missing>";
            if (currentObj != null)
            {
                displayText = showHierarchyPaths ? BuildHierarchyPath(currentObj.transform) : currentObj.name;
            }

            GUIContent content = new GUIContent(displayText);
            if (currentObj != null)
            {
                GUIContent iconContent = EditorGUIUtility.ObjectContent(currentObj, typeof(GameObject));
                if (iconContent != null)
                    content.image = iconContent.image;
            }

            GUIStyle mixedFieldStyle = new GUIStyle(EditorStyles.objectField);
            mixedFieldStyle.alignment = TextAnchor.MiddleLeft;
            mixedFieldStyle.imagePosition = ImagePosition.ImageLeft;

            if (!isPrefabIsolationMode && GUI.Button(clickRect, content, mixedFieldStyle))
            {
                bookmarksReorderableList.index = index;
                SelectBookmarkedObject(bookmarks[index]);
            }
        };

        bookmarksReorderableList.onSelectCallback = list =>
        {
            // Intentionally empty: selection is now only triggered by the explicit click target in the row.
        };

        bookmarksReorderableList.onReorderCallback = _ =>
        {
            SyncIdsFromVisibleBookmarksPreservingUnresolved();
            PersistActiveSceneDataWithUndo("Reorder Bookmarks");
            RefreshBookmarkVisualsGlobal();
        };

        bookmarksReorderableList.onChangedCallback = _ =>
        {
            SyncIdsFromVisibleBookmarksPreservingUnresolved();
            PersistActiveSceneData();
            RefreshBookmarkVisualsGlobal();
        };

        bookmarksReorderableList.onRemoveCallback = list =>
        {
            if (list.index < 0 || list.index >= bookmarks.Count)
                return;

            PersistActiveSceneDataWithUndo("Remove Bookmark");
            
            // If the element is null (unresolved in play mode), we need to manually remove its ID
            if (bookmarks[list.index] == null && list.index < activeSceneBookmarkIds.Count)
            {
                activeSceneBookmarkIds.RemoveAt(list.index);
                bookmarks.RemoveAt(list.index);
            }
            else
            {
                ReorderableList.defaultBehaviours.DoRemoveButton(list);
                SyncIdsFromVisibleBookmarksPreservingUnresolved();
            }
            
            PersistActiveSceneData();
            RefreshBookmarkVisualsGlobal();
        };
    }

    private static string BuildHierarchyPath(Transform target)
    {
        if (target == null)
            return string.Empty;

        string path = target.name;
        while (target.parent != null)
        {
            target = target.parent;
            path = target.name + "/" + path;
        }

        return path;
    }

    private static void SelectBookmarkedObject(GameObject selectedBookmark)
    {
        if (IsInPrefabIsolationMode())
            return;

        if (selectedBookmark == null)
            return;

        EditorApplication.delayCall += () =>
        {
            if (selectedBookmark == null)
                return;

            Selection.objects = new UnityEngine.Object[] { selectedBookmark };
            Selection.activeGameObject = selectedBookmark;
            EditorGUIUtility.PingObject(selectedBookmark);
        };
    }

    private static GameObject ResolveGameObjectFromHierarchyId(int instanceID)
    {
        #if UNITY_6000_3_OR_NEWER
        return EditorUtility.EntityIdToObject((EntityId)instanceID) as GameObject;
        #else
        return EditorUtility.InstanceIDToObject(instanceID) as GameObject;
        #endif
    }

    private static bool IsInPrefabIsolationMode()
    {
        // Prefab stage objects are transient to the prefab editing context.
        // Disable bookmarks whenever any prefab stage is active so we avoid
        // persisting references that become invalid after leaving prefab mode.
        return PrefabStageUtility.GetCurrentPrefabStage() != null;
    }

    private static void SaveBookmarks()
    {
        PersistActiveSceneData();
    }

    private void RefreshWindowTitle()
    {
        Texture icon = GetTintedTitleIconTexture();
        titleContent = new GUIContent(WindowTitle, icon);
    }

    private static GUIContent GetBookmarkMarkerIcon()
    {
        if (bookmarkMarkerIcon == null)
            bookmarkMarkerIcon = EditorGUIUtility.IconContent(BookmarkIconName);

        return bookmarkMarkerIcon;
    }

    private static Texture GetTintedBookmarkMarkerTexture()
    {
        if (tintedBookmarkMarkerTexture != null)
            return tintedBookmarkMarkerTexture;

        GUIContent sourceContent = GetBookmarkMarkerIcon();
        Texture sourceTexture = sourceContent != null ? sourceContent.image : null;
        if (sourceTexture == null)
            return null;

        tintedBookmarkMarkerTexture = CreateTintedTexture(sourceTexture, BookmarkMarkerTint, BookmarkStrongTint);
        return tintedBookmarkMarkerTexture != null ? tintedBookmarkMarkerTexture : sourceTexture;
    }

    private static Texture GetTintedTitleIconTexture()
    {
        if (tintedTitleIconTexture != null)
            return tintedTitleIconTexture;

        GUIContent sourceContent = EditorGUIUtility.IconContent(TitleIconName);
        Texture sourceTexture = sourceContent != null ? sourceContent.image : null;
        if (sourceTexture == null)
            return null;

        tintedTitleIconTexture = CreateTintedTexture(sourceTexture, TitleIconTint, TitleStrongTint);
        return tintedTitleIconTexture != null ? tintedTitleIconTexture : sourceTexture;
    }

    private static Texture2D CreateTintedTexture(Texture sourceTexture, Color tint, bool forceStrongTint)
    {
        if (sourceTexture == null)
            return null;

        int width = sourceTexture.width;
        int height = sourceTexture.height;
        if (width <= 0 || height <= 0)
            return null;

        RenderTexture temporary = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        RenderTexture previous = RenderTexture.active;

        Graphics.Blit(sourceTexture, temporary);
        RenderTexture.active = temporary;

        Texture2D tinted = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tinted.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
        tinted.Apply(false, false);

        Color[] pixels = tinted.GetPixels();
        for (int i = 0; i < pixels.Length; i++)
        {
            Color pixel = pixels[i];
            if (forceStrongTint)
            {
                float brightness = Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b));
                float shade = Mathf.Lerp(0.5f, 1.15f, brightness);
                pixels[i] = new Color(
                    Mathf.Clamp01(tint.r * shade),
                    Mathf.Clamp01(tint.g * shade),
                    Mathf.Clamp01(tint.b * shade),
                    pixel.a);
            }
            else
            {
                pixels[i] = new Color(pixel.r * tint.r, pixel.g * tint.g, pixel.b * tint.b, pixel.a);
            }
        }

        tinted.SetPixels(pixels);
        tinted.Apply(false, true);
        tinted.hideFlags = HideFlags.HideAndDontSave;

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(temporary);

        return tinted;
    }

    private void RefreshBookmarkVisuals()
    {
        Repaint();
        EditorApplication.RepaintHierarchyWindow();
    }

    private static void RefreshBookmarkVisualsGlobal()
    {
        EditorApplication.RepaintHierarchyWindow();
        HierarchyBookmarks[] windows = Resources.FindObjectsOfTypeAll<HierarchyBookmarks>();
        foreach (HierarchyBookmarks window in windows)
        {
            if (window != null)
                window.Repaint();
        }
    }
}