using UnityEditor;

/// <summary>
/// Without a custom Editor, Unity's default Inspector renders the <c>playlist</c> list with a
/// UI Toolkit <c>ListView</c>, which caps visible rows and scrolls internally instead of letting
/// the Inspector grow. Forcing classic IMGUI via <see cref="DrawDefaultInspector"/> avoids that --
/// the whole list is drawn inline (still using <see cref="PathDefinitionDrawer"/> per element), and
/// only the containing Inspector panel scrolls once it's taller than the window.
/// </summary>
[CustomEditor(typeof(AntWalkBuilder))]
public class AntWalkBuilderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
    }
}
