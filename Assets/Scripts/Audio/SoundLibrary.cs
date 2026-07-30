using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewSoundLibrary", menuName = "Bingo Game/Audio/Sound Library")]
public class SoundLibrary : ScriptableObject
{
    public List<Sound> sounds = new List<Sound>();
}