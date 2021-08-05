using UnityEngine;

namespace AnimationImporter
{
    [System.Serializable]
    public class CharacterMetaInfo
    {
        public FrameInfo[] frames;

        [System.Serializable]
        public class FrameInfo
        {
            public CharaPartInfo head;
        }
    }
}