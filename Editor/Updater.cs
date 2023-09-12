using System.Collections;
using System.Reflection;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using UnityEditor.PackageManager;
using System.Diagnostics;
using UnityEditor.PackageManager.Requests;
using System.Runtime.CompilerServices;
using UnityPackage = UnityEditor.PackageManager.PackageInfo;
using Debug = UnityEngine.Debug;

namespace Challenges
{
    public class Updater : ScriptableObject
    {
        public const string gitOwner = "Niwala";
        public const string gitRepo = "Challenges";
        public static string gitUpdaterUrl = $@"https://github.com/{gitOwner}/ChallengesUpdater.git";
        public static string gitDownloadUrl = @$"https://raw.githubusercontent.com//{gitOwner}/{gitRepo}/main";
        public static string gitContentUrl = @$"https://api.github.com/repos/{gitOwner}/{gitRepo}/contents";
        public static string gitBlobUrl = @$"https://github.com/{gitOwner}/{gitRepo}/blob/main";
        public static string gitToken = "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";

        [MenuItem("Challenges/Updater &U")]
        public static void Open()
        {
            Updater instance = ScriptableObject.CreateInstance<Updater>();
            Selection.activeObject = instance;
        }

        [InitializeOnLoadMethod]
        public static void RegisterDelayOpen()
        {
            if (EditorPrefs.GetBool("Updater_AutoOpen", false))
                EditorApplication.update += DelayOpen;
            Selection.selectionChanged += OnSelectionChange;
        }

        private static void DelayOpen()
        {
            EditorApplication.update -= DelayOpen;
            Open();
        }

        private static void OnSelectionChange()
        {
            EditorPrefs.SetBool("Updater_AutoOpen", Selection.activeObject is Updater);
        }
    }

    [CustomEditor(typeof(Updater))]
    public class Updater_Editor : Editor
    {
        private string teacher;
        private string search;
        private static List<Status> challengeList = new List<Status>();
        private static List<Status> filteredChallengeList = new List<Status>();

        private static UpdaterStatus updaterStatus;
        private static string updaterErrorTitle;
        private static string updaterErrorMessage;

        private static Texture2D previewMask;
        private static Shader previewShader;
        private static Material previewMaterial;

        //Styles
        private GUIStyle titleStyle;
        private GUIStyle centredtitleStyle;
        private GUIStyle descriptionStyle;
        private GUIContent validIcon;
        private GUIContent minorUpdateIcon;
        private GUIContent majorUpdateIcon;
        private GUIContent newIcon;
        private GUIContent depreciatedIcon;
        private GUIContent refreshIcon;
        private bool stylesLoaded;

        private string cacheDir { get => "Temp/ChallengesUpdater"; }

        private static string onlineVersion;
        private static string localVersion;


        //Ressources

        private void OnEnable()
        {
            teacher = EditorPrefs.GetString(TutoPack_Selector.lastTeacherPrefKey, "");

            LoadResources();
            LoadCache();
            CheckUpdaterVersion();
        }

        private void OnDisable()
        {
            DestroyImmediate(target);
        }

        public static UpdaterInfo GetCurrentPackage()
        {
            string filePath = GetFilePath();
            filePath = filePath.Replace("Editor\\Updater.cs", "package.json");
            UpdaterInfo updaterInfo = JsonUtility.FromJson<UpdaterInfo>(File.ReadAllText(filePath));
            return updaterInfo;
        }

        public static void CheckUpdaterVersion()
        {
            ChallengesUpdaterIsOutdated((bool b) => { updaterStatus = b ? UpdaterStatus.Outdated : UpdaterStatus.Valid; });
        }

        public static void SendGitFiles(string workspace, params string[] files)
        {
            Process cmd = new Process();
            ProcessStartInfo info = new ProcessStartInfo();
            info.FileName = "cmd.exe";
            info.RedirectStandardInput = true;
            info.RedirectStandardOutput = true;
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            info.WindowStyle = ProcessWindowStyle.Hidden;
            info.WorkingDirectory = workspace;

            cmd.StartInfo = info;
            cmd.Start();

            using (StreamWriter sw = cmd.StandardInput)
            {
                if (sw.BaseStream.CanWrite)
                {
                    sw.WriteLine($"git checkout main");
                    sw.WriteLine($"git fetch origin main");
                    sw.WriteLine($"git rebase -i origin/main");

                    for (int i = 0; i < files.Length; i++)
                        sw.WriteLine($"git add {files[i]}");

                    sw.WriteLine($"git status");
                    sw.WriteLine($"git commit -am \"Publish Updater Index\"");
                    sw.WriteLine($"git push origin main");
                }
            }

            cmd.WaitForExit();
            Debug.Log("Updater Git Status\n" + cmd.StandardOutput.ReadToEnd());
        }

        private static void DownloadUpdaterIndex(System.Action<UpdaterInfo> onUpdaterIndexDownloaded)
        {
            //Set loading status
            updaterStatus = UpdaterStatus.Loading;

            //Download project infos
            string uri = $"{Updater.gitDownloadUrl}/Index.json";
            DownloadFile(uri, (DownloadHandler handler) =>
            {
                if (onUpdaterIndexDownloaded != null)
                    onUpdaterIndexDownloaded.Invoke(JsonUtility.FromJson<UpdaterInfo>(handler.text));
            });
        }

