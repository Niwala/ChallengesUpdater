using UnityEditor;

namespace Challenges
{
    public static class Preferences
    {
        //Strings
        public static StringPreference teacher = new StringPreference("teacher", "");
        public static StringPreference lastUpdate = new StringPreference("lastUpdate", "");

        //Bools
        public static BoolPreference alreadyOpened = new BoolPreference("alreadyOpen", false);
        public static BoolPreference devMode = new BoolPreference("devMode", false);
        public static BoolPreference openSceneOnSelect = new BoolPreference("openSceneOnSelect", true);
        public static BoolPreference checkUpdateEveryday = new BoolPreference("checkUpdateEveryday", true);

        #region Preferences sub utility classes
        public class BoolPreference : BasePreference<bool>
        {
            public BoolPreference(string name, bool defaultValue) : base(name, defaultValue) { }

            protected override bool GetValue()
            {
                return EditorPrefs.GetBool(name, defaultValue);
            }

            protected override void SetValue(bool value)
            {
                EditorPrefs.SetBool(name, value);
            }

            public void Switch()
            {
                value = !value;
            }
        }

        public class IntPreference : BasePreference<int>
        {
            public IntPreference(string name, int defaultValue) : base(name, defaultValue) { }

            protected override int GetValue()
            {
                return EditorPrefs.GetInt(name, defaultValue);
            }

            protected override void SetValue(int value)
            {
                EditorPrefs.SetInt(name, value);
            }
        }

        public class FloatPreference : BasePreference<float>
        {
            public FloatPreference(string name, float defaultValue) : base(name, defaultValue) { }

            protected override float GetValue()
            {
                return EditorPrefs.GetFloat(name, defaultValue);
            }

            protected override void SetValue(float value)
            {
                EditorPrefs.SetFloat(name, value);
            }
        }

        public class StringPreference : BasePreference<string>
        {
            public StringPreference(string name, string defaultValue) : base(name, defaultValue) { }

            protected override string GetValue()
            {
                return EditorPrefs.GetString(name, defaultValue);
            }

            protected override void SetValue(string value)
            {
                EditorPrefs.SetString(name, value);
            }
        }

        public abstract class BasePreference<T>
        {
            protected string name;
            protected T defaultValue;
            protected const string prefix = "Challenges.";

            protected BasePreference(string name, T defaultValue)
            {
                this.name = prefix + name;
                this.defaultValue = defaultValue;
            }

            public T value
            {
                get { return GetValue(); }
                set { SetValue(value); }
            }

            protected abstract T GetValue();

            protected abstract void SetValue(T value);

            public static implicit operator T (BasePreference<T> source)
            {
                return source.value;
            }
        }
        #endregion
    }
}