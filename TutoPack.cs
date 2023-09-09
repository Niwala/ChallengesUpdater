using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Challenges
{
    [CreateAssetMenu(menuName = "Tuto pack", order = 51), System.Serializable]
    public class TutoPack : ScriptableObject
    {
        public List<TutoPage> pages = new List<TutoPage>();

        [Multiline(6)]
        public string description;
        public Texture2D preview;
        public int majorVersion;
        public int minorVersion;
        public float priority;
        public bool hidden;
        public string teacher;
        [Multiline(4)]
        public string tags;
        public Object scene;
        public string onOpen;
    }

    [System.Serializable]
    public class TutoPage
    {
        public List<Content> content = new List<Content>();

        [System.Serializable]
        public struct Content
        {
            public Type type;
            [Multiline] public string text;
            public int padding;
            public Object obj;
        }

        public enum Type
        {
            Title,
            Subtitle,
            Link,
            Text,
            Button,
            Space,
            Code,
            Hint,
            Shader,
            Separator
        }

        public TutoPage Copy()
        {
            TutoPage copy = new TutoPage();
            for (int i = 0; i < content.Count; i++)
                copy.content.Add(content[i]);
            return copy;
        }
    }
}