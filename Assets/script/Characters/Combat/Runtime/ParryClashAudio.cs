using UnityEngine;

[DisallowMultipleComponent]
public sealed class ParryClashAudio : MonoBehaviour
{
    public AudioClip[] clips;
    public int[] startSamples;
    [Range(0,1)]public float volume=.8f;
    AudioSource source;
    public void Play()
    {
        if(clips==null||clips.Length==0)return;
        int index=Random.Range(0,clips.Length);var clip=clips[index];if(clip==null)return;
        if(source==null)
        {
            var go=new GameObject("Parry clash audio");go.transform.SetParent(transform,false);
            source=go.AddComponent<AudioSource>();source.playOnAwake=false;source.loop=false;
            source.spatialBlend=0;source.dopplerLevel=0;source.ignoreListenerPause=true;
        }
        source.Stop();source.clip=clip;source.volume=volume;source.pitch=1;
        source.timeSamples=startSamples!=null&&index<startSamples.Length?Mathf.Clamp(startSamples[index],0,clip.samples-1):0;
        source.Play();
    }
}
