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
        public float priority;
        public bool hidden;
        public string teacher;
        [Multiline(4)]
        public string tags;
        public Object scene;
        public string onOpen;
        public Hash128 hash;

        public void UpdateHash()
        {
            hash = preview.imageContentsHash;
            hash.Append(pages.Count);
            for (int i = 0; i < pages.Count; i++)
                hash.Append(pages[i].GetHash().ToString());
            hash.Append(description);
            hash.Append(priority);
            hash.Append(hidden.ToString());
            hash.Append(teacher);
            hash.Append(tags);
            hash.Append(scene.GetHashCode());
            hash.Append(onOpen);
        }
    }

    [System.Serializable]
    public class TutoPage
    {
        public List<Content> content = new List<Content>();

        public Hash128 GetHash()
        {
            Hash128 hash = new Hash128();
            hash.Append(content.Count);
            for (int i = 0; i < content.Count; i++)
                hash.Append(content[i].GetHash().ToString());
            return hash;
        }

        [System.Serializable]
        public struct Content
        {
            public Type type;
            [Multiline] public string text;
            public int padding;
            public Object obj;

            public Hash128 GetHash()
            {
                Hash128 hash = new Hash128();
                hash.Append((int)type);
                hash.Append(text);
                hash.Append(padding);
                hash.Append(obj?.GetInstanceID() ?? 0);
                return hash;
            }
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