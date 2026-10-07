using System.Collections.Generic;
using UnityEngine;

// 배경음악과 효과음을 재생합니다. 소리 파일은 Resources/Audio 폴더에 있어요. (Art/make_audio.py로 직접 합성한 소리)
//   AudioManager.PlayMusic("battle")  → bgm_battle 반복 재생 (이전 곡은 부드럽게 줄어듦)
//   AudioManager.Play("slash")        → sfx_slash 한 번 재생
// 음량은 설정 화면에서 바꿀 수 있어요.
public class AudioManager : MonoBehaviour
{
    static AudioManager instance;

    const int Voices = 12;               // 동시에 낼 수 있는 효과음 수
    const float SameSoundGap = 0.06f;    // 같은 효과음이 너무 겹치지 않게 최소 간격(초)
    const float MusicBaseVolume = 0.55f;
    const float FadeSeconds = 0.8f;

    readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
    AudioSource musicA, musicB;          // 곡을 바꿀 때 서로 번갈아 쓰며 크로스페이드
    AudioSource[] voices;
    int nextVoice;
    string currentMusic;
    float fade = 1f;                     // 0 → 1: 새 곡이 커지고 이전 곡이 작아짐

    static AudioManager Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("AudioManager");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<AudioManager>();
                instance.Init();
            }
            return instance;
        }
    }

    void Init()
    {
        musicA = gameObject.AddComponent<AudioSource>();
        musicB = gameObject.AddComponent<AudioSource>();
        foreach (var m in new[] { musicA, musicB }) { m.loop = true; m.playOnAwake = false; m.volume = 0f; }
        voices = new AudioSource[Voices];
        for (int i = 0; i < Voices; i++)
        {
            voices[i] = gameObject.AddComponent<AudioSource>();
            voices[i].playOnAwake = false;
        }
    }

    AudioClip Clip(string fileName)
    {
        if (!clips.TryGetValue(fileName, out var clip))
        {
            clip = Resources.Load<AudioClip>("Audio/" + fileName);
            clips[fileName] = clip;
        }
        return clip;
    }

    static float MusicVolume => MusicBaseVolume * SaveData.MusicVolume;

    // ---------------- 배경음악 ----------------

    public static void PlayMusic(string name)
    {
        var a = Instance;
        if (a.currentMusic == name) return;
        a.currentMusic = name;
        var clip = name == null ? null : a.Clip("bgm_" + name);
        // 지금 크게 나오는 쪽을 '이전 곡'으로, 다른 쪽에서 새 곡을 시작
        var swap = a.musicA; a.musicA = a.musicB; a.musicB = swap;
        a.musicA.Stop();
        a.musicA.volume = 0f;
        if (clip != null)
        {
            a.musicA.clip = clip;
            a.musicA.Play();
        }
        a.fade = 0f;
    }

    public static void StopMusic() => PlayMusic(null);

    void Update()
    {
        fade = Mathf.Min(1f, fade + Time.unscaledDeltaTime / FadeSeconds);
        musicA.volume = MusicVolume * fade;   // 새 곡
        musicB.volume = MusicVolume * (1f - fade); // 이전 곡
        if (fade >= 1f && musicB.isPlaying) musicB.Stop();
    }

    // ---------------- 효과음 ----------------

    public static void Play(string name, float volume = 1f, float pitchJitter = 0.06f)
    {
        if (string.IsNullOrEmpty(name) || SaveData.SfxVolume <= 0f) return;
        var a = Instance;
        float now = Time.unscaledTime;
        if (a.lastPlayed.TryGetValue(name, out float last) && now - last < SameSoundGap) return;
        var clip = a.Clip("sfx_" + name);
        if (clip == null) return;
        a.lastPlayed[name] = now;

        var v = a.voices[a.nextVoice];
        a.nextVoice = (a.nextVoice + 1) % Voices;
        v.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        v.PlayOneShot(clip, volume * SaveData.SfxVolume);
    }
}
