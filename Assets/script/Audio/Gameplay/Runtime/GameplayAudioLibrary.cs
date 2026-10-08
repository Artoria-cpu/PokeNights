using UnityEngine;

public enum GameplayCue { SwordSwing, SwordHeavy, FistSwing, PunchHit, SwordHit, MagicShot, MagicBurst, MagicCharge, MagicBeam, PlayerHurt, EnemyHurt, WoodStep, RoadStep, Land, Dodge }
[CreateAssetMenu(menuName="Pokenight/Gameplay Audio")]
public sealed class GameplayAudioLibrary : ScriptableObject
{
    public AudioClip[] swordSwings, punches, enemyHurt, woodSteps, roadSteps;
    public AudioClip swordHeavy, air, magicShot, magicBurst, magicCharge, magicBeam, playerHurt, landing;
    [Range(0,1)] public float masterVolume=.75f;
    public AudioClip Pick(GameplayCue cue)
    {
        switch(cue){
            case GameplayCue.SwordSwing: case GameplayCue.SwordHit:return Pick(swordSwings);
            case GameplayCue.SwordHeavy:return swordHeavy;
            case GameplayCue.FistSwing: case GameplayCue.Dodge:return air;
            case GameplayCue.PunchHit:return Pick(punches);
            case GameplayCue.MagicShot:return magicShot;
            case GameplayCue.MagicBurst:return magicBurst;
            case GameplayCue.MagicCharge:return magicCharge;
            case GameplayCue.MagicBeam:return magicBeam;
            case GameplayCue.PlayerHurt:return playerHurt;
            case GameplayCue.EnemyHurt:return Pick(enemyHurt);
            case GameplayCue.WoodStep:return Pick(woodSteps);
            case GameplayCue.RoadStep:return Pick(roadSteps);
            default:return landing;
        }
    }
    static AudioClip Pick(AudioClip[] clips)=>clips!=null&&clips.Length>0?clips[Random.Range(0,clips.Length)]:null;
}
