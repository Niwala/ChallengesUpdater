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
using UnityEngine.Networking;

namespace Challenges
{
    public class Challenges : EditorWindow
    {
        //Static stuff
        [MenuItem("Challenges/Challenges &U")]
        public static void Open()
        {
            Challenges window = GetWindow<Challenges>();
            window.titleContent = new GUIContent("Challenges");
            window.Show();
        }

        [MenuItem("Edit/Switch Challenges to Edit Mode %e", priority = 150)]
        public static void SwitchToEditMode()
        {
            Challenges challenges = focusedWindow as Challenges;
            if (challenges == null)
                return;

            Preferences.devMode.Switch();
            challenges.Refresh();
        }

        [MenuItem("Edit/Switch Challenges to Edit Mode %e", validate = true)]
        public static bool SwitchToEditModeValidate()
        {
            return focusedWindow is Challenges;
        }

        [InitializeOnLoadMethod]
        public static void FirstLoading()
        {
            if (Preferences.alreadyOpened)
                return;

            Challenge startScreenChallenge = Updater.LoadData<Challenge>("Start Screen\\Start Screen.asset");
            Preferences.alreadyOpened.value = true;

            if (startScreenChallenge != null)
                Open(startScreenChallenge);
        }

        public static void Open(Challenge challenge)
        {
            Challenges window = GetWindow<Challenges>();
            window.titleContent = new GUIContent("Challenges");
            window.Show();

            EditorApplication.delayCall += () => window.OpenChallenge(challenge);
        }

        public static OnEditElement onEditElement;

        public delegate void OnEditElement(Page page, int contentID);

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
            Updater.LoadResources();
            Updater.LoadIcons();
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

            //Bind callbacks and start the process
            BindCallbacks();
            Updater.LoadCache();


            //Check updates of the day
            if (EditorPrefs.GetBool("Challenges.CheckUpdateEveryday", true))
            {
                string lastUpdate = EditorPrefs.GetString("Challenges.LastUpdate", "");
                string currentDay = DateTime.Now.Date.ToString();

                if (lastUpdate != currentDay)
                {
                    EditorPrefs.SetString("Challenges.LastUpdate", currentDay);
                    onCheckUpdaterVersionStart();
                    Updater.ChallengesUpdaterIsOutdated(OnCheckUpdaterVersionEnd);
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
            Updater.onStartLoading += updateToolbar.Start;
            Updater.onStopLoading += updateToolbar.Stop;
            Updater.onCacheUpdated += OnCacheUpdated;
            Updater.onCacheLoaded += OnCacheLoaded;
            Undo.undoRedoPerformed += Refresh;
        }

        public void Refresh()
        {
            if (challengePage.style.display == DisplayStyle.Flex)
                challengePage.RefreshPage();
            if (selectionPage.style.display == DisplayStyle.Flex)
                selectionPage.Refresh();
            if (preferencePage.style.display == DisplayStyle.Flex)
                preferencePage.Refresh();
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

        public void OpenChallenge(Challenge challenge)
        {
            //Open scene
            if (Preferences.openSceneOnSelect)
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
                notifications.AddNotification(new Updater.NotificationInfo(
                    "Challenges Package",
                    "Des mises à jours sont disponibles pour le package Challenges.",
                    "Mettre à jour",
                    Updater.UpdateTheUpdater));
            }
            else
            {
                Updater.UpdateCache();
            }
        }

        public void OnCacheUpdated()
        {
            updateToolbar.Stop();

            Updater.LoadCache();
        }

        public void OnCacheLoaded()
        {
            selectionPage.Refresh();
            updateToolbar.Stop();
            OpenPage(selectionPage);
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
            foreach (var status in Updater.filteredChallengeList)
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
            Updater.previewMaterial.SetVector(name, new Vector4(r.xMin, r.yMin, r.xMax, r.yMax));
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
                HashSet<string> teachers = Updater.GetTeachers();
                teacherMenu.menu.AppendAction("All", OnSelectTeacher, GetTeacherStatus, "");
                teacherMenu.menu.AppendSeparator();
                foreach (var teacher in teachers)
                {
                    teacherMenu.menu.AppendAction(teacher, OnSelectTeacher, GetTeacherStatus, teacher);
                }
                Add(teacherMenu);
                string t = string.IsNullOrEmpty(Updater.teacher) ? "" : Updater.teacher;
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
                contextMenu.menu.AppendAction("Show Start Screen", ShowStartScreen, MenuStatus.Normal);
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
                EditorPrefs.SetString(Updater.lastTeacherPrefKey, (string)e.userData);

                //Apply filters on teacher
                Updater.teacher = (string)e.userData;
                Updater.UpdateFilteredStatus();
                window.RefreshSelectionList();
            }

            private MenuStatus GetTeacherStatus(DropdownMenuAction e)
            {
                string teacher = (string)e.userData;

                if (teacher == Updater.teacher)
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
                Updater.ChallengesUpdaterIsOutdated(window.OnCheckUpdaterVersionEnd);
            }

            private void ShowNotifications(DropdownMenuAction e)
            {
                window.ShowNotifications(true);
            }

            private void ShowStartScreen(DropdownMenuAction e)
            {
                Challenge startScreenChallenge = Updater.LoadData<Challenge>("Start Screen\\Start Screen.asset");
                if (startScreenChallenge != null)
                    window.OpenChallenge(startScreenChallenge);
            }

            private void GetAmplify(DropdownMenuAction e)
            {
                Updater.DownloadAmplify();
            }

            private void Documentation(DropdownMenuAction e)
            {
                Updater.OpenDocumentation();
            }

            private void OnSearchChange(ChangeEvent<string> e)
            {
                if (e.previousValue == e.newValue)
                    return;

                Updater.search = e.newValue;
                Updater.UpdateFilteredStatus();
                window.RefreshSelectionList();
            }
        }

