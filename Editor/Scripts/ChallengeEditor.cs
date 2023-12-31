﻿using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Video;
using UnityEditorInternal;
using ChallengeInfo = Challenges.Updater.ChallengeInfo;
using Debug = UnityEngine.Debug;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace Challenges
{
    [CustomEditor(typeof(Challenge))]
    public class ChallengeEditor : Editor
    {
        private Challenge challenge;

        //New GUI
        public override VisualElement CreateInspectorGUI()
        {
            challenge = target as Challenge;
            VisualElement root = new VisualElement();

            GenerateHeader(root);

            if (Preferences.devMode)
                GenerateDevContent(root);
            else
                GenerateUserContent(root);

            return root;
        }

        private void GenerateHeader(VisualElement root)
        {
            //Cover
            if (challenge.preview != null)
            {
                VisualElement cover = new VisualElement();
                cover.style.backgroundColor = new Color(0, 0, 0, 0.2f);
                cover.style.backgroundImage = challenge.preview;
                cover.style.position = Position.Absolute;
                cover.style.top = cover.style.left = cover.style.right = 0;
                cover.style.height = 80;
                cover.style.borderBottomColor = new Color(0, 0, 0, 0.6f);
                cover.style.borderBottomWidth = 1.0f;
                cover.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(BackgroundSizeType.Cover));
                root.Add(cover);
            }

            //Title
            TextElement title = new TextElement();
            title.text = challenge.name;
            title.style.fontSize = 22;
            title.style.left = 10;
            title.style.top = 10;
            title.style.marginBottom = 60;
            root.Add(title);
        }

        private void GenerateUserContent(VisualElement root)
        {
            //Description
            TextElement description = new TextElement();
            description.text = challenge.description;
            description.style.opacity = 0.7f;
            description.style.fontSize = 14;
            description.SetMargin(3, 10, 10, 3);
            root.Add(description);

            //Open in challenges
            Button openBtn = GenerateRoundButton("Open in Challenge Window");
            openBtn.clicked += () => Challenges.Open(challenge);
            root.Add(openBtn);
        }

        private void GenerateDevContent(VisualElement root)
        {
            VisualElement container = new VisualElement();
            container.SetPadding(0, 10, 10, 0);
            root.Add(container);

            container.Add(new PropertyField(serializedObject.FindProperty(nameof(challenge.hidden))));
            container.Add(new PropertyField(serializedObject.FindProperty(nameof(challenge.teacher))));
            container.Add(new PropertyField(serializedObject.FindProperty(nameof(challenge.preview))));
            container.Add(new PropertyField(serializedObject.FindProperty(nameof(challenge.description))));
            container.Add(new PropertyField(serializedObject.FindProperty(nameof(challenge.tags))));
            container.Add(new PropertyField(serializedObject.FindProperty(nameof(challenge.scene))));
            container.Add(new PropertyField(serializedObject.FindProperty(nameof(challenge.parent))));
            container.Add(new PropertyField(serializedObject.FindProperty(nameof(challenge.priority))));
            container.Add(new PropertyField(serializedObject.FindProperty(nameof(challenge.onOpen))));

            VisualElement commands = new VisualElement();
            commands.style.marginTop = 10;
            commands.style.flexDirection = FlexDirection.Row;
            commands.style.flexGrow = 1;

            Button openBtn = GenerateRoundButton("Open in Challenge Window");
            openBtn.clicked += () => Challenges.Open(challenge);
            commands.Add(openBtn);

            Button publish = GenerateRoundButton("Publish");
            publish.clicked += OnPublish;
            commands.Add(publish);

            root.Add(commands);
        }

        Button GenerateRoundButton(string text)
        {
            Button btn = new Button();
            btn.text = text;
            btn.style.flexGrow = 1;
            btn.SetBorder(1, 10, new Color(0.0f, 0.0f, 0.0f, 0.6f));
            btn.SetMargin(3, 10, 10, 3);
            btn.style.height = 25;
            return btn;
        }

        protected override void OnHeaderGUI() { }








        private static bool editTuto;
        private static bool editInfos;
        private int page
        {
            get { return _page; }
            set
            {
                if (_page != value)
                {
                    _page = value;
                    OnChangePage();
                }
            }
        }
        private int _page;
        private int editContentID = -1;
        private bool editContent
        {
            get { return editContentID != -1 && editContentID < pack.pages[page].content.Count; }
        }
        private Vector2 scroll;

        //Error report
        private bool showErrorReport;
        private string errorReportDescription;
        private string errorReportUsername;
        private EditorWindow[] errorReportWindows;

        //Hints
        private List<bool> hintAlphas = new List<bool>();
        private int hintID;

        //Logs
        private static Queue<LogMessage> logs = new Queue<LogMessage>(100);
        private static string webhookUrl = @"https://discord.com/api/webhooks/1026214468855812166/Mv6ZYtWwElb2xPdr5OgB_P3B3eb47klKdPefewxPjnclhmgyk16TtQSypwoczWbe6xC8";

        //Styles
        private bool stylesLoaded;
        private GUIStyle titleStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle linkStyle;
        private GUIStyle textStyle;
        private GUIStyle pageStyle;
        private Color linkColor = new Color(0x00 / 255f, 0x78 / 255f, 0xDA / 255f, 1f);


        [InitializeOnLoadMethod]
        public static void RecordLogs()
        {
            Application.logMessageReceivedThreaded -= Application_logMessageReceivedThreaded;
            Application.logMessageReceivedThreaded += Application_logMessageReceivedThreaded;
        }

        private static void Application_logMessageReceivedThreaded(string condition, string stackTrace, LogType type)
        {
            logs.Enqueue(new LogMessage(condition, stackTrace, type));
            if (logs.Count == 100)
                logs.Dequeue();
        }

        struct LogMessage
        {
            public string condition;
            public string stackTrace;
            public LogType type;

            public LogMessage(string condition, string stackTrace, LogType type)
            {
                this.condition = condition;
                this.stackTrace = stackTrace;
                this.type = type;
            }
        }

        protected virtual Challenge pack
        {
            get => target as Challenge;
        }

        protected virtual SerializedObject targetObject
        {
            get => serializedObject;
        }

        //public override void OnInspectorGUI()
        //{
        //    if (!stylesLoaded)
        //        LoadStyles();

        //    if (editTuto || pack.pages.Count == 0)
        //    {
        //        if (editInfos)
        //        {
        //            DrawInfosInspectorGUI();
        //        }
        //        else
        //        {
        //            if (editContent)
        //                DrawContentInspectorGUI();
        //            else
        //                DrawEditorInspectorGUI();
        //        }
        //    }
        //    else
        //        DrawTutoPackInspectorGUI();

        //}

        //protected override void OnHeaderGUI()
        //{
        //    if (showErrorReport)
        //    {
        //        showErrorReport = false;
        //        EditorApplication.delayCall += () => SendErrorReport(errorReportDescription, errorReportUsername);
        //    }


        //    GUILayout.BeginHorizontal(EditorStyles.toolbar);

        //    if (editTuto && !editContent)
        //    {
        //        editInfos = GUILayout.Toggle(editInfos, " Challenge Infos ", EditorStyles.toolbarButton);
        //        GUILayout.Space(5);

        //        EditorGUI.BeginDisabledGroup(editInfos);
        //        if (GUILayout.Button(" New page ", EditorStyles.toolbarButton))
        //            NewPage();
        //        if (GUILayout.Button(" Duplicate page ", EditorStyles.toolbarButton))
        //            DuplicatePage();
        //        if (GUILayout.Button(" Delete page ", EditorStyles.toolbarButton))
        //            DeletePage();
        //        EditorGUI.EndDisabledGroup();
        //    }


        //    GUILayout.FlexibleSpace();

        //    if (editTuto)
        //    {
        //        if (GUILayout.Button(" Exit edition ", EditorStyles.toolbarButton))
        //        {
        //            editContentID = -1;
        //            editTuto = false;
        //        }
        //    }


        //    Rect rect = GUILayoutUtility.GetRect(22, EditorGUIUtility.singleLineHeight);
        //    if (GUI.Button(rect, EditorGUIUtility.IconContent("d_icon dropdown"), EditorStyles.toolbarButton))
        //    {
        //        bool autoSelect = EditorPrefs.GetBool(Updater.autoSelectPrefKey, true);
        //        GenericMenu menu = new GenericMenu();
        //        //menu.AddItem(new GUIContent("Challenges"), false, () => { Updater_.Open(); });
        //        menu.AddSeparator("");
        //        menu.AddItem(new GUIContent("Report an error"), false, () => { ShowErrorReportWindow(); });
        //        menu.AddItem(new GUIContent("Rate this challenge"), false, () => { ShowRatingWindow(); });
        //        menu.AddSeparator("");
        //        //menu.AddItem(new GUIContent("Auto Select"), autoSelect, () => { Updater_.ToggleAutoSelect(); });
        //        if (Event.current.shift)
        //        {
        //            menu.AddSeparator("");
        //            menu.AddDisabledItem(new GUIContent("⠀"));
        //            menu.AddDisabledItem(new GUIContent("Dev area"));
        //            menu.AddItem(new GUIContent("Edit"), false, () => { editTuto = !editTuto; });
        //            menu.AddItem(new GUIContent("Publish"), false, () => { OnPublish(); });
        //        }
        //        menu.DropDown(rect);
        //    }
        //    GUILayout.EndHorizontal();

        //    void NewPage()
        //    {
        //        Undo.RecordObject(target, "Create new tuto page");
        //        pack.pages.Insert(page + 1, new Page());
        //        page = Mathf.Clamp(page + 1, 0, pack.pages.Count - 1);
        //        EditorUtility.SetDirty(target);
        //    }

        //    void DeletePage()
        //    {
        //        Undo.RecordObject(target, "Delete tuto page");
        //        pack.pages.RemoveAt(page);
        //        page = Mathf.Clamp(page - 1, 0, pack.pages.Count - 1);
        //        EditorUtility.SetDirty(target);
        //    }

        //    void DuplicatePage()
        //    {
        //        Undo.RecordObject(target, "Duplicate tuto page");
        //        pack.pages.Insert(page + 1, pack.pages[page].Copy());
        //        page = Mathf.Clamp(page + 1, 0, pack.pages.Count - 1);
        //        EditorUtility.SetDirty(target);
        //    }
        //}

        private void OnPublish()
        {
            string repository = EditorPrefs.GetString($"{PublisherInfoEditor.prefPrefix}_Repository");

            //Check repository
            if (!repository.EndsWith('/') || !repository.EndsWith('\\'))
                repository += "\\";
            string directory = $"{repository}Challenges\\";
            if (!Directory.Exists(directory))
            {
                EditorUtility.DisplayDialog($"Publish {pack.name}", "The repository specified does not contain a \"Challenges\" folder.", "Ok");
                return;
            }

            ExportAt(pack, directory);

            //if (EditorUtility.DisplayDialog($"Publish {pack.name}", $"This will make the pack public and accessible to all students.", "Continue", "Cancel"))
            {
                Process cmd = new Process();
                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = "cmd.exe";
                info.RedirectStandardInput = true;
                info.RedirectStandardOutput = true;
                info.UseShellExecute = false;
                info.CreateNoWindow = true;
                info.WindowStyle = ProcessWindowStyle.Hidden;
                info.WorkingDirectory = repository;

                cmd.StartInfo = info;
                cmd.Start();

                using (StreamWriter sw = cmd.StandardInput)
                {
                    if (sw.BaseStream.CanWrite)
                    {
                        EditorUtility.DisplayProgressBar($"Publish {pack.name}", "Checkout...", 0.0f);
                        sw.WriteLine($"git checkout main");

                        EditorUtility.DisplayProgressBar($"Publish {pack.name}", "Fetch...", 0.2f);
                        sw.WriteLine($"git fetch origin main");

                        EditorUtility.DisplayProgressBar($"Publish {pack.name}", "Rebase...", 0.4f);
                        sw.WriteLine($"git rebase -i origin/main");

                        EditorUtility.DisplayProgressBar($"Publish {pack.name}", "Add files...", 0.6f);
                        sw.WriteLine($"git add {directory}{pack.name}.unitypackage");
                        sw.WriteLine($"git add {directory}{pack.name}.json");
                        sw.WriteLine($"git add {directory}{pack.name}.jpg");

                        EditorUtility.DisplayProgressBar($"Publish {pack.name}", "Get Status...", 0.8f);
                        sw.WriteLine($"git status");

                        EditorUtility.DisplayProgressBar($"Publish {pack.name}", "Push...", 1.0f);
                        sw.WriteLine($"git commit -am \"Publish {pack.name}\"");
                        sw.WriteLine($"git push origin main");
                    }
                }


                cmd.WaitForExit();
                EditorUtility.ClearProgressBar();
                Debug.Log(cmd.StandardOutput.ReadToEnd());
            }
        }

        private void OnChangePage()
        {
            for (int i = 0; i < hintAlphas.Count; i++)
                hintAlphas[i] = false;
            scroll = Vector2.zero;
        }

        private void OnEnable()
        {
            page = Mathf.Clamp(EditorPrefs.GetInt($"Tuto_{pack.name}_Page", 0), 0, pack.pages.Count - 1);

            //if (string.IsNullOrEmpty(pack.onOpen))

        }

        private void OnDisable()
        {
            EditorPrefs.SetInt($"Tuto_{pack.name}_Page", page);
        }

        private void LoadStyles()
        {
            titleStyle = new GUIStyle(EditorStyles.largeLabel);
            titleStyle.fontSize = 26;

            subtitleStyle = new GUIStyle(EditorStyles.largeLabel);
            subtitleStyle.fontSize = 18;
            subtitleStyle.richText = true;

            textStyle = new GUIStyle(EditorStyles.label);
            textStyle.wordWrap = true;
            textStyle.fontSize = 14;
            textStyle.richText = true;

            pageStyle = new GUIStyle();
            pageStyle.margin = new RectOffset(20, 10, 20, 0);

            linkStyle = new GUIStyle(textStyle);
            linkStyle.wordWrap = false;
            linkStyle.normal.textColor = linkColor;
            linkStyle.stretchWidth = false;
            stylesLoaded = true;
        }

        private void DrawContentInspectorGUI()
        {
            //Inspector
            GUILayout.Space(10);
            targetObject.UpdateIfRequiredOrScript();
            SerializedProperty prop = targetObject.FindProperty("pages")
                .GetArrayElementAtIndex(page)
                .FindPropertyRelative("content")
                .GetArrayElementAtIndex(editContentID);

            bool warp = EditorStyles.textField.wordWrap;
            EditorStyles.textField.wordWrap = true;
            EditorGUILayout.PropertyField(prop.FindPropertyRelative("type"), new GUIContent());
            EditorGUILayout.PropertyField(prop.FindPropertyRelative("text"), new GUIContent(), GUILayout.MinHeight(200));
            EditorGUILayout.PropertyField(prop.FindPropertyRelative("padding"));
            EditorGUILayout.PropertyField(prop.FindPropertyRelative("obj"));
            EditorStyles.textField.wordWrap = warp;
            targetObject.ApplyModifiedProperties();

            //Preview
            GUILayout.Space(10);
            EditorGUILayout.BeginVertical(pageStyle);
            DrawLine();
            DrawContent(pack.pages[page].content[editContentID]);
            DrawLine();
            hintID = 0;
            EditorGUILayout.EndVertical();

            //Done button
            if (CentredButton("Done"))
            {
                editContentID = -1;
            }

            GUILayout.Space(20);
            GUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.SelectableLabel("<color=blue>Yo, listen up here's a story</color>\t#637bff");
            EditorGUILayout.SelectableLabel("<b>Bold</b>");
            EditorGUILayout.SelectableLabel("<i>Itallic</i>");
            EditorGUILayout.SelectableLabel("<size=16>Big text</size>");
            EditorGUILayout.SelectableLabel("•  \u25B7  \u25B6  —  \u25E6  \u2023  \u29BF  \u29BE  \u25AA  \u25C9  \u2649");
            EditorGUILayout.SelectableLabel("Link : LinkName|LinkAdress");
            EditorGUILayout.SelectableLabel("Shader : Empty text = Square      Int value = Height of the preview");
            EditorGUILayout.EndVertical();
        }

        private void ShowErrorReportWindow()
        {
            EditorApplication.update += Show;
            void Show()
            {
                EditorApplication.update -= Show;
                ErrorReportWindow errorReportWindow = ScriptableObject.CreateInstance<ErrorReportWindow>();
                errorReportWindow.editor = this;
                errorReportWindow.ShowModal();
            }
        }

        private void ShowRatingWindow()
        {
            EditorApplication.update += Show;
            void Show()
            {
                EditorApplication.update -= Show;
                RatingWindow ratingWindow = ScriptableObject.CreateInstance<RatingWindow>();
                ratingWindow.editor = this;
                ratingWindow.ShowModal();
            }
        }

        private string BuildLogs()
        {
            string logFile = "";
            foreach (var item in logs)
                logFile += item.type + " : " + item.condition + "\n" + item.stackTrace + "\n";
            return logFile;
        }

        private void SendErrorReport(string userDescription, string username)
        {
            //Send logs only
            string logsUrl = "";
            WWWForm LogsForm = new WWWForm();
            LogsForm.AddField("content", "**Logs**");
            LogsForm.AddField("Content-Type", "multipart/form-data");
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(BuildLogs());
            LogsForm.AddBinaryData("file1", bytes, "Logs.txt");
            UnityWebRequest.Post(webhookUrl, LogsForm).SendWebRequest().completed += OnLogsPosted;

            void OnLogsPosted(AsyncOperation ao)
            {
                UnityWebRequest request = ((UnityWebRequestAsyncOperation)ao).webRequest;
                Response response = JsonUtility.FromJson<Response>(request.downloadHandler.text);

                if (response.attachments != null && response.attachments.Length > 0 && !string.IsNullOrEmpty(response.attachments[0].url))
                    logsUrl = response.attachments[0].url;

                //Delete log message
                UnityWebRequest.Delete($"{webhookUrl}/messages/{response.id}").SendWebRequest();

                //Send report
                SendReport();
            }

            //Send the report
            void SendReport()
            {
                //Build form
                WWWForm form = new WWWForm();
                List<Embed> embeds = new List<Embed>();
                form.AddField("content", "**Error report**");
                form.AddField("Content-Type", "multipart/form-data");
                int fileCount = 0;

                //Add report
                embeds.Add(BuildReport());

                //Add logs
                embeds.Add(BuildLogs());

                //Add windows screenshots
                for (int i = 0; i < errorReportWindows.Length; i++)
                    embeds.Add(Screenshot(errorReportWindows[i]));

                //Send message
                DiscordMessage message = new DiscordMessage("", embeds.ToArray());
                form.AddField("payload_json", JsonUtility.ToJson(message, true));
                UnityWebRequest.Post(webhookUrl, form).SendWebRequest().completed += OnReportPosted;


                void OnReportPosted(AsyncOperation ao)
                {
                    UnityWebRequest request = ((UnityWebRequestAsyncOperation)ao).webRequest;
                    if (request.result == UnityWebRequest.Result.ProtocolError || request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.DataProcessingError)
                        Debug.LogError(request.error);
                }

                Embed BuildReport()
                {
                    string report = $"```Teacher: {pack.teacher}";
                    report += $"\nChallenge: {pack.name}";
                    report += $"\nDescription: {userDescription}";
                    report += $"\n-----------------------------";
                    report += $"\nTime : {System.DateTime.Now.ToLongTimeString() + " " + System.DateTime.Now.ToLongDateString()}";
                    report += $"\nUnity account : {CloudProjectSettings.userName}";
                    report += $"\nUsername : {username}";
                    report += $"\nCurrent scene : {EditorSceneManager.GetActiveScene().name}";
                    report += $"\nChallenge Hash : {pack.hash}";
                    report += $"\nUpdater Version : {Updater.GetCurrentPackage().version}";
                    report += $"\nCurrent page : {page}```";
                    Embed embed = new Embed("Error report", report, Color.red);
                    return embed;
                }

                Embed BuildLogs()
                {
                    string lastLogs = "\n";
                    int count = 0;
                    foreach (var item in logs)
                    {
                        switch (item.type)
                        {
                            case LogType.Warning:
                                lastLogs += $":orange_circle: **Warning :** {item.condition}\n";
                                break;
                            case LogType.Log:
                                lastLogs += $":white_circle: **Log :** {item.condition}\n";
                                break;
                            case LogType.Error:
                            case LogType.Assert:
                            case LogType.Exception:
                                lastLogs += $":red_circle: **Error :** {item.condition}\n";
                                break;
                        }

                        count++;
                        if (count > 10)
                            break;
                    }

                    string report = lastLogs;
                    report += $"\n[Download log file]({logsUrl})";

                    Embed embed = new Embed("Console", report, Color.red);
                    return embed;
                }

                Embed Screenshot(EditorWindow window)
                {
                    Rect rect = window.position;
                    int width = (int)rect.width;
                    int height = (int)rect.height;
                    string title = window.titleContent.text.Replace(".", "");

                    Color[] colors = InternalEditorUtility.ReadScreenPixel(rect.position, width, height);
                    var screenshot = new Texture2D(width, height, TextureFormat.RGB24, false);
                    screenshot.SetPixels(colors);
                    screenshot.Apply();
                    Embed embed = new Embed(@$"attachment://{title}.png", Color.red);
                    embed.title = title;
                    form.AddBinaryData($"file{fileCount}", screenshot.EncodeToPNG(), $"{title}.png");
                    DestroyImmediate(screenshot);

                    fileCount++;
                    return embed;
                }
            }
        }

        private void SendRating(int rating, string msg)
        {
            //Build rating msg
            string report = $"```Teacher: {pack.teacher}";
            report += $"\nChallenge: {pack.name}";
            report += $"\nRating: {rating + 1}/10";
            report += $"\nComment: {msg}";
            report += $"\n-----------------------------";
            report += $"\nTime : {System.DateTime.Now.ToLongTimeString() + " " + System.DateTime.Now.ToLongDateString()}";
            report += $"\nUnity account : {CloudProjectSettings.userName}";
            report += $"\nCurrent scene : {EditorSceneManager.GetActiveScene().name}";
            report += $"\nChallenge Hash : {pack.hash}";
            report += $"\nUpdater Version : {Updater.GetCurrentPackage().version}";
            report += $"\nCurrent page : {page}```";

            //Send rating msg
            Embed header = new Embed("Rating", report, new Color(0.2f, 0.7f, 1.0f));
            DiscordMessage message = new DiscordMessage("", header);
            UnityWebRequest request = new UnityWebRequest(webhookUrl, "POST");
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(message, true));
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.SetRequestHeader("Content-Type", "application/json; charset=UTF-8");
            request.SendWebRequest();
        }

        [System.Serializable]
        private struct DiscordMessage
        {
            public string content;
            public Embed[] embeds;

            public DiscordMessage(string content, params Embed[] embeds)
            {
                this.content = content;
                this.embeds = embeds;
            }
        }

        [System.Serializable]
        private struct Embed
        {
            public string title;
            public string description;
            public int color;
            public Image image;
            public Field[] fields;

            public Embed(string title, string description, Color color)
            {
                this.title = title;
                this.description = description;
                this.color = int.Parse(ColorUtility.ToHtmlStringRGB(color), System.Globalization.NumberStyles.HexNumber);
                image = default;
                fields = default;
            }

            public Embed(string url, Color color)
            {
                this.title = default;
                this.description = default;
                this.color = int.Parse(ColorUtility.ToHtmlStringRGB(color), System.Globalization.NumberStyles.HexNumber);
                image = new Image(url);
                fields = default;
            }

            public Embed(string url)
            {
                this.title = "Logs";
                this.description = default;
                this.color = int.Parse(ColorUtility.ToHtmlStringRGB(Color.red), System.Globalization.NumberStyles.HexNumber);
                image = default;
                fields = new Field[] { new Field("", url, false) };
            }
        }

        [System.Serializable]
        private struct Image
        {
            public string url;

            public Image(string url)
            {
                this.url = url;
            }
        }

        [System.Serializable]
        private struct Field
        {
            public string name;
            public string value;
            public bool inline;

            public Field(string name, string value, bool inline)
            {
                this.name = name;
                this.value = value;
                this.inline = inline;
            }
        }

        [System.Serializable]
        private struct Response
        {
            public string id;
            public Attachement[] attachments;
        }

        [System.Serializable]
        private struct Attachement
        {
            public string url;
        }

        private void ContentContextClick(int contentID)
        {
            GenericMenu menu = new GenericMenu();

            bool canPaste = false;
            Page.Content newContent = default;
            try
            {
                newContent = JsonUtility.FromJson<Page.Content>(EditorGUIUtility.systemCopyBuffer);
                canPaste = true;
            }
            catch { }

            menu.AddItem(new GUIContent("Edit"), false, () => { editContentID = contentID; });
            menu.AddSeparator("");
            if (contentID > 0)
                menu.AddItem(new GUIContent("Move Up"), false, MoveUp);
            else
                menu.AddDisabledItem(new GUIContent("Move Up"), false);
            if (contentID < pack.pages[page].content.Count - 1)
                menu.AddItem(new GUIContent("Move Down"), false, MoveDown);
            else
                menu.AddDisabledItem(new GUIContent("Move Down"), false);
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Cut"), false, Cut);
            menu.AddItem(new GUIContent("Copy"), false, Copy);
            if (canPaste)
                menu.AddItem(new GUIContent("Paste"), false, Paste);
            else
                menu.AddDisabledItem(new GUIContent("Paste"), false);
            menu.AddItem(new GUIContent("Duplicate"), false, Duplicate);
            menu.AddItem(new GUIContent("Remove"), false, Remove);

            menu.ShowAsContext();

            void MoveUp()
            {
                Undo.RecordObject(target, "Move content up");
                pack.pages[page].content.Insert(contentID - 1, pack.pages[page].content[contentID]);
                pack.pages[page].content.RemoveAt(contentID + 1);
                EditorUtility.SetDirty(target);
            }

            void MoveDown()
            {
                Undo.RecordObject(target, "Move content down");
                pack.pages[page].content.Insert(contentID + 2, pack.pages[page].content[contentID]);
                pack.pages[page].content.RemoveAt(contentID);
                EditorUtility.SetDirty(target);
            }

            void Cut()
            {
                EditorGUIUtility.systemCopyBuffer = JsonUtility.ToJson(pack.pages[page].content[contentID]);
                Remove();
            }

            void Copy()
            {
                EditorGUIUtility.systemCopyBuffer = JsonUtility.ToJson(pack.pages[page].content[contentID]);
            }

            void Paste()
            {
                Undo.RecordObject(target, "Paste content");
                pack.pages[page].content.Insert(contentID, newContent);
                EditorUtility.SetDirty(target);
            }

            void Duplicate()
            {
                Undo.RecordObject(target, "Duplicate content");
                pack.pages[page].content.Insert(contentID, pack.pages[page].content[contentID]);
                EditorUtility.SetDirty(target);
            }

            void Remove()
            {
                Undo.RecordObject(target, "Remove content");
                pack.pages[page].content.RemoveAt(contentID);
                EditorUtility.SetDirty(target);
            }
        }

        private void DrawEditorInspectorGUI()
        {
            DrawPageCommands();

            scroll = GUILayout.BeginScrollView(scroll, GUILayout.MaxHeight(Screen.height - 180));
            Vector2 mp = Event.current.mousePosition;
            EditorGUILayout.BeginVertical(pageStyle);

            if (pack.pages.Count == 0)
                pack.pages.Add(new Page());

            for (int i = 0; i < pack.pages[page].content.Count; i++)
            {
                Rect rect = EditorGUILayout.BeginVertical();
                if (rect.Contains(mp))
                {
                    rect.height += 3;
                    EditorGUI.DrawRect(rect, new Color(0.2f, 0.4f, 1.0f, 0.05f));
                    if (GUI.Button(rect, "", EditorStyles.helpBox))
                    {
                        GUI.FocusControl(null);
                        if (Event.current.button == 1)
                            ContentContextClick(i);
                        else
                            editContentID = i;
                    }
                    EditorGUI.BeginDisabledGroup(true);
                    DrawContent(pack.pages[page].content[i]);
                    EditorGUI.EndDisabledGroup();
                }
                else
                {
                    DrawContent(pack.pages[page].content[i]);
                }
                EditorGUILayout.EndVertical();
            }
            hintID = 0;

            //Add content button
            if (CentredButton("Add content"))
            {
                Undo.RecordObject(pack, "Add content");
                pack.pages[page].content.Add(new Page.Content());
                editContentID = pack.pages[page].content.Count - 1;
                EditorUtility.SetDirty(pack);
            }

            EditorGUILayout.EndVertical();
            GUILayout.EndScrollView();
            Repaint();
        }

        private void DrawTutoPackInspectorGUI()
        {
            DrawPageCommands();
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.MaxHeight(Screen.height - 180));
            GUILayout.BeginVertical(pageStyle);

            if (pack.pages.Count == 0)
                pack.pages.Add(new Page());

            for (int i = 0; i < pack.pages[page].content.Count; i++)
                DrawContent(pack.pages[page].content[i]);
            hintID = 0;
            GUILayout.EndVertical();
            GUILayout.EndScrollView();
            Repaint();
        }

        private void DrawPageCommands()
        {
            Rect leftBtn = new Rect(10, 10, 32, 32);
            //if (GUI.Button(leftBtn, EditorGUIUtility.IconContent("d_tab_prev")))
            //{
            //    if (page == 0)
            //        Updater_.Open();
            //    else
            //        page--;
            //}

            //Rect rightBtn = new Rect(Screen.width - 42, 10, 32, 32);
            //if (GUI.Button(rightBtn, EditorGUIUtility.IconContent("d_tab_next")))
            //{
            //    if (page == pack.pages.Count - 1)
            //        Updater_.Open();
            //    else
            //        page++;
            //}

            Rect labelRect = new Rect(50, 10, Screen.width - 100, 23);
            string label = ObjectNames.NicifyVariableName(pack.name);
            string pageLabel = $"<size=14>Page {page + 1} / {pack.pages.Count}</size>";
            GUIStyle pageStyle = new GUIStyle(subtitleStyle);
            pageStyle.alignment = TextAnchor.MiddleCenter;
            GUI.Label(labelRect, label, subtitleStyle);
            labelRect.y += 15;
            if (GUI.Button(labelRect, pageLabel, pageStyle))
            {
                GenericMenu menu = new GenericMenu();
                for (int i = 0; i < pack.pages.Count; i++)
                {
                    int a = i;
                    menu.AddItem(new GUIContent($"Page {a + 1}"), a == page, () => { page = a; });
                }
                menu.ShowAsContext();
            }
            //GUI.Label(labelBtn, ObjectNames.NicifyVariableName(pack.name) + $" <color=grey><size=12>{pack.majorVersion}.{pack.minorVersion}</size></color>", subtitleStyle);

            Rect lineRect = new Rect(10, 50, Screen.width - 20, 3);
            EditorGUI.DrawRect(lineRect, new Color(0.5f, 0.5f, 0.5f, 0.3f));

            GUILayout.Space(53);
        }

        //private void DrawInfosInspectorGUI()
        //{
        //    GUILayout.BeginVertical(pageStyle);
        //    targetObject.UpdateIfRequiredOrScript();
        //    GUILayout.Label(pack.name, titleStyle);
        //    GUILayout.Space(5);
        //    GUILayout.Label("Hash : " + pack.hash?.ToString());
        //    EditorGUILayout.PropertyField(targetObject.FindProperty("hidden"));
        //    EditorGUILayout.PropertyField(targetObject.FindProperty("teacher"));
        //    EditorGUILayout.PropertyField(targetObject.FindProperty("parent"));
        //    EditorGUILayout.PropertyField(targetObject.FindProperty("scene"));
        //    EditorGUILayout.PropertyField(targetObject.FindProperty("onOpen"));
        //    EditorGUILayout.PropertyField(targetObject.FindProperty("tags"));
        //    EditorGUILayout.PropertyField(targetObject.FindProperty("description"));
        //    EditorGUILayout.PropertyField(targetObject.FindProperty("preview"));
        //    EditorGUILayout.PropertyField(targetObject.FindProperty("priority"));
        //    targetObject.ApplyModifiedProperties();

        //    GUILayout.Space(20);
        //    if (GUILayout.Button("Export"))
        //        Export(pack);
        //    if (GUILayout.Button("Publish"))
        //    {
        //        if (Event.current.shift)
        //            OnPublish();
        //        else
        //            PublisherInfoEditor.Open(OnPublish);
        //    }

        //    GUILayout.EndVertical();
        //}

        private bool CentredButton(string label)
        {
            bool click = false;
            GUILayout.Space(20);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(label, GUILayout.Height(25)))
                click = true;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            return click;
        }

        private void DrawLine()
        {
            Rect r = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(false));
            EditorGUI.DrawRect(r, Color.white * 0.5f);
        }

        delegate T GetValue<T>();
        delegate void SetValue<T>(T value);

        private void DrawContent(Page.Content content)
        {
            if (content.type == Page.Type.Space)
            {
                GUILayout.Space(content.padding);
                return;
            }

            GUILayout.BeginHorizontal();

            if ((content.obj != null && content.type == Page.Type.Link) || content.type == Page.Type.Button || content.type == Page.Type.Shader || (content.obj != null && content.type == Page.Type.Code))
            {
                GUILayout.Space(content.padding - 3);
                Rect rect = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(false));


                switch (content.type)
                {
                    case Page.Type.Link:
                        if (LinkLabel(new GUIContent(content.text)))
                        {
                            Object loaded = AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GetAssetPath(content.obj));

                            switch (loaded)
                            {
                                case VideoClip clip:
                                    UnityEditor.PopupWindow.Show(rect, new PopupVideo(clip));
                                    break;

                                default:
                                    EditorGUIUtility.PingObject(content.obj);
                                    break;
                            }
                        }
                        break;

                    case Page.Type.Button:
                        if (GUILayout.Button(content.text, GUILayout.ExpandWidth(false), GUILayout.Height(25)))
                        {
                            Object loaded = AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GetAssetPath(content.obj));

                            switch (loaded)
                            {
                                case VideoClip clip:
                                    UnityEditor.PopupWindow.Show(rect, new PopupVideo(clip));
                                    break;

                                case Texture2D tex2D:
                                    UnityEditor.PopupWindow.Show(rect, new PopupImage(tex2D));
                                    break;

                                case SceneAsset scene:
                                    EditorSceneManager.OpenScene(AssetDatabase.GetAssetPath(scene), OpenSceneMode.Single);
                                    break;

                                default:
                                    Selection.activeObject = content.obj;
                                    break;
                            }
                        }
                        break;

                    case Page.Type.Code:
                        if (GUILayout.Button(" Afficher le code ", EditorStyles.miniButtonLeft, GUILayout.ExpandWidth(false)))
                        {
                            UnityEditor.PopupWindow.Show(rect, new PopupCode(content.text, content.obj as Texture2D));
                        }
                        if (GUILayout.Button(" Copier le code ", EditorStyles.miniButtonRight, GUILayout.ExpandWidth(false)))
                        {
                            EditorGUIUtility.systemCopyBuffer = content.text;
                        }
                        GUILayout.FlexibleSpace();
                        break;


                    case Page.Type.Shader:
                        {
                            Material mat = content.obj as Material;
                            if (mat != null)
                            {
                                //Properties
                                GUILayout.BeginVertical();
                                Shader shader = mat.shader;

                                int count = shader.GetPropertyCount();
                                for (int i = 0; i < count; i++)
                                {
                                    int propertyID = shader.GetPropertyNameId(i);
                                    string name = shader.GetPropertyDescription(i);

                                    //Attributes
                                    string[] attributes = shader.GetPropertyAttributes(i);
                                    bool isToggle = false;
                                    bool hide = false;
                                    bool hdr = false;

                                    for (int j = 0; j < attributes.Length; j++)
                                    {
                                        isToggle |= attributes[j] == "Toggle";
                                        hide |= attributes[j] == "HideInTuto";
                                        hdr |= attributes[j] == "HDR";
                                    }

                                    if (hide)
                                        continue;

                                    void DrawMatProp<T>(GetValue<T> get, SetValue<T> set)
                                    {
                                        EditorGUI.BeginChangeCheck();
                                        T value = get.Invoke();
                                        if (EditorGUI.EndChangeCheck())
                                            set.Invoke(value);
                                    }

                                    switch (shader.GetPropertyType(i))
                                    {
                                        case UnityEngine.Rendering.ShaderPropertyType.Color:
                                            DrawMatProp(() => EditorGUILayout.ColorField(new GUIContent(name), mat.GetColor(propertyID), false, true, hdr), (Color c) => mat.SetColor(propertyID, c));
                                            break;
                                        case UnityEngine.Rendering.ShaderPropertyType.Vector:
                                            DrawMatProp(() => EditorGUILayout.Vector4Field(name, mat.GetVector(propertyID)), (Vector4 v) => mat.SetVector(propertyID, v));
                                            break;
                                        case UnityEngine.Rendering.ShaderPropertyType.Float:
                                            if (isToggle)
                                                DrawMatProp(() => EditorGUILayout.Toggle(name, mat.GetFloat(propertyID) > 0.5f), (bool b) => mat.SetFloat(propertyID, b ? 1.0f : 0.0f));
                                            else
                                                DrawMatProp(() => EditorGUILayout.FloatField(name, mat.GetFloat(propertyID)), (float f) => mat.SetFloat(propertyID, f));
                                            break;
                                        case UnityEngine.Rendering.ShaderPropertyType.Range:
                                            Vector2 limits = shader.GetPropertyRangeLimits(i);
                                            DrawMatProp(() => EditorGUILayout.Slider(name, mat.GetFloat(propertyID), limits.x, limits.y), (float f) => mat.SetFloat(propertyID, f));
                                            break;
                                        case UnityEngine.Rendering.ShaderPropertyType.Texture:
                                            DrawMatProp(() => EditorGUILayout.ObjectField(name, mat.GetTexture(propertyID), typeof(Texture), false), (Object t) => mat.SetTexture(propertyID, t as Texture));
                                            break;
                                    }
                                }

                                double t = EditorApplication.timeSinceStartup;
                                float ft = (float)(t % 1000.0);
                                mat.SetFloat("_EditorTime", ft);

                                //Preview
                                Rect r;
                                if (!string.IsNullOrEmpty(content.text) && int.TryParse(content.text, out int height))
                                    r = GUILayoutUtility.GetRect(1f, height + 10);
                                else
                                    r = GUILayoutUtility.GetAspectRect(1f);
                                r.Set(r.x + 5, r.y + 5, r.width - 10, r.height - 10);
                                GUI.Box(r, "");
                                EditorGUI.DrawPreviewTexture(r, Texture2D.whiteTexture, mat);
                                GUILayout.EndVertical();
                            }

                        }
                        break;

                    default:
                        break;
                }
            }
            else
            {
                GUILayout.Space(content.padding);
                switch (content.type)
                {
                    case Page.Type.Title:
                        GUILayout.Label(content.text, titleStyle);
                        break;
                    case Page.Type.Subtitle:
                        GUILayout.Label(content.text, subtitleStyle);
                        break;
                    case Page.Type.Link:
                        {
                            string text = content.text;
                            string link = content.text;
                            if (content.text.Contains("|"))
                            {
                                int index = content.text.IndexOf('|');
                                text = content.text.Substring(0, index);
                                link = content.text.Substring(index + 1);
                            }

                            if (LinkLabel(new GUIContent(text)))
                                System.Diagnostics.Process.Start(link);
                        }
                        break;
                    case Page.Type.Text:
                        if (content.obj != null && content.obj is Texture2D tex)
                        {
                            GUILayout.BeginVertical();
                            GUILayout.Label(content.text, textStyle);
                            Rect imageRect = GUILayoutUtility.GetAspectRect(tex.width / (float)tex.height, GUILayout.MaxWidth(tex.width), GUILayout.MaxHeight(tex.height));
                            EditorGUI.DrawPreviewTexture(imageRect, tex);
                            GUILayout.EndVertical();
                        }
                        else
                            GUILayout.Label(content.text, textStyle);
                        break;

                    case Page.Type.Hint:
                        DrawHint(content.text);
                        break;

                    case Page.Type.Code:
                        {
                            GUILayout.BeginVertical(GUI.skin.box);
                            GUI.color = new Color(0.5f, 0.8f, 1.0f, 1.0f);
                            EditorGUILayout.SelectableLabel(content.text, textStyle);
                            GUI.color = Color.white;
                            GUILayout.EndVertical();
                        }
                        break;

                    case Page.Type.Separator:

                        Rect rect = GUILayoutUtility.GetRect(1.0f, content.padding, GUILayout.ExpandWidth(true));
                        rect.Set(rect.x + 20 - content.padding, rect.y + content.padding * 0.5f, rect.width - 40 + content.padding, 1.0f);
                        EditorGUI.DrawRect(rect, Color.white * 0.7f);

                        break;
                }
            }
            GUILayout.EndHorizontal();
        }

        public static string GetDirectory(string name)
        {
            string[] guids = AssetDatabase.FindAssets($"t:{nameof(Challenge)} {name}");

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                Challenge pack = AssetDatabase.LoadAssetAtPath<Challenge>(path);
                if (pack == null)
                    continue;
                if (pack.name == name)
                {
                    string contentPath = new FileInfo(AssetDatabase.GetAssetPath(pack)).DirectoryName;
                    contentPath = contentPath.Substring(Application.dataPath.Length - 6);
                    return contentPath;
                }
            }

            return null;
        }

        public static void Export(Challenge pack)
        {
            string path = Application.dataPath + "/../../Repository/Challenges/";
            ExportAt(pack, path);
        }

        public static void ExportAt(Challenge pack, string directory)
        {
            //Update pack hash
            pack.UpdateHash();
            EditorUtility.SetDirty(pack);
            AssetDatabase.SaveAssetIfDirty(pack);

            //Get path
            string contentPath = new FileInfo(AssetDatabase.GetAssetPath(pack)).DirectoryName;
            contentPath = contentPath.Substring(Application.dataPath.Length - 6);


            //Create directory if needed
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);


            //Export challenge unityPackage
            AssetDatabase.ExportPackage(contentPath, $"{directory}{pack.name}.unitypackage", ExportPackageOptions.Recurse);


            //Export challenge infos
            ChallengeInfo infos = new ChallengeInfo(pack);
            string jsonFile = JsonUtility.ToJson(infos, true);
            File.WriteAllText($"{directory}{pack.name}.json", jsonFile);


            //Export challenge preview
            if (pack.preview != null)
            {
                //Copy image
                string imgSource = AssetDatabase.GetAssetPath(pack.preview);
                string imgDest = $"{directory}{pack.name}.jpg";
                Texture2D tex = new Texture2D(2, 2);
                tex.LoadImage(File.ReadAllBytes(imgSource));
                File.WriteAllBytes(imgDest, tex.EncodeToJPG());
            }
        }

        //Preview

        protected override bool ShouldHideOpenButton()
        { return true; }

        public override bool HasPreviewGUI()
        { return false; }

        public override bool UseDefaultMargins()
        { return false; }

        bool LinkLabel(GUIContent label, params GUILayoutOption[] options)
        {
            var position = GUILayoutUtility.GetRect(label, linkStyle, options);

            Handles.BeginGUI();
            Handles.color = linkStyle.normal.textColor;
            Handles.DrawLine(new Vector3(position.xMin, position.yMax), new Vector3(position.xMax, position.yMax));
            Handles.color = Color.white;
            Handles.EndGUI();

            EditorGUIUtility.AddCursorRect(position, MouseCursor.Link);

            return GUI.Button(position, label, linkStyle);
        }

        void DrawHint(string hint)
        {
            GUILayout.BeginVertical();
            GUILayout.Space(10);
            GUILayout.Label($"<b>Indice {hintID + 1}</b>", textStyle);
            Rect maskRect = EditorGUILayout.BeginVertical();
            if (GUILayout.Button(hint, textStyle))
                hintAlphas[hintID] = !hintAlphas[hintID];
            EditorGUILayout.EndVertical();

            while (hintAlphas.Count <= hintID)
                hintAlphas.Add(false);
            EditorGUI.DrawRect(maskRect, new Color(0, 0, 0, hintAlphas[hintID] ? 0.1f : 1.0f));
            EditorGUIUtility.AddCursorRect(maskRect, MouseCursor.Link);
            hintID++;
            GUILayout.EndVertical();
        }

        public class PopupVideo : PopupWindowContent
        {
            private VideoClip clip;
            private Editor videoEditor;
            private bool firstFrame = true;

            public PopupVideo(VideoClip clip)
            {
                this.clip = clip;
            }

            public override Vector2 GetWindowSize()
            {
                return new Vector2(clip.width, clip.height);
            }

            public override void OnGUI(Rect rect)
            {
                GUI.Box(rect, "", GUI.skin.window);
                rect.Set(rect.x + 10, rect.y + 10, rect.width - 20, rect.height - 20);
                videoEditor.OnPreviewGUI(rect, GUI.skin.window);
                if (firstFrame)
                {
                    MethodInfo playPreview = videoEditor.GetType().GetMethod("PlayPreview", BindingFlags.Instance | BindingFlags.NonPublic);
                    playPreview.Invoke(videoEditor, new object[0]);
                }
                firstFrame = false;
            }

            public override void OnOpen()
            {
                videoEditor = Editor.CreateEditor(clip);
            }

            public override void OnClose()
            {
                DestroyImmediate(videoEditor);
            }
        }

        public class PopupImage : PopupWindowContent
        {
            private Texture2D tex;

            public PopupImage(Texture2D tex)
            {
                this.tex = tex;
            }

            public override Vector2 GetWindowSize()
            {
                return new Vector2(tex.width, tex.height);
            }

            public override void OnGUI(Rect rect)
            {
                GUI.Box(rect, "", GUI.skin.window);
                rect.Set(rect.x + 10, rect.y + 10, rect.width - 20, rect.height - 20);
                EditorGUI.DrawPreviewTexture(rect, tex);
            }
        }

        public class PopupCode : PopupWindowContent
        {
            private string code;
            private Texture2D preview;
            private Vector2 scroll;

            public PopupCode(string code, Texture2D preview)
            {
                this.code = code;
                this.preview = preview;
            }

            public override Vector2 GetWindowSize()
            {
                return new Vector2(preview.width + 20, preview.height + 50);
            }

            public override void OnGUI(Rect rect)
            {
                GUI.Box(rect, "", GUI.skin.window);
                if (GUI.Button(new Rect(rect.x + rect.width - 100, rect.y + 10, 90, 20), "Copy"))
                    EditorGUIUtility.systemCopyBuffer = code;
                rect.Set(rect.x + 10, rect.y + 40, rect.width - 20, rect.height - 50);
                scroll = GUI.BeginScrollView(rect, scroll, new Rect(0, 0, preview.width, preview.height));
                EditorGUI.DrawPreviewTexture(new Rect(0, 0, preview.width, preview.height), preview);
                GUI.EndScrollView();
            }
        }

        public class ErrorReportWindow : EditorWindow
        {
            private string description;
            private string username;
            public ChallengeEditor editor;
            private EditorWindow[] windows;
            private string[] windowsName;
            private bool[] windowsIncluded;

            public void OnEnable()
            {
                titleContent = new GUIContent("Error Report");
                windows = Resources.FindObjectsOfTypeAll<EditorWindow>().ToList().Where(x => x.GetType() != typeof(ErrorReportWindow)).ToArray();
                windowsName = windows.ToList().Select(x => x.titleContent.text).ToArray();
                windowsIncluded = new bool[windowsName.Length];
                for (int i = 0; i < windowsIncluded.Length; i++)
                    windowsIncluded[i] = (windowsName[i] == "Inspector" || windowsName[i] == "Scene");
            }

            public void OnGUI()
            {
                GUILayout.Label("Contact");
                username = EditorGUILayout.TextField(username);
                if (string.IsNullOrEmpty(username))
                    GUI.Label(GUILayoutUtility.GetLastRect(), "(Pseudo Discord ou Mail)", EditorStyles.centeredGreyMiniLabel);

                GUILayout.Label("Description du problème");
                description = EditorGUILayout.TextArea(description, GUILayout.ExpandHeight(true));

                EditorGUILayout.HelpBox("En cliquant sur Send, vous acceptez que ce message soit envoyé à un professeur avec des infos d'Unity, tel que des screenshots et logs.", MessageType.None);

                if (GUILayout.Button("Screenshot inclus", EditorStyles.toolbarDropDown))
                {
                    GenericMenu menu = new GenericMenu();
                    for (int i = 0; i < windowsName.Length; i++)
                    {
                        int a = i;
                        menu.AddItem(new GUIContent(windowsName[a]), windowsIncluded[a], () => { windowsIncluded[a] = !windowsIncluded[a]; });
                    }
                    menu.ShowAsContext();
                }

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Close"))
                {
                    Close();
                }
                EditorGUI.BeginDisabledGroup(string.IsNullOrWhiteSpace(description) || string.IsNullOrWhiteSpace(username));
                if (GUILayout.Button("Send"))
                {
                    List<EditorWindow> includedWindows = new List<EditorWindow>();
                    for (int i = 0; i < windowsIncluded.Length; i++)
                        if (windowsIncluded[i])
                            includedWindows.Add(windows[i]);

                    editor.errorReportDescription = description;
                    editor.errorReportUsername = username;
                    editor.showErrorReport = true;
                    editor.errorReportWindows = includedWindows.ToArray();
                    Close();
                }
                GUILayout.EndHorizontal();
            }
        }

        public class RatingWindow : EditorWindow
        {
            public int rating;
            private string message;
            public ChallengeEditor editor;

            private GUIContent star;

            public void OnEnable()
            {
                titleContent = new GUIContent("Rating");
                star = EditorGUIUtility.IconContent("Favorite@2x");
            }

            public void OnGUI()
            {
                GUILayout.Label($"Score {rating + 1}/10", EditorStyles.centeredGreyMiniLabel);


                Rect rect = GUILayoutUtility.GetRect(1.0f, 30.0f);
                float center = rect.x + rect.width * 0.5f;
                rect.width = Mathf.Min(rect.width, 300);
                rect.position = new Vector2(center - rect.width * 0.5f, rect.y);
                float iconSize = rect.width / 10.0f;

                for (int i = 0; i < 10; i++)
                {
                    Rect r = new Rect(rect.x + i * iconSize, rect.y, iconSize, iconSize);
                    GUI.color = i <= rating ? Color.white : new Color(0.2f, 0.2f, 0.2f, 1.0f);

                    if (GUI.Button(r, star, GUIStyle.none))
                    {
                        if (i == 0 && rating == 0)
                            rating = -1;
                        else
                            rating = i;
                    }
                }
                GUI.color = Color.white;

                GUILayout.Label("Un commentaire ?");
                message = EditorGUILayout.TextArea(message, GUILayout.ExpandHeight(true));

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Close"))
                {
                    Close();
                }
                if (GUILayout.Button("Send"))
                {
                    editor.SendRating(rating, message);
                    Close();
                }
                GUILayout.EndHorizontal();
            }
        }

        [MenuItem("CONTEXT/TutoPack/Edit Tuto")]
        public static void EditTuto(MenuCommand command)
        {
            if (command.context is Challenge pack)
            {
                editTuto = true;
            }
        }

        [MenuItem("CONTEXT/TutoPack/Open Challenges")]
        public static void OpenChallenges(MenuCommand command)
        {
            //Updater_.Open();
        }
    }
}
