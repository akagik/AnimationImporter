using UnityEngine;

[System.Serializable]
public class CharacterMetaInfo
{
    public FrameInfo[] frames;

    [System.Serializable]
    public class FrameInfo
    {
        public PartInfo head;
    }

    [System.Serializable]
    public class PartInfo
    {
        public int index;
        public Vector2 pos;
    }
}
