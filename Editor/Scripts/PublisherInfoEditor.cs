using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace Challenges
{   
    [CustomEditor(typeof(PublisherInfo))]
    public class PublisherInfoEditor : Editor
    {
        private static Object previousSelection;
        public const string prefPrefix = "ChallengesUpdater";

        public static void Open(PublisherInfo.Publish onPublish)
        {
            PublisherInfo info = ScriptableObject.CreateInstance<PublisherInfo>();
            previousSelection = Selection.activeObject;
            info.onPublish = onPublish;
            Selection.activeObject = info;
        }

        private void OnEnable()
        {
            PublisherInfo info = target as PublisherInfo;
            info.repository = EditorPrefs.GetString($"{prefPrefix}_Repository");
        }

        public override VisualElement CreateInspectorGUI()
        {
            PublisherInfo info = target as PublisherInfo;
            VisualElement root = new VisualElement();

            root.Add(new PropertyField(serializedObject.FindProperty(nameof(info.repository))));

            VisualElement commands = new VisualElement();
            commands.style.height = 20;
            commands.style.flexDirection = FlexDirection.Row;
            commands.style.marginTop = 5;
            commands.style.justifyContent = Justify.FlexEnd;
            root.Add(commands);

            Button validateBtn = new Button();
            validateBtn.text = "Publish";
            validateBtn.clicked += Validate;
            commands.Add(validateBtn);

            Button cancelBtn = new Button();
            cancelBtn.text = "Cancel";
            cancelBtn.clicked += Cancel;
            commands.Add(cancelBtn);

            return root;
        }

        private void Validate()
        {
            PublisherInfo info = target as PublisherInfo;
            EditorPrefs.SetString($"{prefPrefix}_Repository", info.repository);
            Selection.activeObject = previousSelection;
            if (info.onPublish != null)
                info.onPublish.Invoke();
        }

        private void Cancel()
        {
            Selection.activeObject = previousSelection;
        }

    }
}
