using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent,RequireComponent(typeof(CharacterMotor)),DefaultExecutionOrder(21020)]
public sealed class CharacterActionAudio : MonoBehaviour
{
    [Range(0,1)]public float volume=.7f;
    [Header("Parry")]
    public AudioClip[] parryClips;
    public int[] parryStartSamples;
    [Range(0,1)] public float parryVolume=.8f;
    CharacterMotor driver;
    CharacterComboAttack combo;
    CharacterCombatStats stats;
    OrbDashAttack dash;
    Vector3 previousPosition;
    int attack=-1,contact,dodges,damage;
    bool grounded,wasDashing;
    float stepDistance,airTime,fallSpeed,nextHurt;
    AudioSource magicVoice;
    AudioSource parryVoice;
    void Awake(){driver=GetComponent<CharacterMotor>();combo=GetComponent<CharacterComboAttack>();stats=GetComponent<CharacterCombatStats>();dash=GetComponent<OrbDashAttack>();}
    void OnEnable(){if(driver==null)Awake();previousPosition=transform.position;grounded=driver.IsGrounded;stepDistance=airTime=0;dodges=driver.DodgeCount;damage=stats!=null?stats.DamageCount:0;attack=combo!=null?combo.AttackCount:-1;}
    void LateUpdate()
    {
        if(Time.deltaTime<=0)return;
        var position=transform.position;float moved=Vector3.ProjectOnPlane(position-previousPosition,Vector3.up).magnitude;previousPosition=position;
        if(stats!=null&&stats.DamageCount!=damage){damage=stats.DamageCount;if(Time.unscaledTime>=nextHurt){Play(stats.enemyOnly?GameplayCue.EnemyHurt:GameplayCue.PlayerHurt,.65f);nextHurt=Time.unscaledTime+.38f;}}
        if(!driver.enabled||!driver.isActiveAndEnabled||CharacterCombatStats.Dead(driver)){StopMagic();grounded=driver.IsGrounded;stepDistance=airTime=0;return;}
        if(driver.DodgeCount!=dodges){dodges=driver.DodgeCount;Play(GameplayCue.Dodge,.42f);}
        if(!driver.IsGrounded){airTime+=Time.deltaTime;fallSpeed=Mathf.Min(fallSpeed,driver.VerticalSpeed);}
        else{if(!grounded&&airTime>.14f&&fallSpeed< -1.5f)Play(GameplayCue.Land,.65f);airTime=0;fallSpeed=0;}
        grounded=driver.IsGrounded;
        if(grounded&&!driver.IsDodging&&!driver.IsAttacking&&!driver.IsActionBlocked&&driver.PlanarSpeed>.25f&&moved<2.5f){stepDistance+=moved;if(stepDistance>=(driver.IsRunning?1.05f:.7f)){stepDistance=0;Play(SceneManager.GetActiveScene().name==GameSession.ShopScene?GameplayCue.WoodStep:GameplayCue.RoadStep,driver.acceptPlayerInput?.35f:.18f);}}
        else stepDistance=0;
        bool dashing=dash!=null&&dash.IsDashing;if(dashing&&!wasDashing)Play(GameplayCue.MagicBurst,.45f);wasDashing=dashing;
        if(combo==null||!combo.IsAttacking||combo.IsOrbAttack)return;
        if(attack!=combo.AttackCount){attack=combo.AttackCount;contact=0;}
        var strike=combo.ActiveStrike;if(strike==null||strike.clip==null)return;
        var state=driver.animator.IsInTransition(0)?driver.animator.GetNextAnimatorStateInfo(0):driver.animator.GetCurrentAnimatorStateInfo(0);
        if(!state.IsName(strike.stateName))return;
        float frame=state.normalizedTime*strike.clip.length*strike.clip.frameRate;
        while(contact<strike.ContactCount&&frame>=strike.ContactFrame(contact)-3){Play(combo.IsSwordAttack?(combo.IsRunningAttack||combo.IsFifthStrike?GameplayCue.SwordHeavy:GameplayCue.SwordSwing):GameplayCue.FistSwing,combo.IsSwordAttack?.68f:.32f);contact++;}
    }
    void Play(GameplayCue cue,float gain){GameplayAudio.Play(cue,transform.position+Vector3.up,volume*gain);}
    public void PlayParry()
    {
        if(parryClips==null||parryClips.Length==0)return;
        int index=Random.Range(0,parryClips.Length);
        AudioClip clip=parryClips[index];
        if(clip==null)return;
        if(parryVoice==null)
        {
            var go=new GameObject("Parry clash audio");
            go.transform.SetParent(transform,false);
            parryVoice=go.AddComponent<AudioSource>();
            parryVoice.playOnAwake=false;
            parryVoice.loop=false;
            parryVoice.spatialBlend=0;
            parryVoice.dopplerLevel=0;
            parryVoice.ignoreListenerPause=true;
        }
        parryVoice.Stop();
        parryVoice.clip=clip;
        parryVoice.volume=parryVolume;
        parryVoice.pitch=1;
        parryVoice.timeSamples=parryStartSamples!=null&&index<parryStartSamples.Length
            ?Mathf.Clamp(parryStartSamples[index],0,clip.samples-1):0;
        parryVoice.Play();
    }
    public void Magic(bool firing)
    {
        if(!Application.isPlaying||GameplayAudio.Library==null)return;
        if(magicVoice==null){var go=new GameObject("Spell voice");go.transform.SetParent(transform,false);go.transform.localPosition=Vector3.up;magicVoice=GameplayAudio.Configure(go);}
        magicVoice.Stop();magicVoice.clip=GameplayAudio.Library.Pick(firing?GameplayCue.MagicBeam:GameplayCue.MagicCharge);magicVoice.volume=volume*(firing?.65f:.4f)*GameplayAudio.Library.masterVolume;magicVoice.pitch=1;magicVoice.Play();
    }
    public void StopMagic(){if(magicVoice!=null)magicVoice.Stop();}
    void OnDisable(){StopMagic();}
}