        private static void ChallengesUpdaterIsOutdated(System.Action<bool> outdated)
        {
            //Check updater version
            UnityPackage currentPackage = UnityPackage.FindForAssembly(Assembly.GetExecutingAssembly());

            //The plugin exist in the assets and not the package manager.
            //So it should always be up to date or updated by git and not the package manager.
            if (currentPackage == null)
            {
                localVersion = GetCurrentPackage().version;
                outdated.Invoke(false);
                return;
            }
            localVersion = currentPackage.version;

            //We need to download the index to find out the latest version of the plugin.
            DownloadUpdaterIndex(OnUpdaterIndexDownloaded);

            void OnUpdaterIndexDownloaded(UpdaterInfo updaterInfo)
            {
                onlineVersion = updaterInfo.version.ToString();
                bool isOutdated = OutDated(localVersion, onlineVersion);
                if (isOutdated)
                {
                    Debug.Log($"the Challenges Updater package version differs from the online version\n" +
                        $"Online Version : {updaterInfo.version}\n" +
                        $"Package Version : {currentPackage.version}");
                }
                outdated.Invoke(isOutdated);
            }
        }

        private static void UpdateTheUpdater()
        {
            //Check if the plugin exist in package (and not in the project assets)
            string filePath = GetFilePath().Replace('\\', '/');
            bool pluginIsInPackage = !filePath.StartsWith(Application.dataPath);

            //We don't want to add the package if the plugin is present in the Assets
            if (!pluginIsInPackage)
            {
                Debug.LogError("The plugin is not recognised as a package, and therefore cannot be updated.");
                return;
            }

            //Send the request to the package manager 
            EditorUtility.DisplayProgressBar("Challenges Updater", "Downloading the package...", 0.0f);
            Client.Add(Updater.gitUpdaterUrl);
            EditorUtility.ClearProgressBar();
        }

        private static void LoadResources()
        {
            //Get data directory
            string dataPath = GetFilePath().Replace($"{nameof(Updater)}.cs", "Data\\");

            //Plugin is in Assets
            if (dataPath.StartsWith(Application.dataPath.Replace('/', '\\')))
            {
                dataPath = dataPath.Remove(0, Application.dataPath.Length - 6);
            }

            //Plugin is in Package Cache
            else
            {
                dataPath = "Packages\\com.niwala.challengesupdater\\Editor\\Data\\";
            }

            if (previewMask == null)
                previewMask = AssetDatabase.LoadAssetAtPath<Texture2D>(dataPath + "PreviewMask.png");

            if (previewShader == null)
                previewShader = AssetDatabase.LoadAssetAtPath<Shader>(dataPath + "PreviewShader.shader");

            if (previewMaterial == null)
                previewMaterial = new Material(previewShader);

            previewMaterial.SetTexture("_Mask", previewMask);
        }

        private static string GetFilePath([CallerFilePath] string sourceFilePath = "")
        {
            return sourceFilePath;
        }

        private void LoadStyles()
        {
            titleStyle = new GUIStyle(EditorStyles.largeLabel);
            titleStyle.fontSize = 20;

            centredtitleStyle = new GUIStyle(titleStyle);
            centredtitleStyle.alignment = TextAnchor.MiddleCenter;

            descriptionStyle = new GUIStyle(EditorStyles.largeLabel);
            descriptionStyle.wordWrap = true;

            validIcon = EditorGUIUtility.IconContent("d_Progress@2x");
            validIcon.tooltip = "All good";
            minorUpdateIcon = EditorGUIUtility.IconContent("d_console.warnicon.inactive.sml@2x");
            minorUpdateIcon.tooltip = "There is a minor update available. It should not affect your data";
            majorUpdateIcon = EditorGUIUtility.IconContent("d_console.warnicon");
            majorUpdateIcon.tooltip = "There is a major update available. It may affect your data";
            newIcon = EditorGUIUtility.IconContent("Download-Available@2x");
            newIcon.tooltip = "New challenge!";
            depreciatedIcon = EditorGUIUtility.IconContent("d_console.erroricon");
            depreciatedIcon.tooltip = "This challenge is deprecated, it will not be updated anymore.";
            refreshIcon = EditorGUIUtility.IconContent("Refresh@2x");
            stylesLoaded = true;
        }

        public override bool UseDefaultMargins()
        { return false; }

        protected override void OnHeaderGUI()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);

            //Teacher
            Rect rect = GUILayoutUtility.GetRect(200, EditorGUIUtility.singleLineHeight);
            string teacherContent = string.IsNullOrEmpty(teacher) ? "Teacher : All" : $"Teacher : {teacher.Replace("/", " › ")}";

            if (GUI.Button(rect, teacherContent, EditorStyles.toolbarDropDown))
            {
                GenericMenu menu = new GenericMenu();

                HashSet<string> teachers = new HashSet<string>();
                foreach (var item in challengeList)
                {
                    if (!teachers.Contains(item.teacher))
                        teachers.Add(item.teacher);
                }

                menu.AddItem(new GUIContent("All"), string.IsNullOrEmpty(teacher), () => { SelectTeacher(""); });
                menu.AddSeparator("");
                foreach (var item in teachers)
                {
                    menu.AddItem(new GUIContent(item), teacher == item, () => { SelectTeacher(item); });
                }

                menu.DropDown(rect);

                void SelectTeacher(string value)
                {
                    teacher = value;
                    EditorPrefs.SetString(TutoPack_Selector.lastTeacherPrefKey, value);
                    UpdateFilteredStatus();
                }
            }


