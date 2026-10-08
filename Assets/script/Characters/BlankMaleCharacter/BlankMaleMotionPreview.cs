using UnityEngine;
using UnityEngine.InputSystem;

public sealed class BlankMaleMotionPreview : MonoBehaviour
{
    public Animator animator;
    public bool autoCycle = true;
    public int motion;
    float elapsed;
    static readonly string[] States = { "Idle", "Walk", "Run" };

    public void SelectMotion(int index)
    {
        motion = Mathf.Clamp(index, 0, States.Length - 1);
        elapsed = 0;
        animator.CrossFadeInFixedTime(States[motion], 0.2f);
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.digit1Key.wasPressedThisFrame) { autoCycle = false; SelectMotion(0); }
            if (keyboard.digit2Key.wasPressedThisFrame) { autoCycle = false; SelectMotion(1); }
            if (keyboard.digit3Key.wasPressedThisFrame) { autoCycle = false; SelectMotion(2); }
            if (keyboard.spaceKey.wasPressedThisFrame) { autoCycle = !autoCycle; elapsed = 0; }
        }
        if (autoCycle && (elapsed += Time.deltaTime) >= 5) SelectMotion((motion + 1) % 3);
    }
}