        public class ChallengeTile : VisualElement
        {
            private Challenges window;
            private VisualElement cover;
            private Updater.Status status;
            private ChallengeBackground background;
            private VisualElement statusIcon;

            public ChallengeTile(Challenges window, Updater.Status status)
            {
                this.window = window;
                this.status = status;

                //Register events
                RegisterCallback<AttachToPanelEvent>(OnAttachPanel);
                RegisterCallback<DetachFromPanelEvent>(OnDetachPanel);
                RegisterCallback<MouseEnterEvent>(MouseEnter);
                RegisterCallback<MouseLeaveEvent>(MouseLeave);
                RegisterCallback<MouseDownEvent>(MouseDown);

                //Size
                style.minWidth = 300;
                style.maxWidth = 500;
                style.height = 140;
                style.flexShrink = 0;
                style.flexGrow = 1;
                this.SetCursor(MouseCursor.Link);

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

            private void OnAttachPanel(AttachToPanelEvent e)
            {
                Updater.onChallengeChanged += OnChallengeChanged;
            }

            private void OnDetachPanel(DetachFromPanelEvent e)
            {
                Updater.onChallengeChanged -= OnChallengeChanged;
            }

            private void OnChallengeChanged(Updater.Status newStatus)
            {
                if (status.name == newStatus.name) 
                {
                    background.loading = newStatus.status == Updater.ChallengeStatus.Loading;
                }
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
                    if (status.challenge != null)
                        window.OpenChallenge(status.challenge);
                    else
                        Updater.DownloadChallenge(status.name);
                }
                else if (e.button == 1)
                {
                    GenericMenu menu = new GenericMenu();

                    if (status.challenge != null)
                    {
                        menu.AddItem(new GUIContent("Open"), false, () => window.OpenChallenge(status.challenge));
                        menu.AddItem(new GUIContent("Reset"), false, () => Updater.DownloadChallenge(status.name));
                        menu.AddItem(new GUIContent("Remove"), false, () => Updater.DeleteChallenge(status.name));
                    }
                    else
                    {
                        menu.AddItem(new GUIContent("Download"), false, () => Updater.DownloadChallenge(status.name));
                    }

                    menu.ShowAsContext();
                }
                else if (e.button == 2)
                {
                    Selection.activeObject = status.challenge;
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
            public bool loading;

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
                if (image == null)
                    return;

                Rect rect = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                float width = (image.width / (float)image.height) * rect.height;
                rect = Rect.MinMaxRect(rect.xMax - width, rect.yMin, rect.xMax, rect.yMax);

                Rect worldRect = worldBound;
                worldRect = Rect.MinMaxRect(worldRect.xMax - width, worldRect.yMin, worldRect.xMax, worldRect.yMax);

                Rect worldClip = worldRect;
                if (viewPort != null)
                    worldClip = viewPort.worldBound;

                if (Updater.previewMaterial == null)
                    Updater.LoadResources();

                Updater.previewMaterial.SetVector("_TexSize", new Vector4(rect.width, rect.height, 1.0f / rect.width, 1.0f / rect.height));
                Updater.previewMaterial.SetTexture("_Mask", Updater.previewMask);
                Updater.previewMaterial.SetInt("_ColorSpace", (int)PlayerSettings.colorSpace);
                Updater.previewMaterial.SetFloat("_EditorTime", (float)(EditorApplication.timeSinceStartup % 10000));
                Updater.previewMaterial.SetFloat("_Loading", loading ? 1.0f : 0.0f);
                SetRectToPreviewMaterial("_WorldRect", worldRect);
                SetRectToPreviewMaterial("_WorldClip", worldClip);
                EditorGUI.DrawPreviewTexture(rect, image, Updater.previewMaterial);
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

        private Challenge challenge;
        private Page page;
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

        private void OnEditElement(Page page, int contentID)
        {
            if (page == this.page)
                list.RefreshItem(contentID);
        }

        public void OpenChallenge(Challenge challenge)
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

            if (Preferences.devMode)
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
                    VisualElement element = page.content[i].Build(out Page.Container containerAction);

                    if (containerAction == Page.Container.Exit)
                        container = null;

                    if (element == null)
                        continue;

                    if (container == null || containerAction == Page.Container.Enter)
                        content.Add(element);
                    else
                        container.Add(element);

                    if (containerAction == Page.Container.Enter)
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
            Challenge parent = challenge.parent;
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
                    string[] guids = AssetDatabase.FindAssets($"t:{typeof(Challenge)}");
                    for (int i = 0; i < guids.Length; i++)
                    {
                        Challenge sibling = AssetDatabase.LoadAssetAtPath<Challenge>(AssetDatabase.GUIDToAssetPath(guids[i]));
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
        private VisualElement devContent;
        private Toggle developerMode;
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
            LinkToggleToPref(openSceneOnSelection, Preferences.openSceneOnSelect);
            properties.Add(openSceneOnSelection);

            Toggle everydayUpdates = new Toggle("Check for updates every day");
            SetToggleStyle(everydayUpdates);
            SetCheckEverydayUpdateToggle(everydayUpdates);
            properties.Add(everydayUpdates);

            developerMode = new Toggle("Developer mode");
            SetToggleStyle(developerMode);
            LinkToggleToPref(developerMode, Preferences.devMode);
            properties.Add(developerMode);

            devContent = new VisualElement();
            devContent.SetVisibility(Preferences.devMode);
            properties.Add(devContent);

            devContent.AddSeparator("Developer Settings");


            //New challenge button
            Button newChallenge = new Button();
            newChallenge.text = "Create a new Challenge";
            newChallenge.clicked += Updater.CreateNewChallenge;
            devContent.Add(newChallenge);


            //Push Updater button
            Button pushUpdaterMinor = new Button();
            pushUpdaterMinor.text = "Push a new version of the updater (Minor)";
            pushUpdaterMinor.clicked += () => Updater.PushNewUpdaterVersion(false);
            devContent.Add(pushUpdaterMinor);

            //Push Updater button
            Button pushUpdaterMajor = new Button();
            pushUpdaterMajor.text = "Push a new version of the updater (Major)";
            pushUpdaterMajor.clicked += () => Updater.PushNewUpdaterVersion(true);
            devContent.Add(pushUpdaterMajor);


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

        private void LinkToggleToPref(Toggle toggle, Preferences.BoolPreference boolPref)
        {
            toggle.SetValueWithoutNotify(boolPref);
            toggle.RegisterValueChangedCallback(OnChange);

            void OnChange(ChangeEvent<bool> e)
            {
                if (e.previousValue == e.newValue)
                    return;

                boolPref.value = e.newValue;
            }
        }

        private void SetCheckEverydayUpdateToggle(Toggle toggle)
        {
            toggle.SetValueWithoutNotify(Preferences.checkUpdateEveryday);
            toggle.RegisterValueChangedCallback(OnChange);

            void OnChange(ChangeEvent<bool> e)
            {
                if (e.previousValue == e.newValue)
                    return;

                Preferences.checkUpdateEveryday.value = e.newValue;
                Preferences.lastUpdate.value = "";
            }
        }

        public void Refresh()
        {
            bool inDevMode = Preferences.devMode;
            devContent.SetVisibility(inDevMode);
            developerMode.SetValueWithoutNotify(inDevMode);
        }
    }

    public class ChallengeNotifications : VisualElement
    {
        private Challenges window;
        private List<Updater.NotificationInfo> notificationInfos = new List<Updater.NotificationInfo>();
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

            Updater.GetNotifications(notificationInfos, includeDismiss);

            //Add notifications to scrollView
            foreach (var info in notificationInfos)
                AddNotification(info);

            RefreshVisibility();
        }

        public void AddNotification(Updater.NotificationInfo info)
        {
            Notification notification = new Notification(this, info);
            notificationElements.Add(notification);
            scroll.Add(notification);
            RefreshVisibility();
        }

        public void Close(Notification notification)
        {
            Updater.DismissNotification(notification.info);
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
            public Updater.NotificationInfo info;

            public Notification(ChallengeNotifications container, Updater.NotificationInfo info)
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
        private Challenge challenge;
        private Page page;
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

        public void Bind(Challenge challenge, int pageID, int contentID)
        {
            this.challenge = challenge;
            this.page = challenge.pages[pageID];
            this.pageID = pageID;
            this.contentID = contentID;

            parent.style.paddingBottom = parent.style.paddingTop = 0;

            Clear();
            VisualElement content = page.content[contentID].Build();
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
                    ContentContainer.Open(page, contentID);
                    break;

                case 1:
                    OpenContextualMenu();
                    break;
            }

        }

        void OpenContextualMenu()
        {
            GenericMenu menu = new GenericMenu();

            string[] names = Enum.GetNames(typeof(Page.Type));
            for (int i = 0; i < names.Length; i++)
            {
                int j = i;
                menu.AddItem(new GUIContent($"Insert/{ObjectNames.NicifyVariableName(names[i])}"), false, () => InsertNew((Page.Type)j));
            }

            menu.AddItem(new GUIContent("Duplicate"), false, Duplicate);
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Delete"), false, Delete);
            menu.AddItem(new GUIContent("Delete Page"), false, DeletePage);

            menu.ShowAsContext();
        }

        private void InsertNew(Page.Type type)
        {
            SerializedObject obj = new SerializedObject(challenge);
            obj.UpdateIfRequiredOrScript();
            page.content.RemoveAt(contentID);

            obj.FindProperty(nameof(challenge.pages))
                .GetArrayElementAtIndex(pageID)
                .FindPropertyRelative(nameof(page.content))
                .InsertArrayElementAtIndex(contentID);

            obj.FindProperty(nameof(challenge.pages))
                .GetArrayElementAtIndex(pageID)
                .FindPropertyRelative(nameof(page.content))
                .GetArrayElementAtIndex(contentID)
                .FindPropertyRelative("type").enumValueIndex = (int)type;

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

            if (Selection.activeObject is ContentContainer container)
            {
                ContentContainer.Open(challenge.pages[pageID], contentID + 1);
            }

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

        public StatusIcon(Updater.ChallengeStatus status)
        {
            style.backgroundImage = Updater.GetStatusIcon(status);
            style.opacity = normalOpacity;
            tooltip = Updater.GetStatusTooltip(status);

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


}