            //Search bar
            rect = GUILayoutUtility.GetRect(Screen.width - 244, EditorGUIUtility.singleLineHeight);
            rect.Set(rect.x + 4, rect.y + 2, rect.width, rect.height);
            string newSearch = EditorGUI.TextField(rect, search, EditorStyles.toolbarSearchField);
            if (newSearch != search)
            {
                search = newSearch;
                UpdateFilteredStatus();
            }


            GUILayout.FlexibleSpace();


            //Settings
            rect = GUILayoutUtility.GetRect(22, EditorGUIUtility.singleLineHeight);
            if (GUI.Button(rect, EditorGUIUtility.IconContent("d_icon dropdown"), EditorStyles.toolbarButton))
            {
                bool autoSelect = EditorPrefs.GetBool(TutoPack_Selector.autoSelectPrefKey, true);

                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("Refresh"), false, () => { UpdateCache(); });

                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Auto Select"), autoSelect, () => { TutoPack_Selector.ToggleAutoSelect(); });

                if (Event.current.shift)
                {
                    menu.AddSeparator("");
                    menu.AddDisabledItem(new GUIContent("⠀"));
                    menu.AddDisabledItem(new GUIContent("Dev area"));
                    menu.AddItem(new GUIContent("Export current Updater version"), false, () => { GenerateUpdaterInfo(false); });
                    menu.AddItem(new GUIContent("Export a new Updater version"), false, () => { GenerateUpdaterInfo(true); });
                    menu.AddItem(new GUIContent("Generate Readme File"), false, () => { GenerateReadme(); });
                    menu.AddItem(new GUIContent("Export all challenges"), false, () => { GenerateUpdaterInfo(false); ExportAllChallenges(); });
                }

