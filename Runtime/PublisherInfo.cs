using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Challenges
{
    public class PublisherInfo : ScriptableObject
    {
        public delegate void Publish();

        public Publish onPublish;
        public string repository;

    }
}
