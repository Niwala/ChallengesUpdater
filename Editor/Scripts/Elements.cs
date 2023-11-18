using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using UnityEditor;
using UnityEditor.UIElements;
using UnityEditor.SceneManagement;

using UnityEngine;
using UnityEngine.Video;
using UnityEngine.Networking;
using UnityEngine.UIElements;

using Cursor = UnityEngine.UIElements.Cursor;

namespace Challenges
{
    /// <summary>
    /// Allows you to quickly build visualElements for content pages.
    /// </summary>
    public static class Elements
    {
        public static VisualElement Build(this Page.Content content)
        {
            VisualElement element = Build(content, out Page.Container containerAction);

            switch (containerAction)
            {
                case Page.Container.Enter:
                    {
                        switch (content.type)
                        {
                            case Page.Type.BeginCallout:
                                {
                                    VisualElement callout = new VisualElement();
                                    callout.SetPadding(10, 10, 10, 10);
                                    callout.SetMargin(0, 0, 0, 10);
                                    callout.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 1.0f);
                                    callout.SetBorderRadius(10, 10, 0, 0);

                                    VisualElement horizontal = null;
                                    if (!string.IsNullOrEmpty(content.altText) || !string.IsNullOrEmpty(content.text))
                                    {
                                        horizontal = new VisualElement();
                                        horizontal.name = "Horizontal";
                                        horizontal.style.flexDirection = FlexDirection.Row;
                                        callout.Add(horizontal);
                                    }

                                    //Icon
                                    if (!string.IsNullOrEmpty(content.altText))
                                    {
                                        VisualElement icon = new VisualElement();
                                        icon.name = "Icon";
                                        icon.style.width = 32;
                                        icon.style.height = 32;
                                        icon.style.unityBackgroundImageTintColor = content.color;
                                        icon.SetMargin(0, -3, 5, -3);
                                        icon.SetOnlineImage(content.altText);
                                        callout.style.paddingLeft = 46;
                                        horizontal.style.marginLeft = -34;
                                        horizontal.Add(icon);
                                    }

                                    //Title
                                    if (!string.IsNullOrEmpty(content.text))
                                    {
                                        TextElement title = new TextElement();
                                        title.name = "Title";
                                        title.style.marginTop = 4;
                                        title.style.fontSize = 20;
                                        title.text = content.text;
                                        horizontal.Add(title);
                                    }

                                    return callout;
                                }

                            case Page.Type.BeginFoldout:
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
                        }
                    }
                case Page.Container.Exit:
                    {
                        switch (content.type)
                        {
                            case Page.Type.EndCallout:
                                {
                                    VisualElement callout = new VisualElement();
                                    callout.SetPadding(10, 10, 10, 10);
                                    callout.SetMargin(10, 0, 0, 0);
                                    callout.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 1.0f);
                                    callout.SetBorderRadius(0, 0, 10, 10);
                                    return callout;
                                }

                            case Page.Type.EndFoldout:
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
                        }
                    }
                default:
                    return element;
            }
        }

        public static VisualElement Build(this Page.Content content, out Page.Container containerAction)
        {
            containerAction = Page.Container.None;

            switch (content.type)
            {
                case Page.Type.Title:
                    {
                        TextElement title = new TextElement();
                        title.style.fontSize = 24;
                        title.style.marginTop = 12;
                        title.text = content.text;

                        return title;
                    }

                case Page.Type.Subtitle:
                    {
                        TextElement subtitle = new TextElement();
                        subtitle.style.fontSize = 18;
                        subtitle.style.marginTop = 6;
                        subtitle.text = content.text;

                        return subtitle;
                    }

                case Page.Type.Link:
                    {
                        TextElement textElement = new TextElement();
                        textElement.style.fontSize = 14;
                        textElement.style.marginTop = 3;
                        textElement.style.opacity = 0.9f;
                        textElement.SetCursor(MouseCursor.Link);

                        string[] values = content.text.Split('|');

                        if (!string.IsNullOrEmpty(content.altText))
                            values = new string[] { content.text, content.altText };

                        if (values.Length < 2)
                            textElement.text = content.text;
                        else
                            textElement.text = $"<a href={values[1]}>{values[0]}</a>";

                        return textElement;
                    }

                case Page.Type.Text:
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

                case Page.Type.Button:
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
                                    UnityEditor.PopupWindow.Show(button.worldBound, new ChallengeEditor.PopupVideo(clip));
                                    break;

                                case Texture2D tex2D:
                                    UnityEditor.PopupWindow.Show(button.worldBound, new ChallengeEditor.PopupImage(tex2D));
                                    break;

                                case SceneAsset scene:
                                    EditorSceneManager.OpenScene(AssetDatabase.GetAssetPath(scene), OpenSceneMode.Single);
                                    break;

                                case Challenge subChallenge:
                                    Challenges.Open(subChallenge);
                                    break;

                                default:
                                    Selection.activeObject = content.obj;
                                    break;
                            }
                        }


                        return button;
                    }

                case Page.Type.Space:
                    {
                        VisualElement space = new VisualElement();
                        space.style.height = content.padding;
                        return space;
                    }

                case Page.Type.Code:
                    {
                        Foldout fold = new Foldout();
                        fold.style.backgroundColor = new Color(0.17f, 0.17f, 0.17f, 1.0f);
                        fold.SetBorder(1, 10, new Color(0, 0, 0, 0.5f));
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
                        textElement.SetBorderRadius(0, 0, 10, 10);
                        textElement.style.borderTopColor = new Color(0, 0, 0, 0.4f);
                        textElement.style.borderTopWidth = 1.0f;
                        fold.Add(textElement);

                        Button copyBtn = new Button();
                        copyBtn.style.backgroundImage = (Texture2D)EditorGUIUtility.IconContent("d_UnityEditor.ConsoleWindow").image;
                        copyBtn.style.position = Position.Absolute;
                        copyBtn.style.top = 2;
                        copyBtn.style.right = -2;
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

                case Page.Type.Hint:
                    {
                        bool toggle = false;

                        VisualElement hint = new VisualElement();
                        hint.SetBorderRadius(10);
                        hint.SetBorder(0, 10, new Color(0, 0, 0, 0.5f));
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
                        hintContent.SetPadding(5, 5, 5, 5);

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
                                    hint.SetBorderWidth(1);
                                }
                                else
                                {
                                    hintMsg.style.visibility = Visibility.Visible;
                                    hintContent.style.visibility = Visibility.Hidden;
                                    hint.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 1.0f);
                                    hint.SetBorderWidth(0);
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

                case Page.Type.Shader:
                    {
                        Object loaded = AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GetAssetPath(content.obj));
                        ShaderContainer shaderContainer = new ShaderContainer(loaded as Material, content.text);
                        return shaderContainer;
                    }

                case Page.Type.Separator:
                    {
                        VisualElement BuildSeparator()
                        {
                            VisualElement element = new VisualElement();
                            element.style.height = 0;
                            element.style.borderTopWidth = 1;
                            element.style.borderBottomWidth = 1;
                            element.style.borderTopColor = new Color(0, 0, 0, 0.3f);
                            element.style.borderBottomColor = new Color(1, 1, 1, 0.2f);
                            element.style.marginTop = 3;
                            element.style.marginBottom = 6;
                            element.style.flexGrow = 1;
                            return element;
                        }

                        if (string.IsNullOrEmpty(content.text))
                        {
                            VisualElement separator = BuildSeparator();
                            separator.SetMargin(5, 0, 0, 15);
                            return separator;
                        }
                        else
                        {
                            VisualElement horizontal = new VisualElement();
                            horizontal.SetMargin(5, 0, 0, 15);
                            horizontal.style.flexDirection = FlexDirection.Row;

                            horizontal.Add(BuildSeparator());

                            TextElement label = new TextElement();
                            label.text = content.text;
                            label.style.fontSize = 13;
                            label.style.opacity = 0.7f;
                            label.SetMargin(0, 5, 5, -4);
                            horizontal.Add(label);

                            horizontal.Add(BuildSeparator());


                            return horizontal;
                        }
                    }

                case Page.Type.BeginCallout:
                    {
                        containerAction = Page.Container.Enter;

                        VisualElement callout = new VisualElement();
                        callout.SetPadding(10, 10, 10, 10);
                        callout.SetMargin(10, 0, 0, 10);
                        callout.SetBorderRadius(10);
                        callout.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 1.0f);
                        callout.name = "Container";

                        VisualElement horizontal = null;
                        if (!string.IsNullOrEmpty(content.altText) || !string.IsNullOrEmpty(content.text))
                        {
                            horizontal = new VisualElement();
                            horizontal.name = "Horizontal";
                            horizontal.style.flexDirection = FlexDirection.Row;
                            callout.Add(horizontal);
                        }

                        //Icon
                        if (!string.IsNullOrEmpty(content.altText))
                        {
                            VisualElement icon = new VisualElement();
                            icon.name = "Icon";
                            icon.style.width = 32;
                            icon.style.height = 32;
                            icon.style.unityBackgroundImageTintColor = content.color;
                            icon.SetMargin(0, -3, 5, -3);
                            icon.SetOnlineImage(content.altText);
                            callout.style.paddingLeft = 46;
                            horizontal.style.marginLeft = -34;
                            horizontal.Add(icon);
                        }

                        //Title
                        if (!string.IsNullOrEmpty(content.text))
                        {
                            TextElement title = new TextElement();
                            title.name = "Title";
                            title.style.marginTop = 4;
                            title.style.fontSize = 20;
                            title.text = content.text;
                            horizontal.Add(title);
                        }

                        return callout;
                    }

                case Page.Type.EndCallout:
                    {
                        containerAction = Page.Container.Exit;
                        return null;
                    }

                case Page.Type.BeginFoldout:
                    {
                        containerAction = Page.Container.Enter;

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

                case Page.Type.EndFoldout:
                    {
                        containerAction = Page.Container.Exit;
                        return null;
                    }

                case Page.Type.Icon:
                    {
                        VisualElement horizontal = new VisualElement();
                        horizontal.style.flexDirection = FlexDirection.Row;

                        //Icon
                        VisualElement icon = new VisualElement();
                        icon.style.width = 32;
                        icon.style.height = 32;
                        icon.style.opacity = 0.7f;
                        icon.style.unityBackgroundImageTintColor = content.color;
                        icon.SetMargin(0, 0, 5, 10);
                        icon.SetOnlineImage(content.altText);
                        horizontal.Add(icon);


                        //Title
                        if (!string.IsNullOrEmpty(content.text))
                        {
                            TextElement title = new TextElement();
                            title.style.fontSize = 24;
                            title.style.marginTop = 12;
                            title.text = content.text;
                            horizontal.Add(title);
                        }

                        return horizontal;
                    }

                case Page.Type.LinkToChallenge:
                    {
                        Updater.Status targetStatus = Updater.GetChallengeStatus(content.text);
                        Challenge target = targetStatus.pack;

                        //Main button
                        Button button = new Button();
                        button.style.height = 60;
                        button.style.maxWidth = 230;
                        button.SetBorderRadius(10);
                        button.SetCursor(MouseCursor.Link);
                        button.SetMargin(5, 0, 0, 5);
                        button.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1.0f);
                        if (target != null)
                            button.clicked += () => Challenges.Open(target);

                        //Background
                        VisualElement preview = new VisualElement();
                        preview.pickingMode = PickingMode.Ignore;
                        preview.StretchToParentSize();
                        preview.style.unityBackgroundImageTintColor = new Color(0.8f, 0.8f, 0.8f, 1.0f);
                        preview.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(BackgroundSizeType.Cover));
                        preview.style.backgroundPositionX = new StyleBackgroundPosition(new BackgroundPosition(BackgroundPositionKeyword.Right));
                        if (target != null && target.preview != null)
                            preview.style.backgroundImage = target.preview;
                        button.Add(preview);

                        //Title
                        TextElement title = new TextElement();
                        title.pickingMode = PickingMode.Ignore;
                        title.style.position = Position.Absolute;
                        title.style.top = 3;
                        title.style.left = 7;
                        title.style.fontSize = 20;
                        title.text = string.IsNullOrEmpty(content.altText) ? content.text : content.altText;
                        button.Add(title);

                        //Description
                        TextElement description = new TextElement();
                        description.pickingMode = PickingMode.Ignore;
                        description.style.position = Position.Absolute;
                        description.style.left = 7;
                        description.style.top = 28;
                        description.text = target != null ? "Clic to open the Challenge" : "Unable to find the challenge";
                        button.Add(description);


                        return button;
                    }

            }

            return null;
        }

        public static DataFlags GetFlags(this Page.Type contentType)
        {
            switch (contentType)
            {
                case Page.Type.Title: 
                case Page.Type.Subtitle:
                case Page.Type.Hint:
                case Page.Type.Separator:
                    return DataFlags.text;


                case Page.Type.Text:
                case Page.Type.Button:
                case Page.Type.BeginFoldout:
                case Page.Type.Video:
                    return DataFlags.text | DataFlags.obj;


                case Page.Type.Link:
                case Page.Type.Code:
                case Page.Type.LinkToChallenge:
                    return DataFlags.text | DataFlags.altText;

                case Page.Type.Space:
                    return DataFlags.padding;


                case Page.Type.Shader:
                case Page.Type.Image:
                    return DataFlags.obj;

                case Page.Type.Icon:
                    return DataFlags.text | DataFlags.color;

                case Page.Type.BeginCallout:
                    return DataFlags.text | DataFlags.altText | DataFlags.color;


                default:
                    return DataFlags.none;

            }
        }
        
        public enum DataFlags
        {
            none = 0,
            text = 1 << 0,
            altText = 1 << 1,
            obj = 1 << 2,
            padding = 1 << 3,
            color = 1 << 4
        }
    }


    /// <summary>
    /// Adds utility functions to VisualElements.
    /// </summary>
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

        public static void SetVisibility(this VisualElement element, bool value)
        {
            element.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public static void SetCursor(this VisualElement element, MouseCursor cursor)
        {
            object objCursor = new Cursor();
            PropertyInfo fields = typeof(Cursor).GetProperty("defaultCursorId", BindingFlags.NonPublic | BindingFlags.Instance);
            fields.SetValue(objCursor, (int)cursor);
            element.style.cursor = new StyleCursor((Cursor)objCursor);
        }

        public static void SetOnlineImage(this VisualElement element, string url)
        {
            if (string.IsNullOrEmpty(url))
                return;
            Updater.DownloadFile(url, (DownloadHandler dh) => GetOnlineIcon(element, dh), true);
        }

        private static void GetOnlineIcon(this VisualElement element, DownloadHandler dh)
        {
            Texture2D tex = new Texture2D(32, 32, TextureFormat.PVRTC_RGBA4, false);
            tex.LoadImage(dh.data);
            element.style.backgroundImage = tex;
        }

        public static void AddSeparator(this VisualElement parent)
        {
            parent.Add(new Page.Content() { type = Page.Type.Separator }.Build());
        }

        public static void AddSeparator(this VisualElement parent, string title)
        {
            parent.Add(new Page.Content() { type = Page.Type.Separator, text = title }.Build());
        }
    }


    /// <summary>
    /// Simplify the display of a challenge element and its properties in the inspector.
    /// </summary>
    public class ContentContainer : ScriptableObject
    {
        public Challenge challenge;
        public Page page;
        public int contentID;

        public static void Open(Page page, int elementID)
        {
            ContentContainer container = ScriptableObject.CreateInstance<ContentContainer>();
            container.page = page;
            container.contentID = elementID;
            Selection.activeObject = container;
        }

        [CustomEditor(typeof(ContentContainer))]
        public class Editor : UnityEditor.Editor
        {
            private ContentContainer container;
            private PropertyField typeField;
            private PropertyField textField;
            private PropertyField altTextField;
            private PropertyField paddingField;
            private PropertyField objectField;
            private PropertyField colorField;
            private VisualElement preview;

            public override VisualElement CreateInspectorGUI()
            {
                //Get components
                container = target as ContentContainer;
                SerializedProperty page = serializedObject.FindProperty(nameof(container.page));
                SerializedProperty content = page.FindPropertyRelative(nameof(container.page.content)).GetArrayElementAtIndex(container.contentID);
                VisualElement root = new VisualElement();

                //Add inspector fields
                typeField = AddField("type");
                typeField.RegisterValueChangeCallback(OnTypeChange);
                textField = AddField("text");
                altTextField = AddField("altText");
                objectField = AddField("obj");
                paddingField = AddField("padding");
                colorField = AddField("color");
                OnTypeChange(null);
                root.AddSeparator();

                PropertyField AddField(string propertyName)
                {
                    PropertyField field = new PropertyField(content.FindPropertyRelative(propertyName));
                    field.RegisterValueChangeCallback(OnChange);
                    root.Add(field);
                    return field;
                }

                //Add preview
                preview = new VisualElement();
                root.Add(preview);

                return root;
            }

            private void OnChange(SerializedPropertyChangeEvent e)
            {
                if (container.page == null)
                    return;

                //Update challenges window view
                Challenges.onEditElement?.Invoke(container.page, container.contentID);

                //Update preview
                preview.Clear();
                preview.Add(container.page.content[container.contentID].Build());
            }

            private void OnTypeChange(SerializedPropertyChangeEvent e)
            {
                Elements.DataFlags flags = container.page.content[container.contentID].type.GetFlags();

                textField.SetVisibility((flags & Elements.DataFlags.text) == Elements.DataFlags.text);
                altTextField.SetVisibility((flags & Elements.DataFlags.altText) == Elements.DataFlags.altText);
                objectField.SetVisibility((flags & Elements.DataFlags.obj) == Elements.DataFlags.obj);
                paddingField.SetVisibility((flags & Elements.DataFlags.padding) == Elements.DataFlags.padding);
                colorField.SetVisibility((flags & Elements.DataFlags.color) == Elements.DataFlags.color);
            }

        }
    }
}
