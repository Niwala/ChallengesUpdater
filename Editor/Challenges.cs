using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityEditor.SceneManagement;
using UnityEngine.Video;

using MenuStatus = UnityEngine.UIElements.DropdownMenuAction.Status;
using Cursor = UnityEngine.UIElements.Cursor;
using Object = UnityEngine.Object;

namespace Challenges
{
    public class Challenges : EditorWindow
    {
        public const string openSceneOnSelectPrefName = "Challenges.OpenSceneOnSelect";
        public const bool openSceneOnSelectDefaultValue = true;
        public const string checkUpdateEverydayPrefName = "Challenges.CheckUpdateEveryday";
        public const bool checkUpdateEverydayDefaultValue = true;
        public const string lastUpdatePrefPrefName = "Challenges.LastUpdate";
        public const string developerModePrefName = "Challenges.DeveloperMode";
        public const bool developerModeDefaultValue = false;


        //Static stuff

        [MenuItem("Challenges/Challenges")]
        public static void Open()
        {
            Challenges window = GetWindow<Challenges>();
            window.titleContent = new GUIContent("Challenges");
            window.Show();
        }

        public static void Open(TutoPack challenge)
        {
            Challenges window = GetWindow<Challenges>();
            window.titleContent = new GUIContent("Challenges");
            window.Show();
            window.OpenChallenge(challenge);
        }

        public static OnEditElement onEditElement;

        public delegate void OnEditElement(TutoPage page, int contentID);

        public delegate void OnLoadingStatus(bool loading, string message);

        public delegate void OnNetActionDone();

        public delegate void BuildContainer(string name);

        //---------------------------------

        public ChallengeSelectionPage selectionPage;
        public ChallengePage challengePage;
        public ChallengePreferencePage preferencePage;
        public ChallengeNotifications notifications;
        public UpdateToolbar updateToolbar;

        private void OnEnable()
        {
            //Initialize resources
            Updater_Editor.LoadResources();
            Updater_Editor.LoadIcons();
            minSize = new Vector2(250, 100);


            //Create UI
            VisualElement root = rootVisualElement;

            selectionPage = new ChallengeSelectionPage(this);
            root.Add(selectionPage);

            challengePage = new ChallengePage(this);
            root.Add(challengePage);

            preferencePage = new ChallengePreferencePage(this);
            root.Add(preferencePage);

            updateToolbar = new UpdateToolbar(this);
            updateToolbar.Stop();
            root.Add(updateToolbar);

            notifications = new ChallengeNotifications(this);
            root.Add(notifications);

            OpenPage(selectionPage);


            //Bind callbacks and start the process
            BindCallbacks();
            Updater_Editor.LoadCache();


            //Check updates of the day
            if (EditorPrefs.GetBool("Challenges.CheckUpdateEveryday", true))
            {
                string lastUpdate = EditorPrefs.GetString("Challenges.LastUpdate", "");
                string currentDay = DateTime.Now.Date.ToString();

                if (lastUpdate != currentDay)
                {
                    EditorPrefs.SetString("Challenges.LastUpdate", currentDay);
                    onCheckUpdaterVersionStart();
                    Updater_Editor.ChallengesUpdaterIsOutdated(OnCheckUpdaterVersionEnd);
                }
            }
        }

        private void OnFocus()
        {
            if (notifications == null)
                return;

            ShowNotifications(false);
        }

        private void BindCallbacks()
        {
            Updater_Editor.onStartLoading += updateToolbar.Start;
            Updater_Editor.onStopLoading += updateToolbar.Stop;
            Updater_Editor.onCacheUpdated += OnCacheUpdated;
            Updater_Editor.onCacheLoaded += OnCacheLoaded;
        }

