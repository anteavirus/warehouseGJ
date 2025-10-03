using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class StableHangingLight : MonoBehaviour
{
    [Header("Light Settings")]
    public float lightIntensity = 2f;
    public float lightRange = 5f;
    public Color lightColor = Color.yellow;
    public GameObject bulbPrefab;
    public Material bulbMaterial;
    public Material wireMaterial;

    [Header("Rope Physics")]
    [Range(3, 15)] public int segments = 6;
    [Range(0.1f, 1f)] public float segmentLength = 0.3f;
    [Range(0.01f, 0.2f)] public float ropeThickness = 0.05f;
    [Range(0.1f, 2f)] public float swingDamping = 0.5f;
    [Range(0.1f, 5f)] public float bulbMass = 0.5f;

    [Header("Joint Settings")]
    [Range(5f, 90f)] public float jointLimit = 45f;
    [Range(0.1f, 5f)] public float jointSpring = 2f;
    [Range(0f, 2f)] public float jointDamper = 0.5f;

    [Header("Stabilization")]
    public float velocityLimit = 5f;
    public float angularVelocityLimit = 50f;
    public bool enableStabilizer = true;
    public LayerMask collisionLayers = ~0;
    public LayerMask ropeLayers = 0;

    private GameObject anchor;
    private List<GameObject> ropeSegments = new List<GameObject>();
    private List<ConfigurableJoint> joints = new List<ConfigurableJoint>();
    private Light bulbLight;
    private Rigidbody bulbRigidbody;
    private bool applicationPlaying;

    void Start()
    {
        Physics.defaultSolverIterations = 25;
        applicationPlaying = Application.isPlaying;
        if (applicationPlaying) InitializeSystem();
    }

    void OnDestroy()
    {
        if (!applicationPlaying) Cleanup();
    }

    void InitializeSystem()
    {
        Cleanup();
        CreateAnchor();
        CreateRopeChain();
        CreateBulb();
        if (enableStabilizer) StartCoroutine(PhysicsStabilizer());
    }

    void Cleanup()
    {
        foreach (Transform child in transform)
        {
            if (child == null) continue;
            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }
        ropeSegments.Clear();
        joints.Clear();
    }

    void CreateAnchor()
    {
        
        anchor = new GameObject("Anchor");
        anchor.transform.parent = transform;
        anchor.transform.position = transform.position + Vector3.up * (segments * segmentLength);

        anchor.layer = (int)Mathf.Log(ropeLayers.value, 2);

        Rigidbody rb = anchor.AddComponent<Rigidbody>();
        rb.isKinematic = true;
    }

    void CreateRopeChain()
    {
        Rigidbody previousRb = anchor.GetComponent<Rigidbody>();
        Vector3 currentPos = anchor.transform.position;

        for (int i = 0; i < segments; i++)
        {
            GameObject segment = CreateSegment(i, currentPos);
            ConfigureSegmentPhysics(segment, previousRb);

            previousRb = segment.GetComponent<Rigidbody>();
            ropeSegments.Add(segment);
            currentPos += Vector3.down * segmentLength;
        }
    }

    GameObject CreateSegment(int index, Vector3 position)
    {
        GameObject segment = new GameObject($"Segment_{index}");
        segment.transform.parent = transform;
        segment.transform.position = position;
        segment.layer = gameObject.layer;

        segment.layer = (int)Mathf.Log(ropeLayers.value, 2);

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.transform.parent = segment.transform;
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale = new Vector3(ropeThickness, segmentLength, ropeThickness);
        visual.GetComponent<MeshRenderer>().material = wireMaterial;
        Destroy(visual.GetComponent<Collider>());

        return segment;
    }

    void ConfigureSegmentPhysics(GameObject segment, Rigidbody previousRb)
    {
        Rigidbody rb = segment.AddComponent<Rigidbody>();
        rb.mass = 0.01f;
        rb.drag = swingDamping;
        rb.angularDrag = swingDamping * 2f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        BoxCollider col = segment.AddComponent<BoxCollider>();
        col.size = new Vector3(ropeThickness, segmentLength, ropeThickness);
        col.material = CreatePhysicsMaterial();

        ConfigurableJoint joint = segment.AddComponent<ConfigurableJoint>();
        joint.connectedBody = previousRb;
        joint.autoConfigureConnectedAnchor = false;

        joint.anchor = new Vector3(0, segmentLength / 2, 0);
        joint.connectedAnchor = new Vector3(0, -segmentLength / 2, 0);

        joint.xMotion = joint.yMotion = joint.zMotion = ConfigurableJointMotion.Locked;

        JointDrive drive = new JointDrive
        {
            positionSpring = jointSpring,
            positionDamper = jointDamper,
            maximumForce = Mathf.Infinity
        };

        joint.angularXDrive = drive;
        joint.angularYZDrive = drive;

        SoftJointLimit limit = new SoftJointLimit
        {
            limit = jointLimit,
            bounciness = 0f
        };

        joint.angularYLimit = joint.angularZLimit = joint.highAngularXLimit = joint.lowAngularXLimit = limit;

        joints.Add(joint);
    }

    void CreateBulb()
    {
        GameObject bulb = bulbPrefab ? Instantiate(bulbPrefab) : CreateDefaultBulb();
        bulb.name = "LightBulb";
        bulb.transform.parent = transform;
        bulb.transform.position = transform.position;
        bulb.layer = gameObject.layer;

        if (!bulb.TryGetComponent(out bulbRigidbody))
        {
            bulbRigidbody = bulb.AddComponent<Rigidbody>();
        }
        ConfigureBulbPhysics(bulbRigidbody);

        if (!bulb.TryGetComponent(out SphereCollider col))
        {
            col = bulb.AddComponent<SphereCollider>();
            col.radius = 0.15f;
        }
        col.material = CreatePhysicsMaterial();

        ConfigurableJoint joint = bulb.AddComponent<ConfigurableJoint>();
        joint.connectedBody = ropeSegments[^1].GetComponent<Rigidbody>();
        joint.autoConfigureConnectedAnchor = false;
        joint.anchor = bulbPrefab != null ? new Vector3(0, 0.5f, 0) : Vector3.zero;
        joint.connectedAnchor = bulbPrefab != null ? new Vector3(0, -0.45f, 0) : Vector3.zero;

        joint.xMotion = ConfigurableJointMotion.Locked;
        joint.yMotion = ConfigurableJointMotion.Locked;
        joint.zMotion = ConfigurableJointMotion.Locked;

        JointDrive drive = new JointDrive
        {
            positionSpring = jointSpring,
            positionDamper = jointDamper,
            maximumForce = Mathf.Infinity
        };

        joint.angularXDrive = drive;
        joint.angularYZDrive = drive;

        SoftJointLimit limit = new SoftJointLimit
        {
            limit = jointLimit,
            bounciness = 0f
        };

        joint.angularYLimit = limit;
        joint.angularZLimit = limit;
        joint.highAngularXLimit = limit;
        joint.lowAngularXLimit = limit;

        if (!bulb.TryGetComponent(out bulbLight))
        {
            bulbLight = bulb.AddComponent<Light>();
        }
        bulbLight.type = LightType.Point;
        bulbLight.intensity = lightIntensity;
        bulbLight.range = lightRange;
        bulbLight.color = lightColor;
    }

    GameObject CreateDefaultBulb()
    {
        GameObject bulb = new GameObject("Bulb");

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visual.transform.parent = bulb.transform;
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale = Vector3.one * 0.3f;
        Destroy(visual.GetComponent<Collider>());

        if (bulbMaterial != null && visual.TryGetComponent<Renderer>(out var renderer))
            renderer.material = bulbMaterial;
            
        return bulb;
    }

    void ConfigureBulbPhysics(Rigidbody rb)
    {
        rb.mass = bulbMass;
        rb.drag = swingDamping;
        rb.angularDrag = swingDamping * 2f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    PhysicMaterial CreatePhysicsMaterial()
    {
        return new PhysicMaterial
        {
            dynamicFriction = 0.1f,
            staticFriction = 0.1f,
            bounciness = 0f,
            frictionCombine = PhysicMaterialCombine.Minimum,
            bounceCombine = PhysicMaterialCombine.Minimum
        };
    }

    IEnumerator PhysicsStabilizer()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);

            foreach (var segment in ropeSegments)
            {
                if (segment == null) continue;

                if (segment.TryGetComponent<Rigidbody>(out var rb))
                {
                    if (rb.velocity.magnitude > velocityLimit)
                        rb.velocity *= 0.8f;
                    
                    if (rb.angularVelocity.magnitude > angularVelocityLimit)
                        rb.angularVelocity *= 0.8f;
                }
            }

            if (bulbRigidbody != null)
            {
                if (bulbRigidbody.velocity.magnitude > velocityLimit * 2)
                    bulbRigidbody.velocity *= 0.6f;
                
                if (bulbRigidbody.angularVelocity.magnitude > angularVelocityLimit * 2)
                    bulbRigidbody.angularVelocity = Vector3.zero;            
            }
        }
    }

    private void OnDrawGizmos()
    {
        Vector3 anchorPos = transform.position + Vector3.up * (segments * segmentLength);
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(anchorPos, ropeThickness * 1.2f);

        Gizmos.color = Color.gray;
        Vector3 lastPos = anchorPos;
        for (int i = 0; i < segments; i++)
        {
            Vector3 segPos = anchorPos + Vector3.down * segmentLength * (i + 1);
            Gizmos.DrawLine(lastPos, segPos);
            Gizmos.DrawSphere(segPos, ropeThickness);
            lastPos = segPos;
        }

        Vector3 bulbPos = anchorPos + Vector3.down * segmentLength * segments;
        Gizmos.color = new Color(1f, 0.6f, 0.15f, 1f); // orange/yellow
        Gizmos.DrawSphere(bulbPos, 0.15f);
    }

}
