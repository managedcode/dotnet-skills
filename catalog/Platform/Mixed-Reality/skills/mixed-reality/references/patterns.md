# MRTK and OpenXR Patterns

## MRTK Architecture Patterns

### Service-Based Architecture

MRTK uses a service locator pattern for core subsystems. Access services through the central service registry:

```csharp
// Accessing MRTK services
var inputSystem = CoreServices.InputSystem;
var spatialAwarenessSystem = CoreServices.SpatialAwarenessSystem;
var diagnosticsSystem = CoreServices.DiagnosticsSystem;
```

### Input Action Mapping

Define input actions in configuration rather than hardcoding gesture detection:

```csharp
// Define input action in MRTK configuration profile
// Then handle in code via IMixedRealityInputHandler
public class GrabHandler : MonoBehaviour, IMixedRealityInputHandler
{
    public void OnInputDown(InputEventData eventData)
    {
        if (eventData.MixedRealityInputAction == grabAction)
        {
            // Handle grab start
        }
    }

    public void OnInputUp(InputEventData eventData)
    {
        if (eventData.MixedRealityInputAction == grabAction)
        {
            // Handle grab release
        }
    }
}
```

### Spatial Awareness Observer Pattern

Subscribe to spatial mesh updates rather than polling:

```csharp
public class SpatialMeshHandler : MonoBehaviour, IMixedRealitySpatialAwarenessObservationHandler<SpatialAwarenessMeshObject>
{
    public void OnObservationAdded(MixedRealitySpatialAwarenessEventData<SpatialAwarenessMeshObject> eventData)
    {
        // New mesh detected
        var meshObject = eventData.SpatialObject;
        ProcessNewMesh(meshObject);
    }

    public void OnObservationUpdated(MixedRealitySpatialAwarenessEventData<SpatialAwarenessMeshObject> eventData)
    {
        // Existing mesh updated
    }

    public void OnObservationRemoved(MixedRealitySpatialAwarenessEventData<SpatialAwarenessMeshObject> eventData)
    {
        // Mesh no longer tracked
    }
}
```

### Profile Configuration Pattern

Use scriptable object profiles for runtime configuration:

```csharp
// Create custom profile inheriting from base
[CreateAssetMenu(fileName = "CustomHandTrackingProfile", menuName = "MRTK/Custom Hand Tracking Profile")]
public class CustomHandTrackingProfile : BaseMixedRealityProfile
{
    [SerializeField]
    private float gestureThreshold = 0.8f;

    [SerializeField]
    private HandTrackingMode trackingMode = HandTrackingMode.Full;

    public float GestureThreshold => gestureThreshold;
    public HandTrackingMode TrackingMode => trackingMode;
}
```

## OpenXR Patterns

### Feature Plugin Pattern

Implement OpenXR features as discrete plugins:

```csharp
#if UNITY_OPENXR
[OpenXRFeature(
    UiName = "Custom Hand Tracking",
    BuildTargetGroups = new[] { BuildTargetGroup.Standalone, BuildTargetGroup.WSA },
    Company = "YourCompany",
    Version = "1.0.0",
    FeatureId = "com.yourcompany.openxr.handtracking"
)]
public class CustomHandTrackingFeature : OpenXRFeature
{
    protected override bool OnInstanceCreate(ulong instance)
    {
        // Initialize OpenXR extension
        return base.OnInstanceCreate(instance);
    }

    protected override void OnSubsystemStart()
    {
        // Subsystem is starting
    }

    protected override void OnSubsystemStop()
    {
        // Subsystem is stopping
    }
}
#endif
```

### Action-Based Input Pattern

Use OpenXR action bindings for cross-platform input:

```csharp
// Define action map in code or via InputActionAsset
var selectAction = new InputAction("Select", InputActionType.Button);
selectAction.AddBinding("<XRController>{RightHand}/trigger");
selectAction.AddBinding("<HandInteraction>{RightHand}/select");

selectAction.performed += ctx => OnSelectPerformed();
selectAction.canceled += ctx => OnSelectCanceled();
selectAction.Enable();
```

