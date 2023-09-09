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

namespace Challenges
{
    public class Updater : ScriptableObject
    {
        public const int projectVersion = 13;

        public const string gitOwner = "Niwala";
        public const string gitRepo = "Challenges";
        public static string gitDownloadUrl = @$"https://raw.githubusercontent.com//{gitOwner}/{gitRepo}/main";
        public static string gitContentUrl = @$"https://api.github.com/repos/{gitOwner}/{gitRepo}/contents";
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

        private string cacheDir { get => "Assets/Updater/Cache"; }
        private string challengesDir { get => $"{cacheDir}/Challenges"; }


        private void OnEnable()
        {
            teacher = EditorPrefs.GetString(TutoPack_Selector.lastTeacherPrefKey, "");
            LoadCache();
        }

        private void OnDisable()
        {
            DestroyImmediate(target);
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
            string teacherContent = string.IsNullOrEmpty(teacher) ? "Teacher : All" : $"Teacher : {teacher.Replace("/", " � ")}";

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
                if (Event.current.shift)
                    menu.AddItem(new GUIContent("Generate Index File"), false, () => { GenerateIndexFile(); });
                if (Event.current.shift)
                    menu.AddItem(new GUIContent("Export all challenges"), false, () => { GenerateIndexFile(); ExportAllChallenges(); });

                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Auto Select"), autoSelect, () => { TutoPack_Selector.ToggleAutoSelect(); });
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
            Rect descriptionRect = new Rect(rect.x + 5, rect.y + 40, rect.width - 100, rect.height - 40);
            Rect buttonRect = new Rect(rect.x + rect.width - 90, rect.y + rect.height - 25, 85, 20);

            switch (updaterStatus)
            {
                case UpdaterStatus.Valid:

                    titleRect = new Rect(rect.x + 10, rect.y + 10, rect.width - 10, 26);
                    buttonRect = new Rect(rect.x + rect.width - 190, rect.y + rect.height - 25, 185, 20);

                    EditorGUI.HelpBox(rect, "", MessageType.None);
                    GUI.Label(titleRect, "Updater", titleStyle);
                    if (GUI.Button(buttonRect, "V�rifier les mises � jour"))
                        UpdateCache();
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


                    GUI.Label(rect, "Chargement...", centredtitleStyle);
                    break;

                case UpdaterStatus.Outdated:
                    EditorGUI.HelpBox(rect, "", MessageType.Warning);
                    GUI.Label(titleRect, "Mise � jour disponible", titleStyle);
                    GUI.Label(descriptionRect, "N'influence pas les challenges", descriptionStyle);
                    if (GUI.Button(buttonRect, "Appliquer"))
                        UpdateProject();
                    break;

                case UpdaterStatus.Error:
                    EditorGUI.HelpBox(rect, "", MessageType.Error);
                    GUI.Label(titleRect, updaterErrorTitle, titleStyle);
                    GUI.Label(descriptionRect, updaterErrorMessage, descriptionStyle);
                    if (GUI.Button(buttonRect, "Rafraichir"))
                        UpdateCache();
                    break;
            }
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
                GUI.DrawTexture(previewRect, status.preview);
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

        private void DownloadChallenge(string name, bool manualImport = false)
        {
            DowloadPackage($"{Updater.gitDownloadUrl}/Challenges/{name}.unitypackage", manualImport);
        }

        private void UpdateProject()
        {
            DowloadPackage($"{Updater.gitDownloadUrl}/Project/Project.unitypackage", false);
        }

        private void DowloadPackage(string uri, bool manualImport = false)
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
                string type = pack.onOpen.Split('.')[0];
                MethodInfo method = Assembly.GetExecutingAssembly().GetType(type).GetMethod(pack.onOpen.Split('.')[1], BindingFlags.Static | BindingFlags.Public);
                method.Invoke("", new object[0]);
            }
        }

        private void PingChallenge(string name)
        {
            string[] tutoPackGUIDs = AssetDatabase.FindAssets("t:TutoPack");
            EditorGUIUtility.PingObject(tutoPackGUIDs.ToList().
                Select(x => AssetDatabase.LoadAssetAtPath<TutoPack>(AssetDatabase.GUIDToAssetPath(x))).
                FirstOrDefault(x => x.name == name));
        }

        private void GenerateIndexFile()
        {
            string[] tutoPackGUIDs = AssetDatabase.FindAssets("t:TutoPack");
            List<TutoPack> packs = tutoPackGUIDs.ToList().
                Select(x => AssetDatabase.LoadAssetAtPath<TutoPack>(AssetDatabase.GUIDToAssetPath(x))).
                Where(x => x != null && x.majorVersion > 0).ToList();

            ChallengeInfo[] infos = packs.ToList().Select(x => new ChallengeInfo(x)).ToArray();
            string file = JsonUtility.ToJson(ProjectInfo.CurrentProjectInfo, true);

            string directory = Application.dataPath.Remove(Application.dataPath.Length - 6) + "../Repository/";
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(directory + "Index.json", file);
        }

        private void CheckCacheDirectories()
        {
            if (!Directory.Exists(cacheDir))
                Directory.CreateDirectory(cacheDir);
            if (!Directory.Exists(challengesDir))
                Directory.CreateDirectory(challengesDir);
        }

