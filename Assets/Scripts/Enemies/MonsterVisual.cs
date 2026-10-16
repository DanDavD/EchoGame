using UnityEngine;

/// <summary>
/// The body of the blind monster, built from primitives and animated in code: a gaunt figure with no eyes and
/// enormous ears, long arms that swing as it walks and reach forward when it hunts.
///
/// The animation is driven only by how fast the monster moves, so it looks the same on every peer (the server owns the
/// real state; clients only see the NetworkTransform). It replaces the placeholder capsule's renderer, and adds no
/// colliders: the capsule collider and the NavMeshAgent keep doing the physics.
/// </summary>
[DisallowMultipleComponent]
public class MonsterVisual : MonoBehaviour
{
    [Tooltip("Metres from the monster's pivot down to the floor (the placeholder capsule is 2 m tall, pivot at its centre).")]
    [SerializeField] private float _pivotHeight = 1f;
    [Tooltip("Above this speed (m/s) the monster is hunting: it leans in and reaches forward.")]
    [SerializeField] private float _huntSpeed = 4.5f;

    private Transform _rig;
    private Transform _hips;
    private Transform _torso;
    private Transform _head;
    private Transform _earLeft;
    private Transform _earRight;
    private Transform _shoulderLeft;
    private Transform _shoulderRight;
    private Transform _legLeft;
    private Transform _legRight;

    private Vector3 _lastPosition;
    private float _speed;
    private float _phase;
    private float _lean;
    private float _seed;

    private void Awake()
    {
        _seed = Random.value * 10f;
        Build();
        _lastPosition = transform.position;
    }

    private void Build()
    {
        // Reuse the placeholder's material (EchoReveal, flesh pattern) and retire its capsule renderer.
        Material material = null;
        var placeholder = GetComponent<MeshRenderer>();
        if (placeholder != null)
        {
            material = placeholder.sharedMaterial;
            placeholder.enabled = false;
        }

        _rig = new GameObject("MonsterRig").transform;
        _rig.SetParent(transform, false);
        _rig.localPosition = new Vector3(0f, -_pivotHeight, 0f);

        _hips = Pivot("Hips", _rig, new Vector3(0f, 1.05f, 0f));
        _torso = Part("Torso", PrimitiveType.Capsule, _hips, new Vector3(0f, 0.38f, 0f), new Vector3(0.58f, 0.62f, 0.38f), material);

        Transform neck = Pivot("Neck", _hips, new Vector3(0f, 0.95f, 0.05f));
        _head = Pivot("Head", neck, Vector3.zero);
        Part("Skull", PrimitiveType.Sphere, _head, new Vector3(0f, 0.21f, 0f), new Vector3(0.27f, 0.44f, 0.3f), material);

        // The ears are the point of the creature: big dishes that flick towards every sound.
        _earLeft = Pivot("EarLeft", _head, new Vector3(-0.15f, 0.2f, -0.02f));
        _earRight = Pivot("EarRight", _head, new Vector3(0.15f, 0.2f, -0.02f));
        Part("EarDishLeft", PrimitiveType.Sphere, _earLeft, new Vector3(-0.04f, 0.38f, 0f), new Vector3(0.04f, 0.95f, 0.3f), material);
        Part("EarDishRight", PrimitiveType.Sphere, _earRight, new Vector3(0.04f, 0.38f, 0f), new Vector3(0.04f, 0.95f, 0.3f), material);
        _earLeft.localRotation = Quaternion.Euler(0f, 0f, 32f);
        _earRight.localRotation = Quaternion.Euler(0f, 0f, -32f);

        _shoulderLeft = Pivot("ShoulderLeft", _hips, new Vector3(-0.36f, 0.78f, 0f));
        _shoulderRight = Pivot("ShoulderRight", _hips, new Vector3(0.36f, 0.78f, 0f));
        Part("ArmLeft", PrimitiveType.Capsule, _shoulderLeft, new Vector3(0f, -0.5f, 0f), new Vector3(0.13f, 0.55f, 0.13f), material);
        Part("ArmRight", PrimitiveType.Capsule, _shoulderRight, new Vector3(0f, -0.5f, 0f), new Vector3(0.13f, 0.55f, 0.13f), material);

        _legLeft = Pivot("LegLeft", _hips, new Vector3(-0.15f, 0f, 0f));
        _legRight = Pivot("LegRight", _hips, new Vector3(0.15f, 0f, 0f));
        Part("LegLeftMesh", PrimitiveType.Capsule, _legLeft, new Vector3(0f, -0.5f, 0f), new Vector3(0.14f, 0.5f, 0.14f), material);
        Part("LegRightMesh", PrimitiveType.Capsule, _legRight, new Vector3(0f, -0.5f, 0f), new Vector3(0.14f, 0.5f, 0.14f), material);
    }

