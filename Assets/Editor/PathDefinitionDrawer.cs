using UnityEditor;
using UnityEngine;

/// <summary>
/// Shows only the fields relevant to the selected <see cref="AntWalkBuilder.WalkType"/>
/// for each playlist entry, instead of every field for every walk type.
/// </summary>
[CustomPropertyDrawer(typeof(AntWalkBuilder.PathDefinition))]
public class PathDefinitionDrawer : PropertyDrawer
{
    static readonly string[] CommonFields = { "type", "repeats" };

    static readonly string[] LineFields = { "directionAngle", "distance", "lineReturnMode" };
    static readonly string[] LoopFields = { "directionAngle", "loopDiameter", "loopDirection" };
    static readonly string[] HalfLoopFields = { "directionAngle", "loopDiameter", "loopDirection", "halfLoopReturnMode" };
    static readonly string[] SpiralFields = { "directionAngle", "loopDiameter", "loopDirection", "spiralTurns", "spiralReturnMode" };
    static readonly string[] RandomPointFields = { "directionAngle", "teleport", "teleportDiameter", "holdDuration" };

    static string[] TypeSpecificFields(AntWalkBuilder.WalkType type)
    {
        switch (type)
        {
            case AntWalkBuilder.WalkType.Line: return LineFields;
            case AntWalkBuilder.WalkType.FullLoop: return LoopFields;
            case AntWalkBuilder.WalkType.HalfLoop: return HalfLoopFields;
            case AntWalkBuilder.WalkType.Spiral: return SpiralFields;
            case AntWalkBuilder.WalkType.RandomPoint: return RandomPointFields;
            default: return LineFields;
        }
    }

    static readonly Color LineTint = new Color(0.30f, 0.55f, 0.85f, 0.20f);
    static readonly Color LoopTint = new Color(0.35f, 0.75f, 0.35f, 0.20f);
    static readonly Color RandomPointTint = new Color(0.80f, 0.45f, 0.85f, 0.20f);
    static readonly Color HalfLoopTint = new Color(0.90f, 0.75f, 0.20f, 0.20f);
    static readonly Color SpiralTint = new Color(0.90f, 0.45f, 0.15f, 0.20f);

    static Color TintFor(AntWalkBuilder.WalkType type)
    {
        switch (type)
        {
            case AntWalkBuilder.WalkType.Line: return LineTint;
            case AntWalkBuilder.WalkType.FullLoop: return LoopTint;
            case AntWalkBuilder.WalkType.HalfLoop: return HalfLoopTint;
            case AntWalkBuilder.WalkType.Spiral: return SpiralTint;
            case AntWalkBuilder.WalkType.RandomPoint: return RandomPointTint;
            default: return Color.clear;
        }
    }

    // Near-white marker filling the gutter to the left of the active element, rather than
    // brightening the element's own backdrop.
    static readonly Color ActiveMarkerColor = new Color(0.95f, 0.95f, 0.95f, 1f);

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var walkType = (AntWalkBuilder.WalkType)property.FindPropertyRelative("type").enumValueIndex;
        Color tint = TintFor(walkType);
        int elementIndex = GetElementIndex(property);

        // Mark the currently-executing playlist entry so it's obvious at a glance during Play.
        bool isActiveSegment = Application.isPlaying &&
            property.serializedObject.targetObject is AntWalkBuilder builder &&
            elementIndex == builder.segmentIndex;

        EditorGUI.DrawRect(position, tint);

        if (isActiveSegment)
        {
            // Fill the gutter to the left of the element, out to the list boundary, as a
            // "playhead" marker instead of lightening the element's own backdrop.
            Rect markerRect = new Rect(position.x * 0.1f, position.y + 1f, position.x * 0.25f, position.height - 1f);
            EditorGUI.DrawRect(markerRect, ActiveMarkerColor);
        }

        // "Element 0" is meaningless when scanning a long playlist -- show the walk type instead.
        string niceTypeName = ObjectNames.NicifyVariableName(walkType.ToString());
        GUIContent elementLabel = new GUIContent(elementIndex >= 0 ? $"{niceTypeName} (Segment {elementIndex})" : niceTypeName);

        Rect foldoutRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, elementLabel, true);

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            float y = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            foreach (string fieldName in VisibleFields(property))
            {
                SerializedProperty fieldProp = property.FindPropertyRelative(fieldName);
                float h = EditorGUI.GetPropertyHeight(fieldProp, true);
                EditorGUI.PropertyField(new Rect(position.x, y, position.width, h), fieldProp, true);
                y += h + EditorGUIUtility.standardVerticalSpacing;
            }

            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;

        if (property.isExpanded)
        {
            foreach (string fieldName in VisibleFields(property))
            {
                SerializedProperty fieldProp = property.FindPropertyRelative(fieldName);
                height += EditorGUI.GetPropertyHeight(fieldProp, true) + EditorGUIUtility.standardVerticalSpacing;
            }
        }

        return height;
    }

    static System.Collections.Generic.IEnumerable<string> VisibleFields(SerializedProperty property)
    {
        var walkType = (AntWalkBuilder.WalkType)property.FindPropertyRelative("type").enumValueIndex;

        foreach (string f in CommonFields) yield return f;
        foreach (string f in TypeSpecificFields(walkType)) yield return f;
    }

    /// <summary>Parses the array index (e.g. 2) out of a property path like "playlist.Array.data[2]".</summary>
    static int GetElementIndex(SerializedProperty property)
    {
        string path = property.propertyPath;
        int start = path.LastIndexOf('[');
        int end = path.LastIndexOf(']');
        if (start >= 0 && end > start && int.TryParse(path.Substring(start + 1, end - start - 1), out int index))
        {
            return index;
        }
        return -1;
    }
}
