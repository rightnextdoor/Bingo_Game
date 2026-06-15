using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AudioManager : MonoBehaviour
{
    #region Singleton / References

    public static AudioManager instance;

    [Header("Fade Durations")]
    [SerializeField] private float fadeOutDuration = 1f;
    [SerializeField] private float fadeInDuration = 1f;

    [Header("SFX Libraries")]
    [SerializeField] private List<SoundLibrary> soundGroups = new List<SoundLibrary>();

    [Header("Zone Music Libraries")]
    [SerializeField] private List<ZoneMusicLibrary> zoneMusicDefinitions = new List<ZoneMusicLibrary>();

    [Header("Zone Scene Mapping")]
    [SerializeField] private List<ZoneSceneMapping> zoneSceneMapping = new List<ZoneSceneMapping>();

    private Sound currentMusic;
    private AudioZone currentZone = AudioZone.None;

    private Coroutine musicFadeCoroutine;
    private Coroutine musicSequenceCoroutine;

    private readonly Dictionary<AudioZone, List<Sound>> remainingZoneMusicTracks = new Dictionary<AudioZone, List<Sound>>();

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        TryParentToGameManager();

        DontDestroyOnLoad(gameObject);

        SetupSoundSources();
        SetupZoneMusicSources();

        PrepareZoneMusicForCurrentScene();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        PrepareZoneMusicForScene(scene.name);
    }

    #endregion

    #region Setup

    private void TryParentToGameManager()
    {
        GameObject gameManagerObject = GameObject.Find("GameManager");

        if (gameManagerObject == null)
            return;

        if (gameManagerObject.transform == transform)
            return;

        if (transform.parent != null)
            return;

        transform.SetParent(gameManagerObject.transform);
    }

    private void SetupSoundSources()
    {
        foreach (SoundLibrary soundLibrary in soundGroups)
        {
            if (soundLibrary == null || soundLibrary.sounds == null)
                continue;

            foreach (Sound sound in soundLibrary.sounds)
            {
                if (sound == null)
                    continue;

                CreateSourceForSound(sound, $"SFX_{sound.name}");
            }
        }
    }

    private void SetupZoneMusicSources()
    {
        foreach (ZoneMusicLibrary musicLibrary in zoneMusicDefinitions)
        {
            if (musicLibrary == null || musicLibrary.musicTracks == null)
                continue;

            foreach (Sound musicTrack in musicLibrary.musicTracks)
            {
                if (musicTrack == null)
                    continue;

                CreateSourceForSound(musicTrack, $"Music_{musicLibrary.zone}_{musicTrack.name}");
            }
        }
    }

    #endregion

    #region Scene / Zone Detection

    private void PrepareZoneMusicForCurrentScene()
    {
        string activeSceneName = SceneManager.GetActiveScene().name;
        PrepareZoneMusicForScene(activeSceneName);
    }

    private void PrepareZoneMusicForScene(string sceneName)
    {
        AudioZone sceneZone = GetZoneForScene(sceneName);

        if (sceneZone == AudioZone.None)
            return;

        if (IsAnyZoneMusicPlaying())
            return;

        PlayZoneMusicImmediate(sceneZone);
    }

    #endregion

    #region Zone Music

    private void PlayZoneMusicImmediate(AudioZone zone)
    {
        Sound nextMusic = GetNextRandomMusicForZone(zone);

        if (nextMusic == null)
            return;

        StopMusicCoroutines();
        StopAllZoneMusicSources();

        ConfigureSourceFromSound(nextMusic);

        nextMusic.source.volume = nextMusic.volume;
        nextMusic.source.Play();

        currentMusic = nextMusic;
        currentZone = zone;

        StartMusicSequenceTimer(nextMusic, zone);
    }

    private void PlayZoneMusicWithFade(AudioZone zone)
    {
        Sound nextMusic = GetNextRandomMusicForZone(zone);

        if (nextMusic == null)
            return;

        PlayMusicTrackWithFade(nextMusic, zone);
    }

    private void PlayMusicTrackWithFade(Sound nextMusic, AudioZone zone)
    {
        if (nextMusic == null)
            return;

        if (nextMusic == currentMusic)
        {
            PlayMusicTrackImmediate(nextMusic, zone);
            return;
        }

        if (musicFadeCoroutine != null)
            StopCoroutine(musicFadeCoroutine);

        if (musicSequenceCoroutine != null)
        {
            StopCoroutine(musicSequenceCoroutine);
            musicSequenceCoroutine = null;
        }

        musicFadeCoroutine = StartCoroutine(CrossfadeMusicCoroutine(nextMusic, zone));
    }

    private void PlayMusicTrackImmediate(Sound music, AudioZone zone)
    {
        if (music == null)
            return;

        StopMusicCoroutines();
        StopAllZoneMusicSources();

        ConfigureSourceFromSound(music);

        music.source.volume = music.volume;
        music.source.Play();

        currentMusic = music;
        currentZone = zone;

        StartMusicSequenceTimer(music, zone);
    }

    private void PlayNextRandomTrackInCurrentZone()
    {
        if (currentZone == AudioZone.None)
            return;

        Sound nextMusic = GetNextRandomMusicForZone(currentZone);

        if (nextMusic == null)
            return;

        PlayMusicTrackWithFade(nextMusic, currentZone);
    }

    #endregion

    #region Loading Transition Music

    public void ApplyZoneMusicForCurrentScene()
    {
        string activeSceneName = SceneManager.GetActiveScene().name;
        AudioZone sceneZone = GetZoneForScene(activeSceneName);

        if (sceneZone == AudioZone.None)
            return;

        if (!IsAnyZoneMusicPlaying())
        {
            PlayZoneMusicImmediate(sceneZone);
            return;
        }

        if (sceneZone == currentZone)
            return;

        PlayZoneMusicWithFade(sceneZone);
    }

    #endregion

    #region SFX

    public void PlaySFX(string soundName)
    {
        Sound sound = FindSFX(soundName);

        if (sound == null)
        {
            Debug.LogWarning($"SFX not found: {soundName}");
            return;
        }

        if (sound.clip == null)
        {
            Debug.LogWarning($"SFX '{soundName}' has no AudioClip assigned.");
            return;
        }

        if (sound.source == null)
            CreateSourceForSound(sound, $"SFX_{sound.name}");

        ConfigureSourceFromSound(sound);

        sound.source.PlayOneShot(sound.clip, sound.volume);
    }

    public void PlaySFXWithDelay(string soundName, float delay)
    {
        StartCoroutine(PlaySFXDelayedCoroutine(soundName, delay));
    }

    #endregion

    #region Lookup Helpers

    private AudioZone GetZoneForScene(string sceneName)
    {
        if (zoneSceneMapping == null || zoneSceneMapping.Count == 0)
        {
            Debug.LogWarning("No ZoneSceneMapping assigned to AudioManager.");
            return AudioZone.None;
        }

        foreach (ZoneSceneMapping sceneMapping in zoneSceneMapping)
        {
            if (sceneMapping == null)
                continue;

            if (sceneMapping.zones == null)
                continue;

            foreach (ZoneSceneMapping.ZoneDefinition zoneDefinition in sceneMapping.zones)
            {
                if (zoneDefinition == null)
                    continue;

                if (zoneDefinition.sceneNames == null)
                    continue;

                if (zoneDefinition.sceneNames.Contains(sceneName))
                    return zoneDefinition.zone;
            }
        }

        Debug.LogWarning($"Scene '{sceneName}' is not mapped to an AudioZone.");
        return AudioZone.None;
    }

    private Sound GetNextRandomMusicForZone(AudioZone zone)
    {
        List<Sound> validTracks = GetValidMusicTracksForZone(zone);

        if (validTracks.Count == 0)
        {
            Debug.LogWarning($"No valid music tracks found for zone: {zone}");
            return null;
        }

        if (!remainingZoneMusicTracks.TryGetValue(zone, out List<Sound> remainingTracks) || remainingTracks == null)
        {
            remainingTracks = new List<Sound>();
            remainingZoneMusicTracks[zone] = remainingTracks;
        }

        remainingTracks.RemoveAll(track => track == null || track.clip == null);

        if (remainingTracks.Count == 0)
            remainingTracks.AddRange(validTracks);

        int randomIndex = Random.Range(0, remainingTracks.Count);
        Sound selectedTrack = remainingTracks[randomIndex];

        remainingTracks.RemoveAt(randomIndex);

        return selectedTrack;
    }

    private List<Sound> GetValidMusicTracksForZone(AudioZone zone)
    {
        List<Sound> validTracks = new List<Sound>();

        ZoneMusicLibrary musicLibrary = FindZoneMusicLibrary(zone);

        if (musicLibrary == null)
        {
            Debug.LogWarning($"No ZoneMusicLibrary found for zone: {zone}");
            return validTracks;
        }

        if (musicLibrary.musicTracks == null || musicLibrary.musicTracks.Count == 0)
        {
            Debug.LogWarning($"ZoneMusicLibrary for zone '{zone}' has no music tracks.");
            return validTracks;
        }

        foreach (Sound musicTrack in musicLibrary.musicTracks)
        {
            if (musicTrack == null)
                continue;

            if (musicTrack.clip == null)
                continue;

            validTracks.Add(musicTrack);
        }

        return validTracks;
    }

    private ZoneMusicLibrary FindZoneMusicLibrary(AudioZone zone)
    {
        foreach (ZoneMusicLibrary musicLibrary in zoneMusicDefinitions)
        {
            if (musicLibrary == null)
                continue;

            if (musicLibrary.zone == zone)
                return musicLibrary;
        }

        return null;
    }

    private Sound FindSFX(string soundName)
    {
        foreach (SoundLibrary soundLibrary in soundGroups)
        {
            if (soundLibrary == null || soundLibrary.sounds == null)
                continue;

            foreach (Sound sound in soundLibrary.sounds)
            {
                if (sound == null)
                    continue;

                if (sound.name == soundName)
                    return sound;
            }
        }

        return null;
    }

    private bool IsAnyZoneMusicPlaying()
    {
        foreach (ZoneMusicLibrary musicLibrary in zoneMusicDefinitions)
        {
            if (musicLibrary == null || musicLibrary.musicTracks == null)
                continue;

            foreach (Sound musicTrack in musicLibrary.musicTracks)
            {
                if (musicTrack == null || musicTrack.source == null)
                    continue;

                if (musicTrack.source.isPlaying)
                    return true;
            }
        }

        return false;
    }

    private float GetTrackDuration(Sound music)
    {
        if (music == null || music.clip == null)
            return 0f;

        float pitch = Mathf.Abs(music.pitch);

        if (pitch <= 0f)
            pitch = 1f;

        return music.clip.length / pitch;
    }

    #endregion

    #region Coroutines

    private IEnumerator CrossfadeMusicCoroutine(Sound nextMusic, AudioZone nextZone)
    {
        Sound oldMusic = currentMusic;
        AudioSource oldSource = oldMusic != null ? oldMusic.source : null;

        if (nextMusic.source == null)
            CreateSourceForSound(nextMusic, $"Music_{nextZone}_{nextMusic.name}");

        AudioSource nextSource = nextMusic.source;

        ConfigureSourceFromSound(nextMusic);

        float oldStartVolume = oldSource != null ? oldSource.volume : 0f;
        float targetVolume = nextMusic.volume;

        nextSource.Stop();
        nextSource.volume = 0f;
        nextSource.Play();

        float timer = 0f;
        float maxDuration = Mathf.Max(fadeOutDuration, fadeInDuration);

        if (maxDuration <= 0f)
        {
            if (oldSource != null)
            {
                oldSource.Stop();
                oldSource.volume = oldMusic != null ? oldMusic.volume : 0f;
            }

            nextSource.volume = targetVolume;

            currentMusic = nextMusic;
            currentZone = nextZone;

            musicFadeCoroutine = null;

            StartMusicSequenceTimer(nextMusic, nextZone);

            yield break;
        }

        while (timer < maxDuration)
        {
            timer += Time.deltaTime;

            if (oldSource != null)
            {
                float fadeOutPercent = fadeOutDuration <= 0f ? 1f : Mathf.Clamp01(timer / fadeOutDuration);
                oldSource.volume = Mathf.Lerp(oldStartVolume, 0f, fadeOutPercent);
            }

            float fadeInPercent = fadeInDuration <= 0f ? 1f : Mathf.Clamp01(timer / fadeInDuration);
            nextSource.volume = Mathf.Lerp(0f, targetVolume, fadeInPercent);

            yield return null;
        }

        if (oldSource != null)
        {
            oldSource.Stop();
            oldSource.volume = oldMusic != null ? oldMusic.volume : 0f;
        }

        nextSource.volume = targetVolume;

        currentMusic = nextMusic;
        currentZone = nextZone;

        musicFadeCoroutine = null;

        StartMusicSequenceTimer(nextMusic, nextZone);
    }

    private IEnumerator MusicSequenceCoroutine(Sound music, AudioZone zone)
    {
        float duration = GetTrackDuration(music);

        if (duration > 0f)
            yield return new WaitForSeconds(duration);

        if (currentMusic != music)
            yield break;

        if (currentZone != zone)
            yield break;

        musicSequenceCoroutine = null;

        PlayNextRandomTrackInCurrentZone();
    }

    private IEnumerator PlaySFXDelayedCoroutine(string soundName, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        PlaySFX(soundName);
    }

    #endregion

    #region Source Helpers

    private void CreateSourceForSound(Sound sound, string sourceName)
    {
        if (sound == null)
            return;

        if (sound.source != null)
            return;

        GameObject sourceObject = new GameObject(sourceName);
        sourceObject.transform.SetParent(transform);

        AudioSource audioSource = sourceObject.AddComponent<AudioSource>();

        sound.source = audioSource;

        ConfigureSourceFromSound(sound);
    }

    private void ConfigureSourceFromSound(Sound sound)
    {
        if (sound == null || sound.source == null)
            return;

        sound.source.clip = sound.clip;
        sound.source.volume = sound.volume;
        sound.source.pitch = sound.pitch;
        sound.source.loop = false;
        sound.source.playOnAwake = false;
        sound.source.outputAudioMixerGroup = sound.mixerGroup;
    }

    private void StopAllZoneMusicSources()
    {
        foreach (ZoneMusicLibrary musicLibrary in zoneMusicDefinitions)
        {
            if (musicLibrary == null || musicLibrary.musicTracks == null)
                continue;

            foreach (Sound musicTrack in musicLibrary.musicTracks)
            {
                if (musicTrack == null || musicTrack.source == null)
                    continue;

                musicTrack.source.Stop();
                musicTrack.source.volume = musicTrack.volume;
            }
        }
    }

    private void StartMusicSequenceTimer(Sound music, AudioZone zone)
    {
        if (musicSequenceCoroutine != null)
            StopCoroutine(musicSequenceCoroutine);

        musicSequenceCoroutine = StartCoroutine(MusicSequenceCoroutine(music, zone));
    }

    private void StopMusicCoroutines()
    {
        if (musicFadeCoroutine != null)
        {
            StopCoroutine(musicFadeCoroutine);
            musicFadeCoroutine = null;
        }

        if (musicSequenceCoroutine != null)
        {
            StopCoroutine(musicSequenceCoroutine);
            musicSequenceCoroutine = null;
        }
    }

    #endregion
}