    private static Transform Pivot(string name, Transform parent, Vector3 localPosition)
    {
        var pivot = new GameObject(name).transform;
        pivot.SetParent(parent, false);
        pivot.localPosition = localPosition;
        return pivot;
    }

    private static Transform Part(string name, PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 scale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = name;
        Object.Destroy(part.GetComponent<Collider>());
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = scale;
        if (material != null) part.GetComponent<MeshRenderer>().sharedMaterial = material;
        return part.transform;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f || _rig == null) return;

        // Speed from the real movement, smoothed (a NetworkTransform moves in small steps).
        Vector3 delta = transform.position - _lastPosition;
        delta.y = 0f;
        _lastPosition = transform.position;
        _speed = Mathf.Lerp(_speed, delta.magnitude / dt, 1f - Mathf.Exp(-6f * dt));

        float t = Time.time + _seed;
        float walk = Mathf.Clamp01(_speed / 2.5f);
        float hunt = Mathf.Clamp01((_speed - _huntSpeed * 0.6f) / (_huntSpeed * 0.4f));

        _phase += dt * (1.5f + _speed * 1.9f);
        float swing = Mathf.Sin(_phase);

        // Legs and arms swing against each other; a hunting monster reaches forward instead of swinging.
        float armSwing = swing * 38f * walk * (1f - hunt);
        float armReach = -70f * hunt + Mathf.Sin(t * 11f) * 6f * hunt;
        float armIdle = Mathf.Sin(t * 0.9f) * 5f * (1f - walk);
        _shoulderLeft.localRotation = Quaternion.Euler(armSwing + armReach + armIdle, 0f, 6f);
        _shoulderRight.localRotation = Quaternion.Euler(-armSwing + armReach - armIdle, 0f, -6f);
        _legLeft.localRotation = Quaternion.Euler(-swing * 34f * walk, 0f, 0f);
        _legRight.localRotation = Quaternion.Euler(swing * 34f * walk, 0f, 0f);

        // Body: a bob while walking, a lean into the chase, a slow breath.
        float bob = Mathf.Abs(Mathf.Sin(_phase)) * 0.05f * walk;
        _hips.localPosition = new Vector3(0f, 1.05f + bob, 0f);
        _lean = Mathf.Lerp(_lean, 10f * walk + 14f * hunt, 1f - Mathf.Exp(-5f * dt));
        _hips.localRotation = Quaternion.Euler(_lean, Mathf.Sin(_phase) * 4f * walk, 0f);
        _torso.localScale = new Vector3(0.58f, 0.62f * (1f + 0.03f * Mathf.Sin(t * 2.1f)), 0.38f);

        // Head: it listens. Standing still it sweeps left and right; moving it looks where it goes, tilted down while hunting.
        float scan = Mathf.Sin(t * 0.7f) * 38f * (1f - walk);
        _head.localRotation = Quaternion.Euler(8f * hunt - 4f, scan, Mathf.Sin(t * 0.43f) * 6f);

        // Ears flick all the time and open wide when it is hunting.
        float flick = Mathf.Sin(t * 8.5f) * 4f + Mathf.Sin(t * 3.1f) * 3f;
        float open = 32f + 14f * hunt;
        _earLeft.localRotation = Quaternion.Euler(flick, 0f, open);
        _earRight.localRotation = Quaternion.Euler(-flick, 0f, -open);
    }
}