        private void LoadCache()
        {
            CheckCacheDirectories();


            //Check project update
            ProjectInfo projectInfo = default;
            if (File.Exists($"{cacheDir}/UpdaterInfo.json"))
            {
                string projectInfoFile = File.ReadAllText($"{cacheDir}/UpdaterInfo.json");
                projectInfo = JsonUtility.FromJson<ProjectInfo>(projectInfoFile);
            }
            if (projectInfo.projectVersion > Updater.projectVersion)
                updaterStatus = UpdaterStatus.Outdated;


            //Read all challenge infos
            List<ChallengeInfo> infos = new List<ChallengeInfo>();
            string[] files = Directory.GetFiles(challengesDir);
            for (int i = 0; i < files.Length; i++)
            {
                if (files[i].EndsWith(".json"))
                {
                    string file = File.ReadAllText(files[i]);
                    infos.Add(JsonUtility.FromJson<ChallengeInfo>(file));
                }
            }


            //Reorder infos
            infos = infos.Where(x => !x.hidden).OrderBy(x => x.priority).ToList();

            //Load local packs
            string[] challengeGUIDs = AssetDatabase.FindAssets($"t:{typeof(TutoPack).Name}");
            List<TutoPack> packs = challengeGUIDs.ToList().
                Select(x => AssetDatabase.LoadAssetAtPath<TutoPack>(AssetDatabase.GUIDToAssetPath(x))).
                Where(x => x != null).OrderBy(x => x.priority).ToList();
            Dictionary<string, TutoPack> nameToPack = new Dictionary<string, TutoPack>();
            for (int i = 0; i < packs.Count; i++)
                nameToPack.Add(packs[i].name, packs[i]);


            //Update status list
            challengeList.Clear();
            for (int i = 0; i < infos.Count; i++)
            {
                ChallengeInfo info = infos[i];

                if (nameToPack.ContainsKey(info.name))
                {
                    TutoPack pack = nameToPack[info.name];
                    if (pack.majorVersion == info.majorVersion)
                    {
                        if (pack.minorVersion == info.minorVersion)
                            challengeList.Add(new Status(pack, ChallengeStatus.Good));
                        else
                            challengeList.Add(new Status(pack, ChallengeStatus.MinorUpdate));
                    }
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
                ProjectInfo projectInfo = JsonUtility.FromJson<ProjectInfo>(handler.text);
                string file = JsonUtility.ToJson(projectInfo);
                File.WriteAllText($"{cacheDir}/UpdaterInfo.json", file);

                SearchChallenges();
            });

            //Find all available challenges
            void SearchChallenges()
            {
                //Get all elements in Challenges directory
                UnityWebRequest request = UnityWebRequest.Get($@"{Updater.gitContentUrl}/Challenges");
                request.SetRequestHeader("authorization", Updater.gitToken);
                request.SendWebRequest().completed += OnChallengesFound;
            }

            //All the challenge paths are found.
            void OnChallengesFound(AsyncOperation obj)
            {
                UnityWebRequest request = (obj as UnityWebRequestAsyncOperation).webRequest;
                if (LogErrorIfAny(request))
                    return;

                GitElement[] elements = GetJsonArray<GitElement>(request.downloadHandler.text);
                Queue<string> challengeInfoUrls = new Queue<string>();

                if (elements == null)
                    return;

                for (int i = 0; i < elements.Length; i++)
                {
                    if (elements[i].isJson)
                        challengeInfoUrls.Enqueue(elements[i].download_url);
                }

                DownloadChallengeInfos(challengeInfoUrls, OnCacheUpdated);
            }

            //Download challenge infos
            void DownloadChallengeInfos(Queue<string> urls, System.Action endCallback)
            {
                if (urls.Count == 0)
                {
                    endCallback.Invoke();
                    return;
                }

                DownloadFile(urls.Dequeue(), (DownloadHandler handler) =>
                {
                    ChallengeInfo info = JsonUtility.FromJson<ChallengeInfo>(handler.text);
                    string file = JsonUtility.ToJson(info);
                    File.WriteAllText($"{challengesDir}/{info.name}.json", file);
                    DownloadChallengeInfos(urls, endCallback);
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
                //Hidden filter
                if (challengeList[i].hidden)
                    continue;

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

        private void DownloadFile(string uri, System.Action<DownloadHandler> callback)
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

        private struct Status
        {
            public TutoPack pack;
            public string name;
            public bool hidden;
            public string teacher;
            public string tags;
            public string description;
            public Texture2D preview;
            public ChallengeStatus status;

            public Status(TutoPack pack, ChallengeStatus status)
            {
                this.pack = pack;
                this.name = pack.name;
                this.hidden = pack.hidden;
                this.status = status;
                this.teacher = pack.teacher;
                this.tags = pack.tags;
                this.description = pack.description;
                if (pack.preview != null)
                    this.preview = pack.preview;
                else
                    this.preview = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Challenges/{name}/Tuto Resources/Preview.png");
            }

            public Status(ChallengeInfo infos, ChallengeStatus status)
            {
                this.pack = null;
                this.name = infos.name;
                this.hidden = infos.hidden;
                this.status = status;
                this.teacher = infos.teacher;
                this.tags = infos.tags;
                this.description = infos.description;
                this.preview = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Challenges/{name}/Tuto Resources/Preview.png");
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
        public struct ProjectInfo
        {
            public int projectVersion;

            public static ProjectInfo CurrentProjectInfo
            {
                get
                {
                    ProjectInfo info = default;
                    info.projectVersion = Updater.projectVersion;
                    return info;
                }
            }
        }

        [System.Serializable]
        public struct ChallengeInfo
        {
            public string name;
            public string teacher;
            public string tags;
            public string description;
            public int minorVersion;
            public int majorVersion;
            public float priority;
            public bool hidden;

            public ChallengeInfo(TutoPack pack)
            {
                name = pack.name;
                teacher = pack.teacher;
                tags = pack.tags;
                description = pack.description;
                minorVersion = pack.minorVersion;
                majorVersion = pack.majorVersion;
                priority = pack.priority;
                hidden = pack.hidden;
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

        private bool LogErrorIfAny(UnityWebRequest request)
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