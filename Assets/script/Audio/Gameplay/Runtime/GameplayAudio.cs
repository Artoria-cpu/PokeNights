using UnityEngine;
using UnityEngine.SceneManagement;

// Shared, bounded set of 3D one-shots. All references live in the editable audio library.
public sealed class GameplayAudio : MonoBehaviour
{
    static GameplayAudio instance;
    static GameplayAudioLibrary library;
    public static GameplayAudioLibrary Library=>library!=null?library:library=Resources.Load<GameplayAudioLibrary>("GameplayAudio");
    readonly AudioSource[] voices=new AudioSource[24];
    AudioListener listener;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics(){instance=null;library=null;}
    public static AudioSource Configure(GameObject go)
    {
        var source=go.AddComponent<AudioSource>();source.playOnAwake=false;source.loop=false;source.spatialBlend=1;source.dopplerLevel=0;source.minDistance=2;source.maxDistance=24;source.rolloffMode=AudioRolloffMode.Linear;source.priority=140;return source;
    }
    public static void Play(GameplayCue cue,Vector3 position,float volume=1)
    {
        if(!Application.isPlaying||Library==null)return;
        var clip=Library.Pick(cue);if(clip==null)return;
        if(instance==null){var root=new GameObject("Gameplay audio voices");instance=root.AddComponent<GameplayAudio>();DontDestroyOnLoad(root);}
        instance.PlayClip(clip,position,volume);
    }
    void Awake(){SceneManager.sceneLoaded+=SceneLoaded;}
    void OnDestroy(){SceneManager.sceneLoaded-=SceneLoaded;if(instance==this)instance=null;}
    void SceneLoaded(Scene scene,LoadSceneMode mode){foreach(var voice in voices)if(voice!=null)voice.Stop();listener=null;}
    void PlayClip(AudioClip clip,Vector3 position,float volume)
    {
        if(listener==null)foreach(var candidate in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))if(candidate.isActiveAndEnabled){listener=candidate;break;}
        if(listener!=null&&(listener.transform.position-position).sqrMagnitude>24*24)return;
        for(int i=0;i<voices.Length;i++){
            if(voices[i]==null){var go=new GameObject("SFX voice "+i);go.transform.SetParent(transform,false);voices[i]=Configure(go);}
            var voice=voices[i];if(voice.isPlaying)continue;
            voice.transform.position=position;voice.clip=clip;voice.volume=Mathf.Clamp01(volume)*Library.masterVolume;voice.pitch=Random.Range(.96f,1.04f);voice.Play();return;
        }
    }
}
