#if UNITY_EDITOR
using UnityEditor;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
static class HierarchyIcons {

    // add your components and the associated icons here
    static Dictionary<Type, GUIContent> typeIcons = new Dictionary<Type, GUIContent>() {
        { typeof(Camera),               LoadIcon("camera") },
        { typeof(Canvas),               LoadIcon("frame") },
        { typeof(Button),               LoadIcon("function") },
        { typeof(Text),                 LoadIcon("text") },
        { typeof(RectTransform),        LoadIcon("rect-transform") },
        { typeof(ParticleSystem),       LoadIcon("particles") },
        { typeof(MonoBehaviour),        LoadIcon("script") },
        { typeof(Collider),             LoadIcon("collider") },
        { typeof(MeshFilter),           LoadIcon("grid") },
        { typeof(Transform),            LoadIcon("transform") }, 
	};

    static GUIContent LoadIcon(string iconName) {
        Texture2D texture = Resources.Load<Texture2D>($"HierarchyIcons/{iconName}");
        return texture != null ? new GUIContent(texture) : new GUIContent();
    }

    // cached game object information
    static Dictionary<int, GUIContent> labeledObjects = new Dictionary<int, GUIContent>();
    static HashSet<int> unlabeledObjects = new HashSet<int>();
    static GameObject[] previousSelection = null; // used to update state on deselect

    // set up all callbacks needed
    static HierarchyIcons() {
        EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;

        // callbacks for when we want to update the object GUI state:
        ObjectFactory.componentWasAdded += c => UpdateObject(c.gameObject.GetInstanceID());
        // there's no componentWasRemoved callback, but we can use selection as a proxy:
        Selection.selectionChanged += OnSelectionChanged;
    }

    static void OnHierarchyGUI(int id, Rect rect) {
        if (unlabeledObjects.Contains(id))
            return; // don't draw things with no component of interest

        if (ShouldDrawObject(id, out GUIContent icon)) {
            // GUI code here
            rect.xMin = rect.xMax - 20; // right-align the icon
            GUI.Label(rect, icon);
        }
    }

    static bool ShouldDrawObject(int id, out GUIContent icon) {
        if (labeledObjects.TryGetValue(id, out icon)) return true;
        // object is unsorted, add it and get icon, if applicable
        return SortObject(id, out icon);
    }

    static bool SortObject(int id, out GUIContent icon) {
        GameObject go = ResolveGameObjectFromHierarchyId(id);

        if (go) {
            foreach ((Type type, GUIContent typeIcon) in typeIcons) {

                Texture defaultIcon = EditorGUIUtility.GetIconForObject(go);
                if (defaultIcon) { labeledObjects.Add(id, icon = new GUIContent(defaultIcon)); return true; } else { if (go.GetComponent(type)) { labeledObjects.Add(id, icon = typeIcon); return true; } }
            }
        }

        unlabeledObjects.Add(id);
        icon = default;
        return false;
    }

    static GameObject ResolveGameObjectFromHierarchyId(int id) {
#if UNITY_6000_3_OR_NEWER
        return EditorUtility.EntityIdToObject((EntityId)id) as GameObject;
#else
        return EditorUtility.InstanceIDToObject(id) as GameObject;
#endif
    }

    static void UpdateObject(int id) {
        unlabeledObjects.Remove(id);
        labeledObjects.Remove(id);
        SortObject(id, out _);
    }

    const int MAX_SELECTION_UPDATE_COUNT = 3; // how many objects we want to allow to get updated on select/deselect

    static void OnSelectionChanged() {
        TryUpdateObjects(previousSelection); // update on deselect
        TryUpdateObjects(previousSelection = Selection.gameObjects); // update on select
    }

    static void TryUpdateObjects(GameObject[] objects) {
        if (objects != null && objects.Length > 0 && objects.Length <= MAX_SELECTION_UPDATE_COUNT) { // max of three to prevent performance hitches when selecting many objects
            foreach (GameObject go in objects) {
                UpdateObject(go.GetInstanceID());
            }
        }
    }
}
#endif