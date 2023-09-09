using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Challenges
{
    [InitializeOnLoad]
    public static class TutoPack_Selector
    {
        private static bool requestUpdater;
        private const string lastTutoPrefKey = "Challenges_LastTuto";
        public const string autoSelectPrefKey = "Challenges_AutoSelect";
        public const string lastTeacherPrefKey = "Challenges_Teacher";

        static TutoPack_Selector()
        {
            if (EditorPrefs.GetBool(autoSelectPrefKey, true))
            {
                EnableAutoSelection();
                requestUpdater = true;
            }
        }

        private static void EnableAutoSelection()
        {
            Selection.selectionChanged += OnSelectionChange;
            EditorApplication.update += EditorUpdate;
        }

        private static void DisableAutoSelection()
        {
            Selection.selectionChanged -= OnSelectionChange;
            EditorApplication.update -= EditorUpdate;
        }

        private static void OnSelectionChange()
        {
            switch (Selection.objects.Length)
            {
                case 0:
                    requestUpdater = true;
                    break;

                case 1:
                    switch (Selection.activeObject)
                    {
                        case TutoPack tutoPack:
                            string path = AssetDatabase.GetAssetPath(tutoPack);
                            string guid = AssetDatabase.AssetPathToGUID(path);
                            EditorPrefs.SetString(lastTutoPrefKey, guid);
                            break;

                        case Updater updater:
                            EditorPrefs.SetString(lastTutoPrefKey, "");
                            break;
                    }
                    break;
            }
        }

        private static void EditorUpdate()
        {
            if (requestUpdater)
            {
                requestUpdater = false;
                string guid = EditorPrefs.GetString(lastTutoPrefKey);

                if (string.IsNullOrEmpty(guid))
                {
                    Updater.Open();
                }
                else
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    TutoPack pack = AssetDatabase.LoadAssetAtPath<TutoPack>(path);

                    if (pack != null)
                    {
                        VirtualChallenge virtualTutoPack = ScriptableObject.CreateInstance<VirtualChallenge>();
                        virtualTutoPack.pack = pack;
                        Selection.activeObject = virtualTutoPack;
                    }
                }
            }
        }

        public static void ToggleAutoSelect()
        {
            bool autoSelect = !EditorPrefs.GetBool(TutoPack_Selector.autoSelectPrefKey, true);
            EditorPrefs.SetBool(TutoPack_Selector.autoSelectPrefKey, autoSelect);

            if (autoSelect)
                EnableAutoSelection();
            else
                DisableAutoSelection();
        }
    }
}