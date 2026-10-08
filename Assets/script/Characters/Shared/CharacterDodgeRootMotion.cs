using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(Animator))]
public sealed class CharacterDodgeRootMotion : MonoBehaviour
{
    public CharacterMotor driver;
    Animator animator;
    void OnEnable(){animator=GetComponent<Animator>();if(driver==null)driver=GetComponentInParent<CharacterMotor>();}
    void OnAnimatorMove()
    {
        // Only dodge and knockdown clips supply displacement; the capsule resolves collisions.
        if(driver==null||animator==null)return;
        if(driver.IsIncapacitated)driver.ApplyIncapacitatedRootMotion(animator.deltaPosition);
        else driver.ApplyDodgeRootMotion(animator.deltaPosition,Time.deltaTime);
    }
}
