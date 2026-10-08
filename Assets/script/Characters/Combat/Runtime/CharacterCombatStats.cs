using UnityEngine;

/// <summary>Health and damage are independent of poise. Lethal hits take animation ownership immediately.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(CharacterMotor)), DefaultExecutionOrder(20000)]
public sealed class CharacterCombatStats : MonoBehaviour
{
    public bool enemyOnly;
    public bool invincible;
    [Range(.01f,1)] public float startingHealthFraction = 1;
    [Min(0)] public int pokemonId;
    public TextAsset pokedex;
    [Min(1)] public float maximumHealth = 80, attack = 15, defense = 50, specialAttack = 30, specialDefense = 50, speed = 50;
    public string displayName;
    [Range(0,1)] public float killHealFraction=.1f;
    public string[] elementalTypes;
    public const string BackDeath = "DeathBackward", ForwardDeath = "DeathForward", DeadParameter = "IsDead";
    public float Health { get; private set; }
    public float LastDamage { get; private set; }
    public float LastTypeMultiplier { get; private set; } = 1;
    float appliedHealth, appliedAttack, appliedDefense;
    public bool IsDead { get; private set; }
    public bool IsCapturing { get; private set; }
    public bool IsCaptured { get; private set; }
    public bool CanCapture => enemyOnly && !IsDead && !IsCapturing && !IsCaptured && Health > 0 && Health < maximumHealth * .5f;
    public bool BeginCapture() { if(!CanCapture)return false; IsCapturing=true; return true; }
    public void CancelCapture() { IsCapturing=false; }
    public void CompleteCapture() { IsCapturing=false; IsCaptured=true; gameObject.SetActive(false); }
    public bool LastDeathFromBack { get; private set; }
    public int DamageCount { get; private set; }
    CharacterMotor driver;

    void Awake()
    {
        driver = GetComponent<CharacterMotor>();
        ApplyDatabase();
        Health = maximumHealth * startingHealthFraction;
        if (!enemyOnly && Application.isPlaying) ApplyProgression(GameSession.Ensure().Progression);
    }
    void Start()
    {
        if (!enemyOnly) return;
        driver.SetPlayerInput(false);
        var selection = FindFirstObjectByType<PlayerRoster>();
        if (selection != null) selection.RegisterEnemy(driver);
    }
    public void ApplyDatabase()
    {
        PokemonDataSerch data = GetComponent<PokemonDataSerch>();
        if (data != null)
        {
            data.Read();
        }
    }
    public static bool Dead(Component actor) => actor != null && actor.GetComponent<CharacterCombatStats>() is CharacterCombatStats stats && (stats.IsDead || stats.IsCapturing || stats.IsCaptured);
    public static bool CanSelect(CharacterMotor actor) => actor != null && actor.isActiveAndEnabled &&
        (!(actor.GetComponent<CharacterCombatStats>() is CharacterCombatStats stats) || (!stats.enemyOnly && !stats.IsDead));
    public static float CalculateDamage(float offense, float armor, float power) =>
        Mathf.Max(1, Mathf.Round(Mathf.Max(1, offense) * Mathf.Max(.01f, power) * 100f / (100f + Mathf.Max(0, armor))));
    public void ApplyProgression(PlayerProgression progress)
    {
        if(enemyOnly || progress == null) return;
        float hp = progress.healthLevel * 20f, atk = progress.attackLevel * 5f, def = progress.defenseLevel * 5f;
        maximumHealth += hp - appliedHealth;
        if(!IsDead) Health = Mathf.Clamp(Health + hp - appliedHealth, 0, maximumHealth);
        attack += atk - appliedAttack; specialAttack += atk - appliedAttack;
        defense += def - appliedDefense; specialDefense += def - appliedDefense;
        appliedHealth = hp; appliedAttack = atk; appliedDefense = def;
    }
    public bool ReceiveDamage(CharacterMotor attacker, bool fromBack, bool special = false, float power = .35f, string attackType = null, float fixedBaseDamage = -1)
    {
        if (invincible) return false;
        if (IsDead || IsCapturing || IsCaptured || attacker == driver || Dead(attacker)) return false;
        var source = attacker != null ? attacker.GetComponent<CharacterCombatStats>() : null;
        if (enemyOnly && source != null && source.enemyOnly) return false;
        float offense = source != null ? (special ? source.specialAttack : source.attack) : 60;
        LastTypeMultiplier = PokemonTypeChart.Multiplier(attackType ?? PlayerProgression.AttackType(attacker), elementalTypes);
        float baseDamage = fixedBaseDamage >= 0 ? fixedBaseDamage : CalculateDamage(offense, special ? specialDefense : defense, power);
        LastDamage = LastTypeMultiplier == 0 ? 0 : Mathf.Max(1, Mathf.Round(baseDamage * LastTypeMultiplier));
        if(LastDamage == 0) { DamagePopupDisplay.Show(this, 0); return false; }
        Health = Mathf.Max(0, Health - LastDamage); DamageCount++;
        DamagePopupDisplay.Show(this,LastDamage);
        GetComponent<CharacterBlood>()?.AddHit(attacker!=null?attacker.transform.position:transform.position-transform.forward*2);
        if (Health <= 0)
        {
            Die(fromBack);
            if(enemyOnly&&source!=null&&!source.enemyOnly)source.RestoreHealth(source.maximumHealth*source.killHealFraction);
        }
        else if(enemyOnly)GetComponent<PartyEnemyAI>()?.NotifyAttacked(attacker);
        return true;
    }
    public void RestoreHealth(float amount)
    {
        if(IsDead||IsCapturing||IsCaptured)return;
        Health=Mathf.Min(maximumHealth,Health+Mathf.Max(0,amount));
    }
    void Die(bool fromBack)
    {
        IsDead = true; LastDeathFromBack = fromBack;
        driver.SetIncapacitated(true);
        var parry = GetComponent<CharacterParryAction>(); if (parry != null) parry.enabled = false;
        driver.ClearAIInput(); driver.combatLookTarget = null;
        var animator = driver.animator;
        if (animator == null) return;
        animator.SetBool(DeadParameter, true);
        animator.speed = 1; animator.updateMode = AnimatorUpdateMode.Normal;
        for (int i = 1; i < animator.layerCount; i++) animator.SetLayerWeight(i, 0);
        driver.Animation.CrossFade(CharacterAnimationDirector.Action.Death, fromBack ? ForwardDeath : BackDeath, .07f, 0, 0);
    }
    void LateUpdate()
    {
        if (!IsDead || driver.animator == null) return;
        // Weapon grip and additive hit layers cannot cover the terminal full-body pose.
        for (int i = 1; i < driver.animator.layerCount; i++) driver.animator.SetLayerWeight(i, 0);
    }
}