                menu.DropDown(rect);
            }
            GUILayout.EndHorizontal();
        }

        private void ExportAllChallenges()
        {
            string[] guids = AssetDatabase.FindAssets("t:TutoPack");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                TutoPack tutoPack = AssetDatabase.LoadAssetAtPath<TutoPack>(path);
                TutoPack_Editor.Export(tutoPack);
            }
        }

        public override void OnInspectorGUI()
        {
            if (!stylesLoaded)
                LoadStyles();

            //if (updateAvailable)
            DrawUpdateAvailableGUI();

            EditorGUI.BeginDisabledGroup(updaterStatus == UpdaterStatus.Outdated);
            for (int i = 0; i < filteredChallengeList.Count; i++)
                DrawChallengeGUI(filteredChallengeList[i]);
            EditorGUI.EndDisabledGroup();

            Repaint();
        }

        private void DrawUpdateAvailableGUI()
        {
            Rect rect = GUILayoutUtility.GetRect(200.0f, 80.0f);
            rect.Set(rect.x + 10, rect.y + 5, rect.width - 20, rect.height - 10);

            bool focus = rect.Contains(Event.current.mousePosition);
            if (focus)
            {
                GUI.color = new Color(0.3f, 0.8f, 1.6f, 1.0f);
                GUI.Box(rect, "", EditorStyles.helpBox);
                GUI.color = Color.white;
            }

            Rect titleRect = new Rect(rect.x + 40, rect.y + 10, rect.width - 10, 26);
            Rect descriptionRect = new Rect(rect.x + 12, rect.y + 40, rect.width - 100, rect.height - 40);
            Rect buttonRect = new Rect(rect.x + rect.width - 90, rect.y + rect.height - 25, 85, 20);

            switch (updaterStatus)
            {
                case UpdaterStatus.Valid:

                    titleRect = new Rect(rect.x + 10, rect.y + 10, rect.width - 10, 26);
                    buttonRect = new Rect(rect.x + rect.width - 190, rect.y + rect.height - 25, 185, 20);

                    EditorGUI.HelpBox(rect, "", MessageType.None);
                    GUI.Label(titleRect, "Updater", titleStyle);
                    GUI.Label(descriptionRect, localVersion, descriptionStyle);
                    if (GUI.Button(buttonRect, "Check updates"))
                    {
                        CheckUpdaterVersion();
                        UpdateCache();
                    }
                    break;

                case UpdaterStatus.Loading:
                    EditorGUI.HelpBox(rect, "", MessageType.None);
                    rect.Set(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2);
                    GUI.BeginClip(rect);

                    Rect lineRect = new Rect(1, rect.height - 3, rect.width - 2, 3);
                    Rect barRect = new Rect(0, lineRect.y, 100, lineRect.height);

                    float progress = (float)((EditorApplication.timeSinceStartup * .5) % 1);
                    barRect.x = lineRect.x - 100 + progress * (lineRect.width + 100);

                    EditorGUI.DrawRect(lineRect, new Color(0.0f, 0.0f, 0.0f, 0.7f));
                    EditorGUI.DrawRect(barRect, new Color(0.4f, 0.8f, 1.0f, 0.7f));
                    GUI.EndClip();


                    GUI.Label(rect, "Loading...", centredtitleStyle);
                    break;

                case UpdaterStatus.Outdated:
                    EditorGUI.HelpBox(rect, "", MessageType.Warning);
                    GUI.Label(titleRect, "Update available", titleStyle);
                    GUI.Label(descriptionRect, $"Local : {localVersion} Online : {onlineVersion}\n(Does not influence the challenges)", descriptionStyle);
                    if (GUI.Button(buttonRect, "Apply"))
                        UpdateTheUpdater();
                    break;

                case UpdaterStatus.Error:
                    EditorGUI.HelpBox(rect, "", MessageType.Error);
                    GUI.Label(titleRect, updaterErrorTitle, titleStyle);
                    GUI.Label(descriptionRect, updaterErrorMessage, descriptionStyle);
                    if (GUI.Button(buttonRect, "Refresh"))
                        UpdateCache();
                    break;
            }
        }

        public static bool OutDated(string localVersion, string onlineVersion)
        {
            string[] lv = localVersion.Split('.');
            string[] ov = onlineVersion.Split('.');

            for (int i = 0; i < 3; i++)
                if (int.Parse(lv[i]) < int.Parse(ov[i]))
                    return true;
            return false;
        }

        private void DrawChallengeGUI(Status status)
        {
            string name = status.name;

            Rect rect = GUILayoutUtility.GetRect(200.0f, 150.0f);
            rect.Set(rect.x + 10, rect.y + 5, rect.width - 20, rect.height - 10);

            Rect btnRect = new Rect(rect.x + rect.width - 42, rect.y + 6, 36, 36);
            int openMenu = GUI.Button(btnRect, "", GUIStyle.none) ? 1 : 0;

            bool focus = rect.Contains(Event.current.mousePosition);
            if (focus)
            {
                GUI.color = new Color(0.3f, 0.8f, 1.6f, 1.0f);
                GUI.Box(rect, "", EditorStyles.helpBox);
                GUI.color = Color.white;
            }
            EditorGUIUtility.AddCursorRect(btnRect, MouseCursor.Link);

            if (GUI.Button(rect, "", EditorStyles.helpBox))
            {
                if (Event.current.button == 0)
                {
                    if (status.status != ChallengeStatus.New)
                        OpenChallenge(name);
                }
                else
                {
                    openMenu = 2;
                }
            }

            if (status.preview != null)
            {
                Rect previewRect = new Rect(rect.x + rect.width - (rect.height - 1) * 2 - 1, rect.y + 1, (rect.height - 1) * 2, rect.height - 2);
                EditorGUI.DrawPreviewTexture(previewRect, status.preview, previewMaterial);
            }


            Rect titleRect = new Rect(rect.x + 5, rect.y + 5, rect.width - 10, 24);
            GUI.Label(titleRect, ObjectNames.NicifyVariableName(name), titleStyle);

            Rect descriptionRect = new Rect(rect.x + 15, rect.y + 35, rect.width - rect.height * 1.5f, rect.height - 35);
            GUI.Label(descriptionRect, status.description, descriptionStyle);

            DrawStatus(btnRect, status.status);

            if (openMenu > 0)
            {
                GenericMenu menu = new GenericMenu();

                menu.AddDisabledItem(new GUIContent($"Status : {ObjectNames.NicifyVariableName(status.status.ToString())}"));
                menu.AddSeparator("");

                switch (status.status)
                {
                    case ChallengeStatus.Good:
                        menu.AddItem(new GUIContent("Open"), false, () => OpenChallenge(name));
                        menu.AddSeparator("");
                        menu.AddItem(new GUIContent("Remove"), false, () => DeleteChallenge(name, true));
                        menu.AddItem(new GUIContent("Reimport"), false, () => ReimportChallenge(name));
                        break;
                    case ChallengeStatus.MinorUpdate:
                        menu.AddItem(new GUIContent("Open"), false, () => OpenChallenge(name));
                        menu.AddSeparator("");
                        menu.AddItem(new GUIContent("Remove"), false, () => DeleteChallenge(name, true));
                        menu.AddItem(new GUIContent("Reimport"), false, () => ReimportChallenge(name));
                        menu.AddSeparator("");
                        menu.AddItem(new GUIContent("Update"), false, () => UpdateChallenge(name, false));
                        break;
                    case ChallengeStatus.MajorUpdate:
                        menu.AddItem(new GUIContent("Open"), false, () => OpenChallenge(name));
                        menu.AddSeparator("");
                        menu.AddItem(new GUIContent("Remove"), false, () => DeleteChallenge(name, true));
                        menu.AddItem(new GUIContent("Reimport"), false, () => ReimportChallenge(name));
                        menu.AddSeparator("");
                        menu.AddItem(new GUIContent("Update"), false, () => { UpdateChallenge(name, true); });
                        break;
                    case ChallengeStatus.New:
                        menu.AddDisabledItem(new GUIContent("Open"), false);
                        menu.AddItem(new GUIContent("Download"), false, () => DownloadChallenge(name));
                        break;
                    case ChallengeStatus.Deprecated:
                        menu.AddItem(new GUIContent("Open"), false, () => OpenChallenge(name));
                        menu.AddSeparator("");
                        menu.AddItem(new GUIContent("Remove"), false, () => DeleteChallenge(name, true));
                        break;
                }

                if (openMenu == 1)
                    menu.DropDown(btnRect);
                else
                    menu.ShowAsContext();
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                LoadCache();
            }
        }

        private void DrawStatus(Rect rect, ChallengeStatus status)
        {
            switch (status)
            {
                case ChallengeStatus.Good:
                    GUI.Label(rect, validIcon);
                    break;
                case ChallengeStatus.MinorUpdate:
                    GUI.Label(rect, minorUpdateIcon);
                    break;
                case ChallengeStatus.MajorUpdate:
                    GUI.Label(rect, majorUpdateIcon);
                    break;
                case ChallengeStatus.New:
                    GUI.Label(rect, newIcon);
                    break;
                case ChallengeStatus.Deprecated:
                    GUI.Label(rect, depreciatedIcon);
                    break;
            }
        }

        private void ReimportChallenge(string name)
        {
            switch (EditorUtility.DisplayDialogComplex("Reimport Challenge",
                "This will overwrite existing assets and erase your progress on the challenge. Are you sure you want to continue?",
                "Yes", "No", "Manual import"))
            {
                case 0:
                    DeleteChallenge(name);
                    DownloadChallenge(name, false);
                    break;
                case 1:
                    return;
                case 2:
                    DownloadChallenge(name, true);
                    break;
            }
        }

        private void UpdateChallenge(string name, bool needConfirmation = false)
        {
            if (needConfirmation)
            {
                ReimportChallenge(name);
            }
            else
            {
                DownloadChallenge(name, false);
            }
        }

        private void DeleteChallenge(string name, bool needConfirmation = false)
        {
            if (needConfirmation)
            {
                if (EditorUtility.DisplayDialog($"Remove {name} challenge",
                    "This operation will erase all your data on the challenge and cannot be undone.",
                    "Remove", "Cancel"))
                    Delete();
            }
            else
            {
                Delete();
            }

            void Delete()
            {
                string path = TutoPack_Editor.GetDirectory(name);

                if (!string.IsNullOrEmpty(path))
                {
                    try
                    {
                        Directory.Delete(path, true);
                        File.Delete($"{path}.meta");
                    }
                    catch { }
                    AssetDatabase.Refresh();
                }

                LoadCache();
            }
        }

        private static void DownloadFile(string uri, System.Action<DownloadHandler> callback)
        {
            UnityWebRequest request = UnityWebRequest.Get(uri);
            request.SetRequestHeader("authorization", Updater.gitToken);
            request.SendWebRequest().completed += WebRequestCompleted;

            void WebRequestCompleted(AsyncOperation obj)
            {
                UnityWebRequest request = (obj as UnityWebRequestAsyncOperation).webRequest;

                if (LogErrorIfAny(request))
                    return;

                string[] pages = uri.Split('/');
                int page = pages.Length - 1;
                callback.Invoke(request.downloadHandler);
            }
        }

        private void DownloadPackage(string uri, bool manualImport = false)
        {
            DownloadFile(uri, (DownloadHandler handler) =>
            {
                byte[] bytes = handler.data;
                string path = Application.dataPath.Remove(Application.dataPath.Length - 6) + "/Temp/";
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                File.WriteAllBytes(path + "Challenge.unitypackage", bytes);
                AssetDatabase.importPackageCompleted += ImportPackageCompleted;
                AssetDatabase.importPackageFailed += ImportPackageFailed;
                AssetDatabase.importPackageCancelled += ImportPackageCancelled;
                AssetDatabase.ImportPackage(path + "Challenge.unitypackage", manualImport);
            });
        }

        private void DownloadChallenge(string name, bool manualImport = false)
        {
            DownloadPackage($"{Updater.gitDownloadUrl}/Challenges/{name}.unitypackage", manualImport);
        }

        private void ImportPackageCompleted(string packageName)
        {
            AssetDatabase.importPackageCompleted -= ImportPackageCompleted;
            AssetDatabase.importPackageFailed -= ImportPackageFailed;
            AssetDatabase.importPackageCancelled -= ImportPackageCancelled;
            LoadCache();
        }

        private void ImportPackageFailed(string packageName, string error)
        {
            AssetDatabase.importPackageCompleted -= ImportPackageCompleted;
            AssetDatabase.importPackageFailed -= ImportPackageFailed;
            AssetDatabase.importPackageCancelled -= ImportPackageCancelled;
            LoadCache();
        }

        private void ImportPackageCancelled(string packageName)
        {
            AssetDatabase.importPackageCompleted -= ImportPackageCompleted;
            AssetDatabase.importPackageFailed -= ImportPackageFailed;
            AssetDatabase.importPackageCancelled -= ImportPackageCancelled;
            LoadCache();
        }

        private void OpenChallenge(string name)
        {
            string[] tutoPackGUIDs = AssetDatabase.FindAssets("t:TutoPack");
            TutoPack pack = tutoPackGUIDs.ToList().
                Select(x => AssetDatabase.LoadAssetAtPath<TutoPack>(AssetDatabase.GUIDToAssetPath(x))).
                FirstOrDefault(x => x.name == name);
            Selection.activeObject = pack;

            Scene existingScene = EditorSceneManager.GetSceneByName(name);
            if (existingScene.name == name)
            {
                return;
            }

            if (pack.scene != null && pack.scene is SceneAsset scene)
            {
                EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
                EditorSceneManager.OpenScene(AssetDatabase.GetAssetPath(pack.scene), OpenSceneMode.Single);
            }

            if (!string.IsNullOrEmpty(pack.onOpen))
            {
                string typeName = pack.onOpen.Split('.')[0];
                string methodName = pack.onOpen.Split('.')[1];

                System.Type[] types = Assembly.GetExecutingAssembly().GetTypes();
                for (int i = 0; i < types.Length; i++)
                {
                    if (types[i].Name != typeName)
                        continue;

                    MethodInfo[] methods = types[i].GetMethods(BindingFlags.Static | BindingFlags.Public);
                    for (int j = 0; j < methods.Length; j++)
                    {
                        if (methods[j].Name == methodName)
                        {
                            methods[j].Invoke("", new object[0]);
                            break;
                        }
                    }
                }
            }
        }

        private void PingChallenge(string name)
        {
            string[] tutoPackGUIDs = AssetDatabase.FindAssets("t:TutoPack");
            EditorGUIUtility.PingObject(tutoPackGUIDs.ToList().
                Select(x => AssetDatabase.LoadAssetAtPath<TutoPack>(AssetDatabase.GUIDToAssetPath(x))).
                FirstOrDefault(x => x.name == name));
        }

        private void GenerateUpdaterInfo(bool incrementVersion)
        {
            //Load info from package json file (Unity formating)
            string filePath = GetFilePath();
            if (!filePath.Contains("\\Assets\\"))
            {
                Debug.LogWarning("The plugin in package form is not authorised to send new versions.");
                return;
            }
            filePath = filePath.Replace("Editor\\Updater.cs", "package.json");
            UpdaterInfo updaterInfo = JsonUtility.FromJson<UpdaterInfo>(File.ReadAllText(filePath));


            //Update package index if needed
            if (incrementVersion)
            {
                string version = updaterInfo.version;
                string[] parts = version.Split('.');
                int revision = int.Parse(parts[2]);
                revision++;
                version = $"{parts[0]}.{parts[1]}.{revision}";
                updaterInfo.version = version;
                localVersion = version;
                File.WriteAllText(filePath, JsonUtility.ToJson(updaterInfo, true));
            }


            //Update package info in the challenges repository
            string file = JsonUtility.ToJson(updaterInfo, true);
            string indexPath = new DirectoryInfo(Application.dataPath).Parent.Parent.FullName + "\\Repository\\";
            if (!Directory.Exists(indexPath))
                Directory.CreateDirectory(indexPath);
            File.WriteAllText(indexPath + "Index.json", file);


            //Publish the new updater version
            EditorUtility.DisplayProgressBar("Challenges Updater", "Push the Updater files", 0.0f);
            string updaterWorkspace = new DirectoryInfo(GetFilePath()).Parent.FullName;
            string[] updaterFiles = Directory.GetFiles(updaterWorkspace, "*", SearchOption.AllDirectories).
                Where(x => !x.Contains("\\Challenges\\") && !x.Contains(".git")).ToArray();
            SendGitFiles(updaterWorkspace, updaterFiles);


            //Publish the new index
            EditorUtility.DisplayProgressBar("Challenges Updater", "Publish the new index", 0.5f);
            string challengesWorkspace = new DirectoryInfo(indexPath).FullName;
            SendGitFiles(challengesWorkspace, indexPath);

            EditorUtility.ClearProgressBar();
        }

        private void GenerateReadme()
        {
            string[] tutoPackGUIDs = AssetDatabase.FindAssets("t:TutoPack");
            List<TutoPack> packs = tutoPackGUIDs.ToList().
                Select(x => AssetDatabase.LoadAssetAtPath<TutoPack>(AssetDatabase.GUIDToAssetPath(x))).
                Where(x => x != null).ToList();

            ChallengeInfo[] infos = packs.ToList().Select(x => new ChallengeInfo(x)).ToArray();
            string file = "";

            //Repo directory
            string directory = Application.dataPath.Remove(Application.dataPath.Length - 6) + "../Repository/";
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            for (int i = 0; i < infos.Length; i++)
            {
                if (infos[i].hidden)
                    continue;

                file += "\n## " + infos[i].name + "\n";
                file += $"- Teacher : {infos[i].teacher}\n";
                file += $"- Hash : {infos[i].hash}\n";
                file += $"```\n{infos[i].description}\n```\n";

                //Add image link to readme
                if (packs[i].preview != null)
                    file += $"![](/Challenges/{packs[i].name}.jpg)\n";

                file += "\n";
            }

            File.WriteAllText(directory + "Readme.md", file);
        }

        private void CheckCacheDirectories()
        {
            if (!Directory.Exists(cacheDir))
                Directory.CreateDirectory(cacheDir);
        }

        private void LoadCache()
        {
            CheckCacheDirectories();


            //Check project update
            //UpdaterInfo projectInfo = default;
            //if (File.Exists($"{cacheDir}/UpdaterInfo.json"))
            //{
            //    string projectInfoFile = File.ReadAllText($"{cacheDir}/UpdaterInfo.json");
            //    projectInfo = JsonUtility.FromJson<UpdaterInfo>(projectInfoFile);
            //}
            //if (projectInfo.version > Updater.projectVersion)
            //    updaterStatus = UpdaterStatus.Outdated;


            //Read all challenge infos
            Dictionary<string, ChallengeInfo> infos = new Dictionary<string, ChallengeInfo>();
            string[] files = Directory.GetFiles(cacheDir);
            for (int i = 0; i < files.Length; i++)
            {
                string challengeName = files[i].Split('.')[0];

                if (!infos.ContainsKey(challengeName))
                    infos.Add(challengeName, default);

                if (files[i].EndsWith(".json"))
                {
                    ChallengeInfo info = infos[challengeName];
                    Texture2D preview = info.preview;
                    info = JsonUtility.FromJson<ChallengeInfo>(File.ReadAllText(files[i]));
                    info.preview = preview;
                    infos[challengeName] = info;
                }
                else if (files[i].EndsWith(".jpg"))
                {
                    ChallengeInfo info = infos[challengeName];
                    info.preview = new Texture2D(2, 2);
                    info.preview.LoadImage(File.ReadAllBytes(files[i]));
                    infos[challengeName] = info;
                }
            }

            //Reorder infos
            List<ChallengeInfo> sortedChallenges = new List<ChallengeInfo>();
            sortedChallenges = infos.Values.Where(x => !x.hidden && !string.IsNullOrEmpty(x.name)).OrderBy(x => x.priority).ToList();

            //Load local packs
            string[] challengeGUIDs = AssetDatabase.FindAssets($"t:{typeof(TutoPack).Name}");
            List<TutoPack> packs = challengeGUIDs.ToList().
                Select(x => AssetDatabase.LoadAssetAtPath<TutoPack>(AssetDatabase.GUIDToAssetPath(x))).
                Where(x => x != null && !x.hidden).OrderBy(x => x.priority).ToList();
            Dictionary<string, TutoPack> nameToPack = new Dictionary<string, TutoPack>();
            for (int i = 0; i < packs.Count; i++)
            {
                nameToPack.Add(packs[i].name, packs[i]);
            }


            //Update status list
            challengeList.Clear();
            for (int i = 0; i < sortedChallenges.Count; i++)
            {
                ChallengeInfo info = sortedChallenges[i];
                if (nameToPack.ContainsKey(info.name))
                {
                    TutoPack pack = nameToPack[info.name];
                    if (pack.hash == info.hash)
                        challengeList.Add(new Status(pack, ChallengeStatus.Good));
                    else
                        challengeList.Add(new Status(pack, ChallengeStatus.MajorUpdate));

                    nameToPack.Remove(info.name);
                }
                else
                {
                    challengeList.Add(new Status(info, ChallengeStatus.New));
                }
            }

            foreach (var item in nameToPack)
                challengeList.Add(new Status(item.Value, ChallengeStatus.Deprecated));

            UpdateFilteredStatus();
        }

        private void UpdateCache()
        {
            updaterStatus = UpdaterStatus.Loading;
            List<ChallengeInfo> infos = new List<ChallengeInfo>();
            CheckCacheDirectories();

            //Download project infos
            string uri = $"{Updater.gitDownloadUrl}/Index.json";
            DownloadFile(uri, (DownloadHandler handler) =>
            {
                UpdaterInfo projectInfo = JsonUtility.FromJson<UpdaterInfo>(handler.text);
                string file = JsonUtility.ToJson(projectInfo);
                File.WriteAllText($"{cacheDir}/UpdaterInfo.json", file);

                GetGitFileList();
            });

            //Get a list of all file under Challenges directory
            void GetGitFileList()
            {
                //Get all elements in Challenges directory
                UnityWebRequest request = UnityWebRequest.Get($@"{Updater.gitContentUrl}/Challenges");
                request.SetRequestHeader("authorization", Updater.gitToken);
                request.SendWebRequest().completed += OnGetGitFileList;
            }

            //All the files on git are listed
            void OnGetGitFileList(AsyncOperation obj)
            {
                UnityWebRequest request = (obj as UnityWebRequestAsyncOperation).webRequest;
                if (LogErrorIfAny(request))
                    return;

                GitElement[] elements = GetJsonArray<GitElement>(request.downloadHandler.text);
                if (elements == null)
                    return;

                Dictionary<string, GitChallenge> gitChallenges = new Dictionary<string, GitChallenge>();
                Queue<GitChallenge> toDownload = new Queue<GitChallenge>();

                for (int i = 0; i < elements.Length; i++)
                {
                    string challengeName = elements[i].name.Split('.')[0];
                    if (!gitChallenges.ContainsKey(challengeName))
                    {
                        GitChallenge gitChallenge = new GitChallenge();
                        gitChallenges.Add(challengeName, gitChallenge);
                        toDownload.Enqueue(gitChallenge);
                    }
                    gitChallenges[challengeName].AddElement(elements[i]);
                }

                DownloadChallengeInfos(toDownload, OnCacheUpdated);
            }

            //Act like a loop iterating on gitChallenges
            void DownloadChallengeInfos(Queue<GitChallenge> gitChallenges, System.Action endCallback)
            {
                if (gitChallenges.Count == 0)
                {
                    endCallback.Invoke();
                    return;
                }

                GitChallenge gitChallenge = gitChallenges.Dequeue();

                DownloadFile(gitChallenge.info.download_url, (DownloadHandler handler) =>
                {
                    //Read downloaded file
                    ChallengeInfo downloaded = JsonUtility.FromJson<ChallengeInfo>(handler.text);
                    string infoFilePath = $"{cacheDir}/{downloaded.name}.json";
                    string previewFilePath = $"{cacheDir}/{downloaded.name}.jpg";

                    //Read existing info in cache
                    ChallengeInfo existing = default;
                    if (File.Exists(infoFilePath))
                    {
                        existing = JsonUtility.FromJson<ChallengeInfo>(File.ReadAllText(infoFilePath));
                    }

                    //If preview don't exist or challenge version changed -> Download preview
                    if (!string.IsNullOrEmpty(gitChallenge.preview.download_url))
                    {
                        if (existing.hash != downloaded.hash || !File.Exists(previewFilePath))
                        {
                            DownloadFile(gitChallenge.preview.download_url, (DownloadHandler handler) =>
                            {
                                File.WriteAllBytes(previewFilePath, handler.data);
                            });
                        }
                    }

                    //Overide existing info by the downloaded one
                    File.WriteAllText(infoFilePath, JsonUtility.ToJson(downloaded));

                    //Download next challenge
                    DownloadChallengeInfos(gitChallenges, endCallback);
                });
            }


            void OnCacheUpdated()
            {
                if (updaterStatus == UpdaterStatus.Loading)
                    updaterStatus = UpdaterStatus.Valid;
                LoadCache();
            }
        }

        private void UpdateFilteredStatus()
        {
            filteredChallengeList.Clear();

            for (int i = 0; i < challengeList.Count; i++)
            {
                //Teacher filter
                if (!string.IsNullOrEmpty(teacher) && challengeList[i].teacher != teacher)
                    continue;

                //Search filter
                if (!string.IsNullOrEmpty(search))
                {
                    Match match = Regex.Match(challengeList[i].tags, search, RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);
                    if (!match.Success)
                    {
                        match = Regex.Match(challengeList[i].name, search, RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);
                        if (!match.Success)
                            continue;
                    }
                }

                filteredChallengeList.Add(challengeList[i]);
            }
        }

        private struct Status
        {
            public TutoPack pack;
            public string name;
            public string teacher;
            public string tags;
            public string description;
            public Texture2D preview;
            public ChallengeStatus status;

            public Status(TutoPack pack, ChallengeStatus status)
            {
                this.pack = pack;
                this.name = pack.name;
                this.status = status;
                this.teacher = pack.teacher;
                this.tags = pack.tags;
                this.description = pack.description;
                this.preview = pack.preview;
            }

            public Status(ChallengeInfo infos, ChallengeStatus status)
            {
                this.pack = null;
                this.name = infos.name;
                this.status = status;
                this.teacher = infos.teacher;
                this.tags = infos.tags;
                this.description = infos.description;
                this.preview = infos.preview;
            }
        }

        private class GitChallenge
        {
            public GitElement unityPackage;
            public GitElement info;
            public GitElement preview;

            public void AddElement(GitElement element)
            {
                if (element.name.EndsWith(".unitypackage"))
                    unityPackage = element;
                else if (element.name.EndsWith(".json"))
                    info = element;
                else if (element.name.EndsWith(".jpg"))
                    preview = element;
            }
        }

        [System.Serializable]
        private struct GitElement
        {
            public string name;
            public string path;
            public string sha;
            public long size;
            public string url;
            public string html_url;
            public string git_url;
            public string download_url;
            public string type;

            public bool isDirectory { get => type == "dir"; }
            public bool isFile { get => type == "file"; }
            public bool isJson { get => isFile && name.EndsWith(".json"); }
        }

        private enum ChallengeStatus
        {
            Good,
            MinorUpdate,
            MajorUpdate,
            New,
            Deprecated
        }

        [System.Serializable]
        public struct UpdaterInfo
        {
            public string name;
            public string displayName;
            public string version;
            public string unity;
            public string description;
            public AuthorInfo author;
        }

        [System.Serializable]
        public struct AuthorInfo
        {
            public string name;
            public string email;
            public string url;
        }

        [System.Serializable]
        public struct ChallengeInfo
        {
            public string name;
            public string teacher;
            public string tags;
            public string description;
            public Hash128 hash;
            public float priority;
            public bool hidden;
            public Texture2D preview;

            public ChallengeInfo(TutoPack pack)
            {
                name = pack.name;
                teacher = pack.teacher;
                tags = pack.tags;
                description = pack.description;
                hash = pack.hash;
                priority = pack.priority;
                hidden = pack.hidden;
                preview = pack.preview;
            }
        }

        private enum UpdaterStatus
        {
            Valid,
            Loading,
            Outdated,
            Error
        }

        //Json utilities
        public static T[] GetJsonArray<T>(string json)
        {
            string newJson = "{ \"array\": " + json + "}";
            Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(newJson);
            return wrapper.array;
        }
        public static string arrayToJson<T>(T[] array)
        {
            Wrapper<T> wrapper = new Wrapper<T>();
            wrapper.array = array;
            return JsonUtility.ToJson(wrapper);
        }
        [System.Serializable]
        private class Wrapper<T>
        {
            public T[] array;
        }

        private static bool LogErrorIfAny(UnityWebRequest request)
        {
            switch (request.result)
            {
                case UnityWebRequest.Result.ConnectionError:
                    updaterStatus = UpdaterStatus.Error;
                    updaterErrorTitle = "Connection Error";
                    updaterErrorMessage = request.error;
                    Debug.LogError($"Download challenge infos - Connection Error\n{request.error}");
                    return true;
                case UnityWebRequest.Result.ProtocolError:
                    updaterStatus = UpdaterStatus.Error;
                    updaterErrorTitle = "Protocol Error";
                    updaterErrorMessage = request.error;
                    Debug.LogError($"Download challenge infos - Protocol Error\n{request.error}");
                    return true;
                case UnityWebRequest.Result.DataProcessingError:
                    updaterStatus = UpdaterStatus.Error;
                    updaterErrorTitle = "Data Processing Error";
                    updaterErrorMessage = request.error;
                    Debug.LogError($"Download challenge infos - Data Processing Error\n{request.error}");
                    return true;

                default: return false;
            }
        }
    }
}