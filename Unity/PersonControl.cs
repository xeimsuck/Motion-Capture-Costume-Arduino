using UnityEngine;

// boneMap:
// 1: Head
// 2: Chest
// 3: Spine
// 4: Left Hand
// 5: Right Hand
// 6: Left Upper Leg
// 7: Right Upper Leg

public class PersonControl : MonoBehaviour
{
    [Header("Data source")]
    public ArduinoSensorReader reader;

    [Header("Animator / bones")]
    public Animator animator;
    public HumanBodyBones[] boneMap;
    [Tooltip("Slerp speed for rotation application")]
    public float rotSlerpSpeed = 20f;

    [Header("Root movement")]
    public int pelvisSensorIndex = 0;
    public enum RootMode { None, IntegrateAccel, StepBased }
    public RootMode rootMode = RootMode.StepBased;

    [Header("Step-based settings")]
    public float stepThreshold = 1.2f;
    public float stepLength = 0.3f; 

    [Header("Integrate settings")]
    public float accelDamping = 0.99f;
    public float gravityBlendSpeed = 1f; 

    private Transform[] boneTransforms;
    private Quaternion[] sensorToBoneOffset;
    private bool calibrated = false;

    private Vector3 rootVelocity = Vector3.zero;
    private Vector3 estimatedGravity = Vector3.up * 9.81f;
    private bool stepDetected = false;

    void Start()
    {
        if (reader == null) Debug.LogError("MocapMapper: reader не назначен.");
        if (animator == null) Debug.LogError("MocapMapper: animator не назначен.");
        if (boneMap == null || boneMap.Length == 0) Debug.LogError("MocapMapper: boneMap не задан.");

        boneTransforms = new Transform[boneMap.Length];
        for (int i = 0; i < boneMap.Length; i++)
        {
            boneTransforms[i] = animator.GetBoneTransform(boneMap[i]);
            if (boneTransforms[i] == null)
                Debug.LogWarning($"MocapMapper: костя {boneMap[i]} не найдена в аватаре.");
        }

        sensorToBoneOffset = new Quaternion[boneMap.Length];
    }

    public void Calibrate()
    {
        if (reader == null || reader.sensors == null) return;
        int n = Mathf.Min(reader.sensors.Length, boneTransforms.Length);

        for (int i = 0; i < n; i++)
        {
            var s = reader.sensors[i];
            Quaternion sensorQ = SensorQuaternion(s);
            var bone = boneTransforms[i];
            if (bone == null) { sensorToBoneOffset[i] = Quaternion.identity; continue; }

            sensorToBoneOffset[i] = bone.rotation * Quaternion.Inverse(sensorQ);
        }

        calibrated = true;
        rootVelocity = Vector3.zero;
        if (pelvisSensorIndex >= 0 && pelvisSensorIndex < boneTransforms.Length && boneTransforms[pelvisSensorIndex] != null)
            estimatedGravity = boneTransforms[pelvisSensorIndex].rotation * Vector3.up * 9.81f;

        Debug.Log("MocapMapper: calibrated");
    }

    void LateUpdate()
    {
        if (!calibrated || reader == null || reader.sensors == null) return;

        ApplySensorRotations();
        ApplyRootMovement();
    }

    void ApplySensorRotations()
    {
        int n = Mathf.Min(reader.sensors.Length, boneTransforms.Length);

        for (int i = 0; i < n; i++)
        {
            var bone = boneTransforms[i];
            if (bone == null) continue;

            Quaternion sensorQ = SensorQuaternion(reader.sensors[i]);

            Quaternion desiredWorld = sensorToBoneOffset[i] * sensorQ;

            Transform parent = bone.parent;
            Quaternion desiredLocal = parent != null ? Quaternion.Inverse(parent.rotation) * desiredWorld : desiredWorld;

            bone.localRotation = Quaternion.Slerp(bone.localRotation, desiredLocal, Time.deltaTime * rotSlerpSpeed);
        }
    }

    void ApplyRootMovement()
    {
        if (rootMode == RootMode.None) return;
        if (pelvisSensorIndex < 0 || pelvisSensorIndex >= reader.sensors.Length) return;

        var s = reader.sensors[pelvisSensorIndex];

        if (rootMode == RootMode.StepBased)
        {
            float vertical = s.accY;
            if (vertical > stepThreshold && !stepDetected)
            {
                stepDetected = true;
                transform.position += transform.forward * stepLength;
            }
            else if (vertical < stepThreshold * 0.5f)
            {
                stepDetected = false;
            }
        }
        else if (rootMode == RootMode.IntegrateAccel)
        {
            Vector3 accelLocal = new Vector3(s.accX, s.accY, s.accZ);
            Transform pelvisBone = boneTransforms[pelvisSensorIndex];
            if (pelvisBone == null) return;

            Vector3 accelWorld = pelvisBone.rotation * accelLocal;

            estimatedGravity = Vector3.Lerp(estimatedGravity, accelWorld, Time.deltaTime * gravityBlendSpeed);

            Vector3 linear = accelWorld - estimatedGravity;

            rootVelocity += linear * Time.deltaTime;
            rootVelocity *= accelDamping; 

            transform.position += rootVelocity * Time.deltaTime;
        }
    }

    private Quaternion SensorQuaternion(ArduinoSensorReader.SensorData s)
    {
        return Quaternion.Euler(s.rotX, s.rotY, s.rotZ);
    }
}
