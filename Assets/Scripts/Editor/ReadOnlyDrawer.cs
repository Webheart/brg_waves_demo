using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(RuntimeReadOnlyAttribute))]
public class ReadOnlyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        bool prevEnabled = GUI.enabled;
        GUI.enabled = !Application.isPlaying;
        EditorGUI.PropertyField(position, property, label);
        GUI.enabled = prevEnabled;
    }
}