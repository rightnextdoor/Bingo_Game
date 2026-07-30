using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ZoneSceneMapping", menuName = "Bingo Game/Audio/Zone Scene Mapping")]
public class ZoneSceneMapping : ScriptableObject
{
    [System.Serializable]
    public class ZoneDefinition
    {
        public AudioZone zone = AudioZone.None;

        public List<string> sceneNames = new List<string>();
    }

    public List<ZoneDefinition> zones = new List<ZoneDefinition>();
}