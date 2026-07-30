using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewZoneMusicLibrary", menuName = "Bingo Game/Audio/Zone Music Library")]
public class ZoneMusicLibrary : ScriptableObject
{
    public AudioZone zone = AudioZone.None;

    public List<Sound> musicTracks = new List<Sound>();
}