        private void OpenPage(VisualElement page)
        {
            selectionPage.style.display = selectionPage == page ? DisplayStyle.Flex : DisplayStyle.None;
            challengePage.style.display = challengePage == page ? DisplayStyle.Flex : DisplayStyle.None;
            preferencePage.style.display = preferencePage == page ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void OpenSelectionPage()
        {
            OpenPage(selectionPage);
            selectionPage.Refresh();
        }

        public void OpenChallenge(TutoPack challenge)
        {
            //Open scene
            if (EditorPrefs.GetBool(Challenges.openSceneOnSelectPrefName, Challenges.openSceneOnSelectDefaultValue))
            {
                if (challenge.scene != null && challenge.scene is SceneAsset scene)
                {
                    Scene existingScene = EditorSceneManager.GetSceneByName(challenge.scene.name);
                    if (existingScene.name != challenge.scene.name)
                    {
                        EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
                        EditorSceneManager.OpenScene(AssetDatabase.GetAssetPath(challenge.scene), OpenSceneMode.Single);
                    }
                }
            }

            //Callback on open
            if (!string.IsNullOrEmpty(challenge.onOpen))
            {
                string typeName = challenge.onOpen.Split('.')[0];
                string methodName = challenge.onOpen.Split('.')[1];

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

            //Refresh gui
            challengePage.OpenChallenge(challenge);
            OpenPage(challengePage);
        }

        public void OpenPreferences()
        {
            OpenPage(preferencePage);
        }

        public void RefreshSelectionList()
        {
            selectionPage?.Refresh();
        }

        public void ShowNotifications(bool includeDismiss)
        {
            notifications.ShowNotifications(includeDismiss);
        }

        public void onCheckUpdaterVersionStart()
        {
            updateToolbar.Start("Check Challenges Updater version...");
        }

        public void OnCheckUpdaterVersionEnd(bool updatedOutdated)
        {
            updateToolbar.Stop();

            if (updatedOutdated)
            {
                notifications.AddNotification(new Updater_Editor.NotificationInfo(
                    "Challenges Package",
                    "Des mises à jours sont disponibles pour le package Challenges.",
                    "Mettre à jour",
                    Updater_Editor.UpdateTheUpdater));
            }
            else
            {
                Updater_Editor.UpdateCache();
            }
        }

        public void OnCacheUpdated()
        {
            updateToolbar.Stop();

            Updater_Editor.LoadCache();
        }

        public void OnCacheLoaded()
        {
            updateToolbar.Stop();
        }

        public static void SetCursor(VisualElement element, MouseCursor cursor)
        {
            object objCursor = new Cursor();
            PropertyInfo fields = typeof(Cursor).GetProperty("defaultCursorId", BindingFlags.NonPublic | BindingFlags.Instance);
            fields.SetValue(objCursor, (int)cursor);
            element.style.cursor = new StyleCursor((Cursor)objCursor);
        }

        public static VisualElement BuildElement(TutoPage.Content content)
        {
            VisualElement element = BuildElement(content, out TutoPage.Container containerAction);

            switch (containerAction)
            {
                case TutoPage.Container.Enter:
                    {
                        switch (content.type)
                        {
                            case TutoPage.Type.BeginCallout:
                                {
                                    VisualElement callout = new VisualElement();
                                    callout.SetPadding(10, 10, 10, 10);
                                    callout.SetMargin(0, 0, 0, 10);
                                    callout.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 1.0f);
                                    callout.SetBorderRadius(10, 10, 0, 0);
                                    return callout;
                                }

                            case TutoPage.Type.BeginFoldout:
                                {
                                    Foldout foldout = new Foldout() { text = content.text };
                                    foldout.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 1.0f);
                                    foldout.SetBorderRadius(10, 10, 0, 0);
                                    foldout.Add(foldout);
                                    foldout.SetEnabled(false);
                                    return foldout;
                                }

                            default:
                                return new TextElement() { text = "Exit container " };
                                break;
                        }
                    }
                case TutoPage.Container.Exit:
                    {
                        switch (content.type)
                        {
                            case TutoPage.Type.EndCallout:
                                {
                                    VisualElement callout = new VisualElement();
                                    callout.SetPadding(10, 10, 10, 10);
                                    callout.SetMargin(10, 0, 0, 0);
                                    callout.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 1.0f);
                                    callout.SetBorderRadius(0, 0, 10, 10);
                                    return callout;
                                }

                            case TutoPage.Type.EndFoldout:
                                {
                                    TextElement foldout = new TextElement() { text = "Exit foldout" };
                                    foldout.style.unityFontStyleAndWeight = FontStyle.Italic;
                                    foldout.style.marginBottom = 3;
                                    foldout.style.paddingLeft = 7;
                                    foldout.SetBorderRadius(0, 0, 10, 10);
                                    foldout.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 1.0f);
                                    foldout.SetEnabled(false);
                                    return foldout;
                                }

                            default:
                                return new TextElement() { text = "Exit container " };
                                break;
                        }
                    }
                default:
                    return element;
            }
        }

        public static VisualElement BuildElement(TutoPage.Content content, out TutoPage.Container containerAction)
        {
            containerAction = TutoPage.Container.None;

            switch (content.type)
            {
                case TutoPage.Type.Title:
                    {
                        TextElement title = new TextElement();
                        title.style.fontSize = 24;
                        title.style.marginTop = 12;
                        title.text = content.text;

                        return title;
                    }

                case TutoPage.Type.Subtitle:
                    {
                        TextElement subtitle = new TextElement();
                        subtitle.style.fontSize = 18;
                        subtitle.style.marginTop = 6;
                        subtitle.text = content.text;

                        return subtitle;
                    }

                case TutoPage.Type.Link:
                    {
                        TextElement textElement = new TextElement();
                        textElement.style.fontSize = 14;
                        textElement.style.marginTop = 3;
                        textElement.style.opacity = 0.9f;
                        SetCursor(textElement, MouseCursor.Link);

                        string[] values = content.text.Split('|');

                        if (!string.IsNullOrEmpty(content.altText))
                            values = new string[] { content.text, content.altText };

                        if (values.Length < 2)
                            textElement.text = content.text;
                        else
                            textElement.text = $"<a href={values[1]}>{values[0]}</a>";

                        return textElement;
                    }

                case TutoPage.Type.Text:
                    {
                        Texture2D img = content.obj as Texture2D;
                        string txt = content.text;

                        if (img != null && !string.IsNullOrEmpty(content.text))
                        {
                            VisualElement container = new VisualElement();
                            container.Add(GetTextElement());
                            container.Add(GetImageElement());
                            return container;
                        }

                        if (img != null)
                        {
                            return GetImageElement();
                        }
                        else
                        {
                            return GetTextElement();
                        }

                        VisualElement GetImageElement()
                        {
                            VisualElement imageElement = new VisualElement();
                            imageElement.style.backgroundImage = img;
                            imageElement.style.width = new Length(100, LengthUnit.Percent);
                            imageElement.style.height = 0;
                            imageElement.style.paddingTop = new Length(100 * ((float)img.height / img.width), LengthUnit.Percent);

                            VisualElement imgContainer = new VisualElement();
                            imgContainer.style.maxWidth = img.width;
                            imgContainer.Add(imageElement);

                            return imgContainer;
                        }

                        VisualElement GetTextElement()
                        {
                            TextElement textElement = new TextElement();
                            textElement.style.fontSize = 14;
                            textElement.style.marginTop = 3;
                            textElement.style.opacity = 0.9f;
                            textElement.text = txt;
                            return textElement;
                        }
                    }

                case TutoPage.Type.Button:
                    {
                        Button button = new Button();
                        button.text = content.text;
                        button.style.alignSelf = Align.FlexStart;
                        button.style.height = 24;
                        button.style.marginLeft = -2;
                        button.style.marginRight = 0;
                        button.style.marginTop = 3;
                        button.style.marginBottom = 3;
                        button.clicked += OnClick;

                        void OnClick()
                        {
                            Object loaded = AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GetAssetPath(content.obj));

                            switch (loaded)
                            {
                                case VideoClip clip:
                                    UnityEditor.PopupWindow.Show(button.worldBound, new TutoPack_Editor.PopupVideo(clip));
                                    break;

                                case Texture2D tex2D:
                                    UnityEditor.PopupWindow.Show(button.worldBound, new TutoPack_Editor.PopupImage(tex2D));
                                    break;

                                case SceneAsset scene:
                                    EditorSceneManager.OpenScene(AssetDatabase.GetAssetPath(scene), OpenSceneMode.Single);
                                    break;

                                case TutoPack subChallenge:
                                    Challenges.Open(subChallenge);
                                    break;

                                default:
                                    Selection.activeObject = content.obj;
                                    break;
                            }
                        }


                        return button;
                    }

                case TutoPage.Type.Space:
                    {
                        VisualElement space = new VisualElement();
                        space.style.height = content.padding;
                        return space;
                    }

                case TutoPage.Type.Code:
                    {
                        Foldout fold = new Foldout();
                        fold.style.backgroundColor = new Color(0.17f, 0.17f, 0.17f, 1.0f);
                        fold.SetBorder(1, 5, new Color(0, 0, 0, 0.6f));
                        fold.text = content.altText;
                        fold.SetMargin(5, 0, 0, 5);

                        TextElement textElement = new TextElement();
                        textElement.style.fontSize = 14;
                        textElement.SetPadding(5, 5, 5, 5);
                        textElement.SetMargin(0, -14, 0, 3);
                        textElement.style.opacity = 0.9f;
                        textElement.text = content.text;
                        textElement.style.backgroundColor = new Color(0.13f, 0.13f, 0.13f, 1.0f);
                        textElement.selection.isSelectable = true;
                        fold.Add(textElement);

                        Button copyBtn = new Button();
                        copyBtn.style.backgroundImage = (Texture2D)EditorGUIUtility.IconContent("d_UnityEditor.ConsoleWindow").image;
                        copyBtn.style.position = Position.Absolute;
                        copyBtn.style.top = 2;
                        copyBtn.style.right = -3;
                        copyBtn.style.height = 16;
                        copyBtn.style.width = 16;
                        copyBtn.style.backgroundColor = Color.clear;

                        Toggle toggle = fold.Q<Toggle>();
                        toggle.style.height = 17;
                        toggle.labelElement.style.fontSize = 13;
                        toggle.labelElement.style.marginTop = 1;
                        toggle.labelElement.style.opacity = 0.7f;
                        toggle.Add(copyBtn);

                        return fold;
                    }

                case TutoPage.Type.Hint:
                    {
                        bool toggle = false;

                        VisualElement hint = new VisualElement();
                        hint.style.borderBottomLeftRadius = hint.style.borderBottomRightRadius = hint.style.borderTopRightRadius = hint.style.borderTopLeftRadius = 7;
                        hint.style.borderBottomColor = hint.style.borderTopColor = hint.style.borderLeftColor = hint.style.borderRightColor = new Color(0, 0, 0, 0.6f);
                        hint.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 1.0f);
                        hint.style.minHeight = 30;
                        hint.style.marginBottom = hint.style.marginTop = 5;

                        TextElement hintMsg = new TextElement();
                        hintMsg.text = "Clic to reveal";
                        hintMsg.style.fontSize = 14;
                        hintMsg.style.opacity = 0.3f;
                        hintMsg.style.unityFontStyleAndWeight = FontStyle.Italic;
                        hintMsg.StretchToParentSize();
                        hintMsg.style.unityTextAlign = TextAnchor.MiddleCenter;
                        hint.Add(hintMsg);

                        TextElement hintContent = new TextElement();
                        hintContent.text = content.text;
                        hintContent.style.fontSize = 13;
                        hintContent.style.opacity = 0.8f;
                        hintContent.style.visibility = Visibility.Hidden;

                        hintContent.style.paddingBottom = hintContent.style.paddingLeft = hintContent.style.paddingRight = hintContent.style.paddingTop = 5;

                        float height = hintContent.resolvedStyle.height;

                        //hintMsg.style.unityTextAlign = TextAnchor.MiddleCenter;
                        hint.Add(hintContent);

                        hint.RegisterCallback<MouseDownEvent>(ToggleHint);


                        void ToggleHint(MouseDownEvent e)
                        {
                            if (e.button == 0)
                            {
                                if (!toggle)
                                {
                                    hintMsg.style.visibility = Visibility.Hidden;
                                    hintContent.style.visibility = Visibility.Visible;
                                    hint.style.backgroundColor = Color.clear;
                                    hint.style.borderBottomWidth = hint.style.borderTopWidth = hint.style.borderLeftWidth = hint.style.borderRightWidth = 1;
                                }
                                else
                                {
                                    hintMsg.style.visibility = Visibility.Visible;
                                    hintContent.style.visibility = Visibility.Hidden;
                                    hint.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 1.0f);
                                    hint.style.borderBottomWidth = hint.style.borderTopWidth = hint.style.borderLeftWidth = hint.style.borderRightWidth = 0;
                                }
                                toggle = !toggle;
                            }
                        }


                        //VisualElement imageElement = new VisualElement();
                        //imageElement.style.width = new Length(100, LengthUnit.Percent);
                        //imageElement.style.height = 0;
                        //imageElement.style.paddingTop = new Length(100 * ((float)img.height / img.width), LengthUnit.Percent);


                        return hint;
                    }

                case TutoPage.Type.Shader:
                    {
                        Object loaded = AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GetAssetPath(content.obj));
                        ShaderContainer shaderContainer = new ShaderContainer(loaded as Material, content.text);
                        return shaderContainer;
                    }

                case TutoPage.Type.Separator:
                    {
                        VisualElement separator = new VisualElement();
                        separator.style.height = 0;
                        separator.style.borderTopWidth = 1;
                        separator.style.borderBottomWidth = 1;
                        separator.style.borderTopColor = new Color(0, 0, 0, 0.4f);
                        separator.style.borderBottomColor = new Color(1, 1, 1, 0.2f);
                        separator.style.marginTop = 3;
                        separator.style.marginBottom = 6;
                        return separator;
                    }

                case TutoPage.Type.BeginCallout:
                    {
                        containerAction = TutoPage.Container.Enter;

                        VisualElement callout = new VisualElement();
                        callout.SetPadding(10, 10, 10, 10);
                        callout.SetMargin(10, 0, 0, 10);
                        callout.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 1.0f);
                        callout.SetBorderRadius(10);
                        callout.name = "Container";
                        return callout;
                    }

                case TutoPage.Type.EndCallout:
                    {
                        containerAction = TutoPage.Container.Exit;
                        return null;
                    }

                case TutoPage.Type.BeginFoldout:
                    {
                        containerAction = TutoPage.Container.Enter;

                        VisualElement foldoutContainer = new VisualElement();
                        foldoutContainer.SetBorder(1, 10, new Color(0, 0, 0, 0.5f));
                        foldoutContainer.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1.0f);

                        //Fold
                        Foldout foldout = new Foldout() { text = content.text };
                        foldout.SetBorderRadius(10, 10, 10, 10);
                        foldout.style.backgroundColor = new Color(0, 0, 0, 0.1f);
                        foldout.value = false;
                        foldoutContainer.Add(foldout);

                        //Content
                        VisualElement foldoutContent = new VisualElement();
                        foldoutContent.name = "Container";
                        foldoutContent.SetPadding(5, 10, 10, 0);
                        foldoutContent.style.display = DisplayStyle.None;
                        foldoutContainer.Add(foldoutContent);

                        //Line
                        VisualElement line = new VisualElement();
                        line.style.flexGrow = 1;
                        line.style.backgroundColor = new Color(0, 0, 0, 0.5f);
                        line.style.height = 1;
                        line.style.marginLeft = -10;
                        line.style.marginRight = -10;
                        line.style.marginBottom = 5;
                        foldoutContent.Add(line);

                        foldout.RegisterValueChangedCallback(SwitchFold);

                        void SwitchFold(ChangeEvent<bool> e)
                        {
                            if (e.newValue)
                            {
                                foldout.SetBorderRadius(10, 10, 0, 0);
                                foldoutContent.style.display = DisplayStyle.Flex;
                            }
                            else
                            {
                                foldout.SetBorderRadius(10, 10, 10, 10);
                                foldoutContent.style.display = DisplayStyle.None;
                            }
                        }

                        return foldoutContainer;
                    }

                case TutoPage.Type.EndFoldout:
                    {
                        containerAction = TutoPage.Container.Exit;
                        return null;
                    }
                    break;
            }

            return null;
        }
    }

    public class ChallengeSelectionPage : VisualElement
    {
        private Challenges window;
        private ScrollView scrollView;
        private VisualElement content;
        private VisualElement viewPort;
        private List<ChallengeTile> tiles = new List<ChallengeTile>();

        public ChallengeSelectionPage(Challenges window)
        {
            this.window = window;

            //Toolbar
            SelectorToolbar toolbar = new SelectorToolbar(window);
            Add(toolbar);

            //Tiles
            scrollView = new ScrollView();
            scrollView.RegisterCallback((AttachToPanelEvent e) => LoadViewPort());
            Add(scrollView);
            content = new VisualElement();
            scrollView.Add(content);
            Refresh();
        }

        public void Refresh()
        {
            content.Clear();
            tiles.Clear();
            foreach (var status in Updater_Editor.filteredChallengeList)
            {
                ChallengeTile tile = new ChallengeTile(window, status);
                tiles.Add(tile);
                content.Add(tile);
                if (viewPort != null)
                    tile.SetViewPort(viewPort);
            }
        }

        private void LoadViewPort()
        {
            viewPort = scrollView.Q<VisualElement>("unity-content-viewport");
            foreach (var tile in tiles)
            {
                tile.SetViewPort(viewPort);
            }
        }

        private static void SetRectToPreviewMaterial(string name, Rect r)
        {
            Updater_Editor.previewMaterial.SetVector(name, new Vector4(r.xMin, r.yMin, r.xMax, r.yMax));
        }

        public class SelectorToolbar : Toolbar
        {
            private Challenges window;
            private ToolbarMenu teacherMenu;
            private ToolbarSearchField searchField;
            private ToolbarMenu contextMenu;

            public SelectorToolbar(Challenges window)
            {
                this.window = window;

                //Toolbar - Teacher menu
                teacherMenu = new ToolbarMenu();
                teacherMenu.style.width = 175;
                teacherMenu.style.minWidth = 100;
                HashSet<string> teachers = Updater_Editor.GetTeachers();
                teacherMenu.menu.AppendAction("All", OnSelectTeacher, GetTeacherStatus, "");
                teacherMenu.menu.AppendSeparator();
                foreach (var teacher in teachers)
                {
                    teacherMenu.menu.AppendAction(teacher, OnSelectTeacher, GetTeacherStatus, teacher);
                }
                Add(teacherMenu);
                string t = string.IsNullOrEmpty(Updater_Editor.teacher) ? "" : Updater_Editor.teacher;
                OnSelectTeacher(new DropdownMenuAction("", null, null, t));


                //Toolbar - Search field
                searchField = new ToolbarSearchField();
                searchField.style.flexGrow = 1;
                searchField.style.flexShrink = 1;
                searchField.style.minWidth = 50;
                searchField.RegisterValueChangedCallback(OnSearchChange);
                Add(searchField);


                //Toolbar - Context menu
                contextMenu = new ToolbarMenu();
                contextMenu.style.width = 22;
                contextMenu.style.paddingRight = 4;
                contextMenu.style.flexShrink = 0;
                contextMenu.menu.AppendAction("Check Updates", CheckForUpdate, MenuStatus.Normal);
                contextMenu.menu.AppendAction("Show Notifications", ShowNotifications, MenuStatus.Normal);
                contextMenu.menu.AppendSeparator();
                contextMenu.menu.AppendAction("Get Amplify", GetAmplify, MenuStatus.Normal);
                contextMenu.menu.AppendAction("Documentation", Documentation, MenuStatus.Normal);
                contextMenu.menu.AppendSeparator();
                contextMenu.menu.AppendAction("Preferences", OnOpenPreferences, MenuStatus.Normal);

                Add(contextMenu);
            }

            private void OnSelectTeacher(DropdownMenuAction e)
            {
                //Update toolbar menu label
                string teacher = (string)e.userData;
                if (string.IsNullOrEmpty(teacher))
                    teacher = "All";
                teacherMenu.text = $"Teacher : {teacher}";

                //Save teacher in prefs
                EditorPrefs.SetString(TutoPack_Selector.lastTeacherPrefKey, (string)e.userData);

                //Apply filters on teacher
                Updater_Editor.teacher = (string)e.userData;
                Updater_Editor.UpdateFilteredStatus();
                window.RefreshSelectionList();
            }

            private MenuStatus GetTeacherStatus(DropdownMenuAction e)
            {
                string teacher = (string)e.userData;

                if (teacher == Updater_Editor.teacher)
                    return MenuStatus.Checked;
                return MenuStatus.Normal;
            }

            private void OnOpenPreferences(DropdownMenuAction e)
            {
                window.OpenPreferences();
            }

            private void CheckForUpdate(DropdownMenuAction e)
            {
                window.onCheckUpdaterVersionStart();
                Updater_Editor.ChallengesUpdaterIsOutdated(window.OnCheckUpdaterVersionEnd);
            }

            private void ShowNotifications(DropdownMenuAction e)
            {
                window.ShowNotifications(true);
            }

            private void GetAmplify(DropdownMenuAction e)
            {
                Updater_Editor.DownloadAmplify();
            }

            private void Documentation(DropdownMenuAction e)
            {
                Updater_Editor.OpenDocumentation();
            }

            private void OnSearchChange(ChangeEvent<string> e)
            {
                if (e.previousValue == e.newValue)
                    return;

                Updater_Editor.search = e.newValue;
                Updater_Editor.UpdateFilteredStatus();
                window.RefreshSelectionList();
            }
        }

        public class ChallengeTile : VisualElement
        {
            private Challenges window;
            private VisualElement cover;
            private Updater_Editor.Status status;
            private ChallengeBackground background;
            private VisualElement statusIcon;

            public ChallengeTile(Challenges window, Updater_Editor.Status status)
            {
                this.window = window;
                this.status = status;

                //Register events
                RegisterCallback<MouseEnterEvent>(MouseEnter);
                RegisterCallback<MouseLeaveEvent>(MouseLeave);
                RegisterCallback<MouseDownEvent>(MouseDown);

                //Size
                style.minWidth = 300;
                style.maxWidth = 500;
                style.height = 140;
                style.flexShrink = 0;
                style.flexGrow = 1;
                Challenges.SetCursor(this, MouseCursor.Link);

                //Border
                style.borderBottomLeftRadius = style.borderTopRightRadius = style.borderTopLeftRadius = style.borderBottomRightRadius = 5.0f;
                style.borderBottomColor = style.borderTopColor = style.borderLeftColor = style.borderRightColor = new Color(0, 0, 0, 0.7f);
                style.borderBottomWidth = style.borderTopWidth = style.borderLeftWidth = style.borderRightWidth = 1.0f;

                //Margins
                style.marginLeft = style.marginRight = 10;
                style.marginTop = style.marginBottom = 5;


                //Preview
                background = new ChallengeBackground(status.preview);
                Add(background);


                //Title
                TextElement text = new TextElement();
                text.style.marginLeft = 5;
                text.style.marginTop = 3;
                text.style.fontSize = 20;
                text.style.opacity = 0.8f;
                text.text = status.name;
                text.pickingMode = PickingMode.Ignore;
                Add(text);


                //Description
                TextElement description = new TextElement();
                description.style.marginLeft = 5;
                description.style.marginTop = 2;
                description.style.opacity = 0.8f;
                description.style.fontSize = 13;
                description.text = status.description;
                description.pickingMode = PickingMode.Ignore;
                Add(description);


                //Cover
                cover = new VisualElement();
                cover.pickingMode = PickingMode.Ignore;
                cover.style.position = Position.Absolute;
                cover.style.top = cover.style.bottom = cover.style.left = cover.style.right = 0;
                cover.style.backgroundColor = new Color(0.2f, 0.25f, 0.5f, 0.2f);
                cover.style.display = DisplayStyle.None;
                Add(cover);


                //Status icon
                statusIcon = new StatusIcon(status.status);
                statusIcon.style.position = Position.Absolute;
                statusIcon.style.width = statusIcon.style.height = 32;
                statusIcon.style.top = 9;
                statusIcon.style.right = 6;
                Add(statusIcon);
            }

            private void MouseEnter(MouseEnterEvent e)
            {
                cover.style.display = DisplayStyle.Flex;
                style.borderBottomColor = style.borderTopColor = style.borderLeftColor = style.borderRightColor = new Color(0, 0.7f, 1, 0.7f);
            }

            private void MouseLeave(MouseLeaveEvent e)
            {
                cover.style.display = DisplayStyle.None;
                style.borderBottomColor = style.borderTopColor = style.borderLeftColor = style.borderRightColor = new Color(0, 0, 0, 0.7f);
            }

            private void MouseDown(MouseDownEvent e)
            {
                if (e.button == 0)
                {
                    window.OpenChallenge(status.pack);
                }
                else if (e.button == 1)
                {
                    GenericMenu menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Open"), false, () => window.OpenChallenge(status.pack));
                    menu.AddItem(new GUIContent("Reset"), false, () => Updater_Editor.DownloadChallenge(status.name));


                    menu.ShowAsContext();
                }
                else if (e.button == 2)
                {
                    Selection.activeObject = status.pack;
                }

            }

            public void SetViewPort(VisualElement viewPort)
            {
                background.SetViewPort(viewPort);
            }
        }

        public class ChallengeBackground : IMGUIContainer
        {
            private Texture2D image;
            private Rect clipRect;
            private VisualElement viewPort;

            public ChallengeBackground(Texture2D image)
            {
                this.image = image;
                this.StretchToParentSize();
                pickingMode = PickingMode.Ignore;
                onGUIHandler += OnGUI;
            }

            public void SetViewPort(VisualElement viewPort)
            {
                this.viewPort = viewPort;
            }

            private void OnGUI()
            {
                Rect rect = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                float width = (image.width / (float)image.height) * rect.height;
                rect = Rect.MinMaxRect(rect.xMax - width, rect.yMin, rect.xMax, rect.yMax);

                Rect worldRect = worldBound;
                worldRect = Rect.MinMaxRect(worldRect.xMax - width, worldRect.yMin, worldRect.xMax, worldRect.yMax);

                Rect worldClip = worldRect;
                if (viewPort != null)
                    worldClip = viewPort.worldBound;

                if (Updater_Editor.previewMaterial == null)
                    Updater_Editor.LoadResources();

                Updater_Editor.previewMaterial.SetTexture("_Mask", Updater_Editor.previewMask);
                Updater_Editor.previewMaterial.SetInt("_ColorSpace", (int)PlayerSettings.colorSpace);
                SetRectToPreviewMaterial("_WorldRect", worldRect);
                SetRectToPreviewMaterial("_WorldClip", worldClip);
                EditorGUI.DrawPreviewTexture(rect, image, Updater_Editor.previewMaterial);
            }
        }
    }

    public class ChallengePage : VisualElement
    {
        private Challenges window;
        private Toolbar toolbar;

        private ToolbarMenu pageMenu;
        private PageButton nextPage;
        private PageButton previousPage;
        private VisualElement content;
        private ListView list;
        private ToolbarBreadcrumbs breadcrumbs;

        private TutoPack challenge;
        private TutoPage page;
        public int pageID { get; private set; }

        public ChallengePage(Challenges window)
        {
            this.window = window;
            this.style.flexGrow = 1;
            RegisterCallback<AttachToPanelEvent>(AttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(DetachFromPanel);

            //Header
            Toolbar header = new Toolbar();
            //header.style.height = 30;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.backgroundColor = new Color(1, 1, 1, 0.05f);

            //Header - Breadcrumbs
            breadcrumbs = new ToolbarBreadcrumbs();
            header.Add(breadcrumbs);

            Add(header);


            //Toolbar
            toolbar = new Toolbar();
            toolbar.style.height = 30;
            toolbar.style.justifyContent = Justify.SpaceBetween;
            //toolbar.style.backgroundColor = new Color(1, 1, 1, 0.05f);
            Add(toolbar);

            //Toolbar - Page commands
            nextPage = new PageButton(true);
            nextPage.clicked += () => { OpenPage(pageID - 1); };
            toolbar.Add(nextPage);

            pageMenu = new ToolbarMenu();
            pageMenu.style.backgroundColor = Color.clear;
            pageMenu.text = "Page : 1 / 1  ";
            toolbar.Add(pageMenu);

            previousPage = new PageButton(false);
            previousPage.clicked += () => { OpenPage(pageID + 1); };
            toolbar.Add(previousPage);


            //Content
            ScrollView scrollView = new ScrollView();
            content = new VisualElement();
            content.style.paddingBottom = 35;
            content.style.paddingLeft = content.style.paddingTop = content.style.paddingRight = 10;
            scrollView.Add(content);
            Add(scrollView);
        }

        private void AttachToPanel(AttachToPanelEvent e)
        {
            Challenges.onEditElement += OnEditElement;
        }

        private void DetachFromPanel(DetachFromPanelEvent e)
        {
            Challenges.onEditElement -= OnEditElement;
        }

        private void OnEditElement(TutoPage page, int contentID)
        {
            if (page == this.page)
                list.RefreshItem(contentID);
        }

        public void OpenChallenge(TutoPack challenge)
        {
            this.challenge = challenge;

            //List pages
            pageMenu.menu.ClearItems();
            for (int i = 0; i < challenge.pages.Count; i++)
            {
                pageMenu.menu.AppendAction($"Page {i + 1}", OnSelectPage, CheckPage, i);
            }

            int defaultPageID = EditorPrefs.GetInt($"Challenges.{challenge.name}.page", 0);
            OpenPage(defaultPageID);
        }

        public void OpenPage(int pageNumber)
        {
            //Clamp page number
            pageNumber = Mathf.Clamp(pageNumber, 0, challenge.pages.Count - 1);
            pageID = pageNumber;
            page = challenge.pages[pageID];
            pageMenu.text = $"Page {pageID + 1} / {challenge.pages.Count}  ";

            EditorPrefs.SetInt($"Challenges.{challenge.name}.page", pageID);

            RefreshBreadcrumb();
            RefreshPage();
        }

        public void RefreshPage()
        {
            content.Clear();

            bool developer = EditorPrefs.GetBool(Challenges.developerModePrefName, Challenges.developerModeDefaultValue);

            if (developer)
            {
                list = new ListView(page.content, -1, MakeVisualElement, BindElement);
                list.showFoldoutHeader = false;
                list.reorderMode = ListViewReorderMode.Animated;
                list.reorderable = true;
                list.showBorder = true;
                list.virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight;
                list.showAddRemoveFooter = false;
                list.showBoundCollectionSize = false;

                content.Add(list);

                VisualElement MakeVisualElement()
                {
                    return new ElementContainer();
                }

                void BindElement(VisualElement container, int contentID)
                {
                    ((ElementContainer)container).Bind(challenge, pageID, contentID);
                }
            }
            else
            {
                VisualElement container = null;

                //Add content
                for (int i = 0; i < page.content.Count; i++)
                {
                    VisualElement element = Challenges.BuildElement(page.content[i], out TutoPage.Container containerAction);

                    if (containerAction == TutoPage.Container.Exit)
                        container = null;

                    if (element == null)
                        continue;

                    if (container == null || containerAction == TutoPage.Container.Enter)
                        content.Add(element);
                    else
                        container.Add(element);

                    if (containerAction == TutoPage.Container.Enter)
                    {
                        container = element.Q<VisualElement>("Container");
                    }
                }
            }
        }

        private void RefreshBreadcrumb()
        {
            breadcrumbs.Clear();
            breadcrumbs.PushItem("Challenges", window.OpenSelectionPage);
            TutoPack parent = challenge.parent;
            int limit = 0;
            while (parent != null && limit < 10)
            {
                breadcrumbs.PushItem(parent.name, OpenParentChallenge);
                parent = parent.parent;
                limit++;
            }
            breadcrumbs.PushItem(challenge.name, OpenSiblingsMenu);
            breadcrumbs.PushItem($"Page {pageID + 1}", OpenPageMenu);

            void OpenPageMenu()
            {
                GenericMenu menu = new GenericMenu();
                for (int i = 0; i < challenge.pages.Count; i++)
                {
                    int j = i;
                    menu.AddItem(new GUIContent($"Page {j + 1}"), pageID == j, () => OpenPage(j));
                }
                menu.ShowAsContext();
            }

            void OpenSiblingsMenu()
            {
                if (challenge.parent != null)
                {
                    GenericMenu menu = new GenericMenu();
                    string[] guids = AssetDatabase.FindAssets($"t:{typeof(TutoPack)}");
                    for (int i = 0; i < guids.Length; i++)
                    {
                        TutoPack sibling = AssetDatabase.LoadAssetAtPath<TutoPack>(AssetDatabase.GUIDToAssetPath(guids[i]));
                        if (sibling != null && sibling.parent == challenge.parent)
                            menu.AddItem(new GUIContent(sibling.name), challenge == sibling, () => window.OpenChallenge(sibling));
                    }
                    menu.ShowAsContext();
                }
                else
                {
                    GenericMenu menu = new GenericMenu();
                    menu.AddItem(new GUIContent(challenge.name), true, () => { });
                    menu.ShowAsContext();
                }
            }

            void OpenParentChallenge()
            {
                window.OpenChallenge(challenge.parent);
            }

        }

        private void OnSelectPage(DropdownMenuAction e)
        {
            OpenPage((int)e.userData);
        }

        private MenuStatus CheckPage(DropdownMenuAction e)
        {
            return ((int)e.userData) == pageID ? MenuStatus.Checked : MenuStatus.Normal;
        }
    }

    public class ChallengePreferencePage : VisualElement
    {
        private Challenges window;
        private int fieldID;

        public ChallengePreferencePage(Challenges window)
        {
            this.window = window;

            //Toolbar
            Toolbar toolbar = new Toolbar();
            toolbar.style.marginBottom = 5;
            this.Add(toolbar);

            //Toolbar - title
            TextElement title = new TextElement();
            title.text = "Preferences";
            title.style.marginLeft = 6;
            title.style.marginTop = 2;
            title.style.fontSize = 13;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            toolbar.Add(title);


            //Properties
            VisualElement properties = new VisualElement();
            properties.style.marginLeft = properties.style.marginRight = properties.style.marginBottom = 16;
            Add(properties);

            //Properties
            Toggle openSceneOnSelection = new Toggle("Open Scene on Select");
            SetToggleStyle(openSceneOnSelection);
            LinkToggleToPref(openSceneOnSelection, Challenges.openSceneOnSelectPrefName, Challenges.openSceneOnSelectDefaultValue);
            properties.Add(openSceneOnSelection);

            Toggle everydayUpdates = new Toggle("Check for updates every day");
            SetToggleStyle(everydayUpdates);
            SetCheckEverydayUpdateToggle(everydayUpdates);
            properties.Add(everydayUpdates);

            Toggle developerMode = new Toggle("Developer mode");
            SetToggleStyle(developerMode);
            LinkToggleToPref(developerMode, Challenges.developerModePrefName, Challenges.developerModeDefaultValue);
            properties.Add(developerMode);

            Button closeBtn = new Button();
            closeBtn.text = "Close";
            closeBtn.style.alignSelf = Align.FlexEnd;
            closeBtn.style.height = 24;
            closeBtn.style.width = 120;
            closeBtn.style.marginRight = -2;
            closeBtn.clicked += window.OpenSelectionPage;
            properties.Add(closeBtn);
        }

        private void SetToggleStyle(Toggle toggle)
        {
            toggle.labelElement.style.flexGrow = 10000.0f;

            toggle.style.paddingLeft = 20 + toggle.style.marginLeft.value.value;
            toggle.style.marginLeft = -20;
            toggle.style.paddingRight = 20 + toggle.style.marginRight.value.value;
            toggle.style.marginRight = -20;
            toggle.style.height = 20;
            toggle.style.paddingTop = 2;


            if ((fieldID % 2) == 0)
                toggle.style.backgroundColor = new Color(0.5f, 0.5f, 0.5f, 0.1f);

            fieldID++;
        }

        private void LinkToggleToPref(Toggle toggle, string prefName, bool defaultValue)
        {
            toggle.SetValueWithoutNotify(EditorPrefs.GetBool(prefName, defaultValue));
            toggle.RegisterValueChangedCallback(OnChange);

            void OnChange(ChangeEvent<bool> e)
            {
                if (e.previousValue == e.newValue)
                    return;

                EditorPrefs.SetBool(prefName, e.newValue);
            }
        }

        private void SetCheckEverydayUpdateToggle(Toggle toggle)
        {
            toggle.SetValueWithoutNotify(EditorPrefs.GetBool(Challenges.checkUpdateEverydayPrefName, Challenges.checkUpdateEverydayDefaultValue));
            toggle.RegisterValueChangedCallback(OnChange);

            void OnChange(ChangeEvent<bool> e)
            {
                if (e.previousValue == e.newValue)
                    return;

                EditorPrefs.SetBool(Challenges.checkUpdateEverydayPrefName, e.newValue);
                EditorPrefs.SetString(Challenges.lastUpdatePrefPrefName, "");
            }
        }
    }

    public class ChallengeNotifications : VisualElement
    {
        private Challenges window;
        private List<Updater_Editor.NotificationInfo> notificationInfos = new List<Updater_Editor.NotificationInfo>();
        private List<Notification> notificationElements = new List<Notification>();
        private ScrollView scroll;

        public ChallengeNotifications(Challenges window)
        {
            this.window = window;

            //Style
            style.position = Position.Absolute;
            style.left = style.right = style.top = style.bottom = 0;
            style.backgroundColor = new Color(.05f, .05f, .05f, 0.8f);
            style.paddingTop = 7;

            //Scrollview
            scroll = new ScrollView();
            this.Add(scroll);

            ShowNotifications(false);
        }

        public void ShowNotifications(bool includeDismiss)
        {
            //Get notifications
            foreach (var item in notificationElements)
                item.parent.Remove(item);
            notificationInfos.Clear();
            notificationElements.Clear();

            Updater_Editor.GetNotifications(notificationInfos, includeDismiss);

            //Add notifications to scrollView
            foreach (var info in notificationInfos)
                AddNotification(info);

            RefreshVisibility();
        }

        public void AddNotification(Updater_Editor.NotificationInfo info)
        {
            Notification notification = new Notification(this, info);
            notificationElements.Add(notification);
            scroll.Add(notification);
            RefreshVisibility();
        }

        public void Close(Notification notification)
        {
            Updater_Editor.DismissNotification(notification.info);
            notification.parent.Remove(notification);
            notificationElements.Remove(notification);
            RefreshVisibility();
        }

        public void RefreshVisibility()
        {
            style.display = notificationElements.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;
        }

        public class Notification : VisualElement
        {
            private ChallengeNotifications container;
            public Updater_Editor.NotificationInfo info;

            public Notification(ChallengeNotifications container, Updater_Editor.NotificationInfo info)
            {
                this.container = container;
                this.info = info;

                //Size
                style.minWidth = 150;
                style.maxWidth = 512;
                style.minHeight = 70;
                style.flexShrink = 0;
                style.flexGrow = 0;
                style.paddingBottom = 5;
                style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 1.0f);

                //Border
                style.borderBottomLeftRadius = style.borderTopRightRadius = style.borderTopLeftRadius = style.borderBottomRightRadius = 5.0f;
                style.borderBottomColor = style.borderTopColor = style.borderLeftColor = style.borderRightColor = new Color(0, 0, 0, 0.7f);
                style.borderBottomWidth = style.borderTopWidth = style.borderLeftWidth = style.borderRightWidth = 1.0f;

                //Margins
                style.marginLeft = style.marginRight = 10;
                style.marginTop = style.marginBottom = 5;

                //Header
                VisualElement header = new VisualElement();
                header.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1.0f);
                header.style.borderBottomColor = new Color(0, 0, 0, 0.6f);
                header.style.borderBottomWidth = 1;
                header.style.height = 28;
                header.style.flexDirection = FlexDirection.Row;
                header.style.justifyContent = Justify.SpaceBetween;
                Add(header);

                //Title
                TextElement text = new TextElement();
                text.style.marginLeft = 5;
                text.style.marginTop = 3;
                text.style.marginBottom = 5;
                text.style.fontSize = 18;
                text.style.opacity = 0.8f;
                text.text = info.title;
                text.style.flexWrap = Wrap.NoWrap;
                text.style.overflow = Overflow.Hidden;
                text.style.textOverflow = TextOverflow.Ellipsis;
                header.Add(text);

                //Close btn
                CloseButton closeButton = new CloseButton();
                closeButton.clicked += () => { container.Close(this); };
                closeButton.style.backgroundColor = Color.clear;
                closeButton.style.borderRightWidth = closeButton.style.borderTopWidth = closeButton.style.borderBottomWidth = 0;
                closeButton.style.marginBottom = closeButton.style.marginTop = closeButton.style.marginRight = 0;
                closeButton.style.borderLeftWidth = 1;
                closeButton.style.borderLeftColor = new Color(0, 0, 0, 0.6f);
                closeButton.style.width = 28;
                header.Add(closeButton);


                //Description
                TextElement description = new TextElement();
                description.style.marginLeft = 5;
                description.style.marginTop = 2;
                description.style.opacity = 0.8f;
                description.style.fontSize = 13;
                description.text = info.message;
                Add(description);


                //Commands
                VisualElement commands = new VisualElement();
                commands.style.flexDirection = FlexDirection.Row;
                commands.style.justifyContent = Justify.FlexEnd;
                Add(commands);

                //Action button
                Button actionButton = new Button();
                actionButton.text = info.actionName;
                actionButton.clicked += () => { info.action.Invoke(); };
                commands.Add(actionButton);
            }
        }
    }

    public class UpdateToolbar : Toolbar
    {
        private Challenges window;
        private LoadingBar loadingBar;

        public UpdateToolbar(Challenges window)
        {
            this.window = window;

            style.position = Position.Absolute;
            style.left = 0;
            style.right = 0;
            style.bottom = 0;
            style.height = 20;

            style.borderTopColor = new Color(0, 0, 0, 0.6f);
            style.borderTopWidth = 1.0f;


            loadingBar = new LoadingBar();
            Add(loadingBar);
        }

        public void Stop()
        {
            style.display = DisplayStyle.None;
        }

        public void Start(string message)
        {
            loadingBar.text.text = message;
            style.display = DisplayStyle.Flex;
        }

        public void SetLoading(bool inLoading, string message)
        {
            if (inLoading)
                Start(message);
            else
                Stop();
        }

        public class LoadingBar : VisualElement
        {
            public TextElement text;
            private VisualElement barBack;
            private VisualElement bar;
            private const int width = 200;
            private const float speed = 3;

            public LoadingBar()
            {
                text = new TextElement();
                text.style.position = Position.Absolute;
                text.style.left = text.style.right = text.style.bottom = 0;
                text.style.height = 20;
                text.style.unityTextAlign = TextAnchor.MiddleCenter;
                text.style.opacity = 0.6f;
                text.style.unityFontStyleAndWeight = FontStyle.Italic;
                text.text = "Updating : Introduction Challenge";
                Add(text);

                style.position = Position.Absolute;
                style.left = style.right = style.bottom = 0;
                style.height = 20;

                barBack = new VisualElement();
                barBack.style.position = Position.Absolute;
                barBack.style.bottom = barBack.style.left = barBack.style.right = 0;
                barBack.style.height = 2;
                barBack.style.backgroundColor = new Color(0.0f, 0.0f, 0.0f, 0.7f);
                Add(barBack);

                bar = new VisualElement();
                bar.style.position = Position.Absolute;
                bar.style.bottom = 0;
                bar.style.width = width;
                bar.style.height = 2;
                bar.style.backgroundColor = new Color(0.4f, 0.8f, 1.0f, 0.7f);
                Add(bar);

                RegisterCallback<AttachToPanelEvent>(OnEnable);
                RegisterCallback<DetachFromPanelEvent>(OnDisable);
            }

            private void OnEnable(AttachToPanelEvent e)
            {
                EditorApplication.update += Refresh;
            }

            private void OnDisable(DetachFromPanelEvent e)
            {
                EditorApplication.update -= Refresh;
            }

            private void Refresh()
            {
                bar.style.left = ((float)(EditorApplication.timeSinceStartup % speed) / speed) * (barBack.resolvedStyle.width + width) - width;
            }
        }
    }

    public class CloseButton : VisualElement
    {
        private Color normalColor = Color.clear;
        private Color hoverColor = new Color(0.4f, 0, 0, 0.4f);
        private Color checkColor = new Color(0.2f, 0, 0, 0.4f);

        private bool mouseInside;
        public TextElement label;

        public System.Action clicked;

        public CloseButton()
        {
            style.backgroundColor = normalColor;

            RegisterCallback<MouseEnterEvent>(MouseEnter);
            RegisterCallback<MouseLeaveEvent>(MouseLeave);
            RegisterCallback<MouseDownEvent>(MouseDown);
            RegisterCallback<MouseUpEvent>(MouseUp);

            label = new TextElement();
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.fontSize = 18;
            label.style.opacity = 0.5f;
            label.style.paddingTop = 2;
            label.text = "✕";
            Add(label);
        }

        private void MouseEnter(MouseEnterEvent e)
        {
            style.backgroundColor = hoverColor;
            label.style.opacity = 1.0f;
            mouseInside = true;
        }

        private void MouseLeave(MouseLeaveEvent e)
        {
            style.backgroundColor = normalColor;
            label.style.opacity = 0.5f;
            mouseInside = false;
        }

        private void MouseDown(MouseDownEvent e)
        {
            style.backgroundColor = checkColor;
            label.style.opacity = 0.3f;
        }

        private void MouseUp(MouseUpEvent e)
        {
            style.backgroundColor = mouseInside ? hoverColor : normalColor;
            label.style.opacity = mouseInside ? 1.0f : 0.5f;

            clicked?.Invoke();
        }
    }

    public class PageButton : VisualElement
    {
        private Color normalColor = Color.clear;
        private Color hoverColor = new Color(0.4f, 0.4f, 0.4f, 1);
        private Color checkColor = new Color(0.1f, 0.1f, 0.1f, 1);
        private float labelOpacity = 0.5f;

        private bool mouseInside;
        public VisualElement icon;

        public System.Action clicked;

        public PageButton(bool left)
        {
            style.backgroundColor = normalColor;
            style.borderLeftWidth = left ? 0 : 1;
            style.borderRightWidth = left ? 1 : 0;
            style.borderLeftColor = style.borderRightColor = new Color(0, 0, 0, 0.6f);
            style.width = 30;

            RegisterCallback<MouseEnterEvent>(MouseEnter);
            RegisterCallback<MouseLeaveEvent>(MouseLeave);
            RegisterCallback<MouseDownEvent>(MouseDown);
            RegisterCallback<MouseUpEvent>(MouseUp);

            icon = new VisualElement();
            icon.style.backgroundImage = (Texture2D)(left ? EditorGUIUtility.IconContent("d_tab_prev@2x") : EditorGUIUtility.IconContent("d_tab_next@2x")).image;
            icon.StretchToParentSize();
            icon.style.top = -1;
            icon.style.left = -1;
            icon.style.width = 32;
            icon.style.height = 32;
            icon.style.opacity = labelOpacity;

            Add(icon);
        }

        private void MouseEnter(MouseEnterEvent e)
        {
            style.backgroundColor = hoverColor;
            icon.style.opacity = 1.0f;
            mouseInside = true;
        }

        private void MouseLeave(MouseLeaveEvent e)
        {
            style.backgroundColor = normalColor;
            icon.style.opacity = labelOpacity;
            mouseInside = false;
        }

        private void MouseDown(MouseDownEvent e)
        {
            style.backgroundColor = checkColor;
            icon.style.opacity = 0.3f;
        }

        private void MouseUp(MouseUpEvent e)
        {
            style.backgroundColor = mouseInside ? hoverColor : normalColor;
            icon.style.opacity = mouseInside ? 1.0f : labelOpacity;

            clicked?.Invoke();
        }
    }

    public class ElementContainer : VisualElement
    {
        private TutoPack challenge;
        private TutoPage page;
        private int pageID;
        private int contentID;

        public ElementContainer()
        {
            RegisterCallback<MouseDownEvent>(OnMouseDown);

            style.borderLeftColor = new Color(0, 0, 0, 0.4f);
            style.borderLeftWidth = 1;
            style.marginLeft = -7;
            style.paddingLeft = 7;
            style.flexGrow = 1;
            this.StretchToParentSize();
        }

        public void Bind(TutoPack challenge, int pageID, int contentID)
        {
            this.challenge = challenge;
            this.page = challenge.pages[pageID];
            this.pageID = pageID;
            this.contentID = contentID;

            parent.style.paddingBottom = parent.style.paddingTop = 0;

            Clear();
            VisualElement content = Challenges.BuildElement(page.content[contentID]);
            if (content != null)
                Add(content);
            else
                Add(new TextElement() { text = $"Unknow type : {page.content[contentID].type}" });
        }

        void OnMouseDown(MouseDownEvent e)
        {
            switch (e.button)
            {
                case 0:
                    ChallengeElementContainer.Open(page, contentID);
                    break;

                case 1:
                    OpenContextualMenu();
                    break;
            }

        }

        void OpenContextualMenu()
        {
            GenericMenu menu = new GenericMenu();

            string[] names = System.Enum.GetNames(typeof(TutoPage.Type));
            for (int i = 0; i < names.Length; i++)
            {
                int j = i;
                menu.AddItem(new GUIContent($"Insert/{ObjectNames.NicifyVariableName(names[i])}"), false, () => InsertNew((TutoPage.Type)j));
            }

            menu.AddItem(new GUIContent("Duplicate"), false, Duplicate);
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Delete"), false, Delete);
            menu.AddItem(new GUIContent("Delete Page"), false, DeletePage);

            menu.ShowAsContext();
        }

        private void InsertNew(TutoPage.Type type)
        {
            SerializedObject obj = new SerializedObject(challenge);
            obj.UpdateIfRequiredOrScript();
            page.content.RemoveAt(contentID);

            obj.FindProperty(nameof(challenge.pages))
                .GetArrayElementAtIndex(pageID)
                .FindPropertyRelative(nameof(page.content))
                .InsertArrayElementAtIndex(contentID);

            obj.ApplyModifiedProperties();
            RefreshPage();
        }

        private void Duplicate()
        {
            SerializedObject obj = new SerializedObject(challenge);
            obj.UpdateIfRequiredOrScript();
            page.content.RemoveAt(contentID);

            obj.FindProperty(nameof(challenge.pages))
                .GetArrayElementAtIndex(pageID)
                .FindPropertyRelative(nameof(page.content))
                .InsertArrayElementAtIndex(contentID);

            obj.ApplyModifiedProperties();
            RefreshPage();
        }

        private void Delete()
        {
            if (page.content.Count == 1)
            {
                DeletePage();
                return;
            }

            SerializedObject obj = new SerializedObject(challenge);
            obj.UpdateIfRequiredOrScript();
            page.content.RemoveAt(contentID);

            obj.FindProperty(nameof(challenge.pages))
                .GetArrayElementAtIndex(pageID)
                .FindPropertyRelative(nameof(page.content))
                .DeleteArrayElementAtIndex(contentID);

            obj.ApplyModifiedProperties();
            RefreshPage();
        }

        private void DeletePage()
        {
            SerializedObject obj = new SerializedObject(challenge);
            obj.UpdateIfRequiredOrScript();
            page.content.RemoveAt(contentID);

            obj.FindProperty(nameof(challenge.pages))
                .DeleteArrayElementAtIndex(pageID);

            obj.ApplyModifiedProperties();

            ChallengePage pageElement = GetFirstAncestorOfType<ChallengePage>();
            pageElement.OpenPage(pageElement.pageID - 1);
        }

        private void RefreshPage()
        {
            ChallengePage pageElement = GetFirstAncestorOfType<ChallengePage>();
            pageElement.RefreshPage();
        }

    }

    public class ShaderContainer : IMGUIContainer
    {
        private Material material;
        private string text;

        public ShaderContainer(Material material, string text)
        {
            this.text = text;
            this.material = material;
            onGUIHandler += OnGUI;
        }

        private void OnGUI()
        {
            if (material != null)
            {
                //Properties
                GUILayout.BeginVertical();
                Shader shader = material.shader;

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
                            DrawMatProp(() => EditorGUILayout.ColorField(new GUIContent(name), material.GetColor(propertyID), false, true, hdr), (Color c) => material.SetColor(propertyID, c));
                            break;
                        case UnityEngine.Rendering.ShaderPropertyType.Vector:
                            DrawMatProp(() => EditorGUILayout.Vector4Field(name, material.GetVector(propertyID)), (Vector4 v) => material.SetVector(propertyID, v));
                            break;
                        case UnityEngine.Rendering.ShaderPropertyType.Float:
                            if (isToggle)
                                DrawMatProp(() => EditorGUILayout.Toggle(name, material.GetFloat(propertyID) > 0.5f), (bool b) => material.SetFloat(propertyID, b ? 1.0f : 0.0f));
                            else
                                DrawMatProp(() => EditorGUILayout.FloatField(name, material.GetFloat(propertyID)), (float f) => material.SetFloat(propertyID, f));
                            break;
                        case UnityEngine.Rendering.ShaderPropertyType.Range:
                            Vector2 limits = shader.GetPropertyRangeLimits(i);
                            DrawMatProp(() => EditorGUILayout.Slider(name, material.GetFloat(propertyID), limits.x, limits.y), (float f) => material.SetFloat(propertyID, f));
                            break;
                        case UnityEngine.Rendering.ShaderPropertyType.Texture:
                            DrawMatProp(() => EditorGUILayout.ObjectField(name, material.GetTexture(propertyID), typeof(Texture), false), (Object t) => material.SetTexture(propertyID, t as Texture));
                            break;
                    }
                }

                double t = EditorApplication.timeSinceStartup;
                float ft = (float)(t % 1000.0);
                material.SetFloat("_EditorTime", ft);

                //Preview
                Rect r;
                if (!string.IsNullOrEmpty(text) && int.TryParse(text, out int height))
                    r = GUILayoutUtility.GetRect(1f, height + 10);
                else
                    r = GUILayoutUtility.GetAspectRect(1f);
                r.Set(r.x + 5, r.y + 5, r.width - 10, r.height - 10);
                GUI.Box(r, "");
                EditorGUI.DrawPreviewTexture(r, Texture2D.whiteTexture, material);
                GUILayout.EndVertical();
            }

        }
    }

    public class StatusIcon : VisualElement
    {
        private float normalOpacity = 0.7f;
        private float hoverOpacity = 1.0f;
        private float checkOpacity = 0.5f;
        private bool mouseInside;

        public Action clicked;

        public StatusIcon(Updater_Editor.ChallengeStatus status)
        {
            style.backgroundImage = Updater_Editor.GetStatusIcon(status);
            style.opacity = normalOpacity;
            tooltip = Updater_Editor.GetStatusTooltip(status);

            RegisterCallback<MouseEnterEvent>(MouseEnter);
            RegisterCallback<MouseLeaveEvent>(MouseLeave);
            RegisterCallback<MouseDownEvent>(MouseDown);
            RegisterCallback<MouseUpEvent>(MouseUp);
        }

        private void MouseEnter(MouseEnterEvent e)
        {
            style.opacity = hoverOpacity;
            mouseInside = true;
        }

        private void MouseLeave(MouseLeaveEvent e)
        {
            style.opacity = normalOpacity;
            mouseInside = false;
        }

        private void MouseDown(MouseDownEvent e)
        {
            style.opacity = checkOpacity;
        }

        private void MouseUp(MouseUpEvent e)
        {
            style.opacity = mouseInside ? hoverOpacity : normalOpacity;
            clicked?.Invoke();
        }
    }

    delegate T GetValue<T>();
    delegate void SetValue<T>(T value);

    public static class UIExtension
    {
        public static void SetBorderRadius(this VisualElement element, float value)
        {
            element.SetBorderRadius(value, value, value, value);
        }

        public static void SetBorderRadius(this VisualElement element, float topLeft, float topRight, float bottomRight, float bottomLeft)
        {
            element.style.borderTopLeftRadius = topLeft;
            element.style.borderTopRightRadius = topRight;
            element.style.borderBottomRightRadius = bottomRight;
            element.style.borderBottomLeftRadius = bottomLeft;
        }

        public static void SetBorderColor(this VisualElement element, Color color)
        {
            element.style.borderBottomColor = color;
            element.style.borderLeftColor = color;
            element.style.borderRightColor = color;
            element.style.borderTopColor = color;
        }

        public static void SetBorderWidth(this VisualElement element, float width)
        {
            element.style.borderBottomWidth = width;
            element.style.borderLeftWidth = width;
            element.style.borderRightWidth = width;
            element.style.borderTopWidth = width;
        }

        public static void SetBorder(this VisualElement element, float width, float radius, Color color)
        {
            element.SetBorderWidth(width);
            element.SetBorderRadius(radius);
            element.SetBorderColor(color);
        }

        public static void SetPadding(this VisualElement element, float bottom, float left, float right, float top)
        {
            element.style.paddingBottom = bottom;
            element.style.paddingLeft = left;
            element.style.paddingRight = right;
            element.style.paddingTop = top;
        }

        public static void SetMargin(this VisualElement element, float bottom, float left, float right, float top)
        {
            element.style.marginBottom = bottom;
            element.style.marginLeft = left;
            element.style.marginRight = right;
            element.style.marginTop = top;
        }
    }

}
