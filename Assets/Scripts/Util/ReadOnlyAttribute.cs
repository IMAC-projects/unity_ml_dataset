// see:
// https://www.patrykgalach.com/2020/01/20/readonly-attribute-in-unity-editor/

using UnityEditor;
using UnityEngine;

namespace Util
{

    /**
     * An Attribute which prevents a serialised field
     * to be changed in the Unity Editor.
     */
    public class ReadOnlyAttribute : PropertyAttribute
    {
        
    }
    
    [CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
    class ReadOnlyDrawer : PropertyDrawer
    {
        public override void OnGUI(
            Rect position, SerializedProperty property, GUIContent label)
        {
            var previous = GUI.enabled;
            
            // Disable edit for the field.
            GUI.enabled = false;
            
            // Display the field.
            EditorGUI.PropertyField(position, property, label);

            GUI.enabled = previous;
        }
    }
}