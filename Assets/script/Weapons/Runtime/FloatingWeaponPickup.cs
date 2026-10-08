using UnityEngine;

[RequireComponent(typeof(SphereCollider), typeof(Rigidbody))]
public sealed class FloatingWeaponPickup : MonoBehaviour
{
    public CharacterWeaponEquipment equipment;
    public Transform visual;
    public bool isOrb;
    public float spinSpeed = 55f, bobHeight = .12f, bobSpeed = 1.8f;
    Vector3 restPosition;
    bool collected;
    void Awake()
    {
        if (visual != null) restPosition = visual.localPosition;
        GetComponent<SphereCollider>().isTrigger = true;
        var body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
    }
    void Update()
    {
        if (visual == null || collected) return;
        visual.localPosition = restPosition + Vector3.up * Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        visual.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
    }
    void OnTriggerEnter(Collider other) { TryCollect(other); }
    void OnTriggerStay(Collider other) { TryCollect(other); }
    void TryCollect(Collider other)
    {
        if (collected || equipment == null) return;
        var driver = other.GetComponentInParent<CharacterMotor>();
        if (driver == null || !driver.acceptPlayerInput) return;
        if(!(isOrb?equipment.AcquireOrb(driver):equipment.AcquireSword(driver)))return;
        collected = true;
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
}

