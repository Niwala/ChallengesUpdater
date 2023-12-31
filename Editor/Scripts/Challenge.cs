using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Challenges
{
    public class Challenge : ScriptableObject
    {
        public Challenge parent;
        public List<Page> pages = new List<Page>();

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
        public string hash;

        public void UpdateHash()
        {
            if (preview == null)
            {
                throw new System.Exception($"Missing preview on {name} challenge.");
            }
            Hash128 h = preview.imageContentsHash;
            h.Append(pages.Count);
            for (int i = 0; i < pages.Count; i++)
                h.Append(pages[i].GetHash().ToString());
            h.Append(description);
            h.Append(priority);
            h.Append(hidden.ToString());
            h.Append(teacher);
            h.Append(tags);
            h.Append(scene.GetHashCode());
            h.Append(onOpen);
            hash = h.ToString();
        }
    }

    [System.Serializable]
    public class Page
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
            public string altText;
            public int padding;
            public Object obj;
            public Color color;

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
            Separator,
            Image,
            Video,
            LinkToChallenge,
            BeginCallout,
            EndCallout,
            BeginFoldout,
            EndFoldout,
            Icon,
        }

        public enum Container
        {
            None,
            Enter,
            Exit
        }

        public Page Copy()
        {
            Page copy = new Page();
            for (int i = 0; i < content.Count; i++)
                copy.content.Add(content[i]);
            return copy;
        }
    }
}