### Session State Management

Handle OpenXR session lifecycle properly:

```csharp
public class XRSessionManager : MonoBehaviour
{
    private void OnEnable()
    {
        XRInputSubsystem.boundaryChanged += OnBoundaryChanged;
    }

    private void OnDisable()
    {
        XRInputSubsystem.boundaryChanged -= OnBoundaryChanged;
    }

    public void HandleSessionStateChange(XRSessionState state)
    {
        switch (state)
        {
            case XRSessionState.Ready:
                InitializeXRContent();
                break;
            case XRSessionState.Focused:
                ResumeExperience();
                break;
            case XRSessionState.Visible:
                PauseInteractiveContent();
                break;
            case XRSessionState.Stopping:
                SaveSessionState();
                break;
        }
    }
}
```

## Cross-Cutting Patterns

### Dependency Injection for XR Services

Abstract XR services behind interfaces for testability:

```csharp
public interface ISpatialAnchorService
{
    Task<SpatialAnchor> CreateAnchorAsync(Pose pose, string identifier);
    Task<SpatialAnchor> LoadAnchorAsync(string identifier);
    Task DeleteAnchorAsync(string identifier);
}

public class AzureSpatialAnchorsService : ISpatialAnchorService
{
    private readonly CloudSpatialAnchorSession _session;

    public async Task<SpatialAnchor> CreateAnchorAsync(Pose pose, string identifier)
    {
        var cloudAnchor = new CloudSpatialAnchor();
        cloudAnchor.LocalAnchor = CreateNativeAnchor(pose);
        cloudAnchor.AppProperties["id"] = identifier;

        await _session.CreateAnchorAsync(cloudAnchor);
        return new SpatialAnchor(cloudAnchor);
    }
}
```

### Object Pooling for XR Performance

Pool frequently instantiated objects to avoid GC pressure:

```csharp
public class XRObjectPool<T> where T : Component
{
    private readonly Queue<T> _available = new();
    private readonly T _prefab;
    private readonly Transform _poolParent;

    public T Get()
    {
        if (_available.Count > 0)
        {
            var obj = _available.Dequeue();
            obj.gameObject.SetActive(true);
            return obj;
        }
        return Object.Instantiate(_prefab, _poolParent);
    }

    public void Return(T obj)
    {
        obj.gameObject.SetActive(false);
        _available.Enqueue(obj);
    }
}
```

### Async Loading Pattern for MR Content

Load heavy assets asynchronously to maintain frame rate:

```csharp
public class MRContentLoader
{
    public async Task<GameObject> LoadHologramAsync(string assetPath, CancellationToken ct)
    {
        var handle = Addressables.LoadAssetAsync<GameObject>(assetPath);

        while (!handle.IsDone)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Yield();
        }

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            return Object.Instantiate(handle.Result);
        }

        throw new InvalidOperationException($"Failed to load asset: {assetPath}");
    }
}
```

### Graceful Degradation Pattern

Handle missing device capabilities gracefully:

```csharp
public class CapabilityManager
{
    public bool HandTrackingAvailable { get; private set; }
    public bool EyeTrackingAvailable { get; private set; }
    public bool SpatialMappingAvailable { get; private set; }

    public void DetectCapabilities()
    {
        HandTrackingAvailable = IsSubsystemAvailable<XRHandSubsystem>();
        EyeTrackingAvailable = IsSubsystemAvailable<XREyeTrackingSubsystem>();
        SpatialMappingAvailable = IsSubsystemAvailable<XRMeshSubsystem>();
    }

    private bool IsSubsystemAvailable<T>() where T : class, ISubsystem
    {
        var descriptors = new List<SubsystemDescriptorBase>();
        SubsystemManager.GetSubsystemDescriptors(descriptors);
        return descriptors.Any(d => d is T);
    }

    public void ConfigureInputForCapabilities(InputController controller)
    {
        if (HandTrackingAvailable)
        {
            controller.EnableHandTracking();
        }
        else
        {
            controller.EnableControllerFallback();
        }
    }
}
```
