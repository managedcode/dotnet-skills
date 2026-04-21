# Common HoloLens Scenarios

## Scenario: Spatial Anchors for Persistent Holograms

Place holograms that persist across sessions using Azure Spatial Anchors:

```csharp
public class PersistentHologramManager : MonoBehaviour
{
    private CloudSpatialAnchorSession _anchorSession;
    private readonly Dictionary<string, CloudSpatialAnchor> _anchors = new();

    public async Task InitializeAsync(string accountId, string accountKey, string accountDomain)
    {
        _anchorSession = new CloudSpatialAnchorSession();
        _anchorSession.Configuration.AccountId = accountId;
        _anchorSession.Configuration.AccountKey = accountKey;
        _anchorSession.Configuration.AccountDomain = accountDomain;

        _anchorSession.AnchorLocated += OnAnchorLocated;
        _anchorSession.LocateAnchorsCompleted += OnLocateAnchorsCompleted;

        _anchorSession.Start();
    }

    public async Task<string> PlaceHologramAsync(GameObject hologram, Pose worldPose)
    {
        // Create native anchor
        var nativeAnchor = hologram.AddComponent<WorldAnchor>();

        // Create cloud anchor
        var cloudAnchor = new CloudSpatialAnchor
        {
            LocalAnchor = nativeAnchor.GetNativeSpatialAnchorPtr()
        };

        // Wait for enough spatial data
        while (!_anchorSession.IsReadyForCreate)
        {
            await Task.Delay(100);
            float progress = _anchorSession.SessionStatus.RecommendedForCreateProgress;
            Debug.Log($"Spatial data collection: {progress:P0}");
        }

        await _anchorSession.CreateAnchorAsync(cloudAnchor);
        _anchors[cloudAnchor.Identifier] = cloudAnchor;

        return cloudAnchor.Identifier;
    }

    public void LocateSavedAnchors(IEnumerable<string> anchorIds)
    {
        var criteria = new AnchorLocateCriteria
        {
            Identifiers = anchorIds.ToArray(),
            Strategy = LocateStrategy.AnyStrategy
        };
        _anchorSession.CreateWatcher(criteria);
    }

    private void OnAnchorLocated(object sender, AnchorLocatedEventArgs args)
    {
        if (args.Status == LocateAnchorStatus.Located)
        {
            var anchor = args.Anchor;
            // Restore hologram at anchor position
            UnityDispatcher.InvokeOnMainThread(() =>
            {
                RestoreHologramAtAnchor(anchor);
            });
        }
    }
}
```

## Scenario: Hand Menu with MRTK

Create a hand-attached menu that follows the user's palm:

```csharp
public class HandMenuController : MonoBehaviour
{
    [SerializeField] private GameObject menuRoot;
    [SerializeField] private float activationAngle = 30f;
    [SerializeField] private Handedness targetHand = Handedness.Left;

    private IMixedRealityHandJointService _jointService;
    private bool _isMenuActive;

    private void Start()
    {
        _jointService = CoreServices.GetInputSystemDataProvider<IMixedRealityHandJointService>();
    }

    private void Update()
    {
        if (_jointService == null) return;

        if (_jointService.IsHandTracked(targetHand))
        {
            var palmPose = _jointService.RequestJointTransform(TrackedHandJoint.Palm, targetHand);
            UpdateMenuVisibility(palmPose);

            if (_isMenuActive)
            {
                PositionMenu(palmPose);
            }
        }
        else
        {
            HideMenu();
        }
    }

    private void UpdateMenuVisibility(Transform palmTransform)
    {
        // Check if palm is facing user (gazing at palm)
        var headPosition = CameraCache.Main.transform.position;
        var palmToHead = (headPosition - palmTransform.position).normalized;
        var palmNormal = palmTransform.up;

        float angle = Vector3.Angle(palmNormal, palmToHead);

        if (angle < activationAngle && !_isMenuActive)
        {
            ShowMenu();
        }
        else if (angle > activationAngle + 10f && _isMenuActive)
        {
            HideMenu();
        }
    }

    private void PositionMenu(Transform palmTransform)
    {
        menuRoot.transform.position = palmTransform.position + palmTransform.up * 0.1f;
        menuRoot.transform.rotation = Quaternion.LookRotation(
            palmTransform.forward,
            CameraCache.Main.transform.position - menuRoot.transform.position
        );
    }

    private void ShowMenu()
    {
        _isMenuActive = true;
        menuRoot.SetActive(true);
    }

    private void HideMenu()
    {
        _isMenuActive = false;
        menuRoot.SetActive(false);
    }
}
```

## Scenario: Spatial Mapping for Physics

Use spatial mapping meshes for hologram physics interactions:

```csharp
public class SpatialPhysicsManager : MonoBehaviour, IMixedRealitySpatialAwarenessObservationHandler<SpatialAwarenessMeshObject>
{
    [SerializeField] private PhysicMaterial surfaceMaterial;
    [SerializeField] private LayerMask spatialMeshLayer;

    private readonly Dictionary<int, MeshCollider> _meshColliders = new();

    private void OnEnable()
    {
        CoreServices.SpatialAwarenessSystem?.RegisterHandler<IMixedRealitySpatialAwarenessObservationHandler<SpatialAwarenessMeshObject>>(this);
    }

    private void OnDisable()
    {
        CoreServices.SpatialAwarenessSystem?.UnregisterHandler<IMixedRealitySpatialAwarenessObservationHandler<SpatialAwarenessMeshObject>>(this);
    }

    public void OnObservationAdded(MixedRealitySpatialAwarenessEventData<SpatialAwarenessMeshObject> eventData)
    {
        var meshObject = eventData.SpatialObject;
        AddPhysicsToMesh(meshObject);
    }

    public void OnObservationUpdated(MixedRealitySpatialAwarenessEventData<SpatialAwarenessMeshObject> eventData)
    {
        var meshObject = eventData.SpatialObject;
        UpdateMeshPhysics(meshObject);
    }

    public void OnObservationRemoved(MixedRealitySpatialAwarenessEventData<SpatialAwarenessMeshObject> eventData)
    {
        if (_meshColliders.TryGetValue(eventData.Id, out var collider))
        {
            Destroy(collider);
            _meshColliders.Remove(eventData.Id);
        }
    }

    private void AddPhysicsToMesh(SpatialAwarenessMeshObject meshObject)
    {
        var meshFilter = meshObject.Filter;
        if (meshFilter == null || meshFilter.sharedMesh == null) return;

        var collider = meshObject.GameObject.AddComponent<MeshCollider>();
        collider.sharedMesh = meshFilter.sharedMesh;
        collider.material = surfaceMaterial;
        meshObject.GameObject.layer = (int)Mathf.Log(spatialMeshLayer.value, 2);

        _meshColliders[meshObject.Id] = collider;
    }

    private void UpdateMeshPhysics(SpatialAwarenessMeshObject meshObject)
    {
        if (_meshColliders.TryGetValue(meshObject.Id, out var collider))
        {
            collider.sharedMesh = meshObject.Filter.sharedMesh;
        }
    }
}
```

## Scenario: Eye Tracking for Gaze Selection

Implement eye-gaze targeting for accessible interaction:

```csharp
public class EyeGazeSelector : MonoBehaviour
{
    [SerializeField] private float dwellTimeThreshold = 0.8f;
    [SerializeField] private float gazeStabilityRadius = 0.02f;

    private IMixedRealityEyeGazeProvider _eyeGazeProvider;
    private GameObject _currentTarget;
    private float _dwellTimer;
    private Vector3 _lastGazePoint;

    private void Start()
    {
        _eyeGazeProvider = CoreServices.InputSystem?.EyeGazeProvider;

        if (_eyeGazeProvider == null)
        {
            Debug.LogWarning("Eye tracking not available. Falling back to head gaze.");
        }
    }

    private void Update()
    {
        if (!IsEyeTrackingAvailable())
        {
            UseHeadGazeFallback();
            return;
        }

        ProcessEyeGaze();
    }

    private bool IsEyeTrackingAvailable()
    {
        return _eyeGazeProvider != null && _eyeGazeProvider.IsEyeTrackingEnabled;
    }

    private void ProcessEyeGaze()
    {
        var gazeOrigin = _eyeGazeProvider.GazeOrigin;
        var gazeDirection = _eyeGazeProvider.GazeDirection;

        if (Physics.Raycast(gazeOrigin, gazeDirection, out var hit, 10f))
        {
            var hitObject = hit.collider.gameObject;

            if (hitObject != _currentTarget)
            {
                OnGazeTargetChanged(_currentTarget, hitObject);
                _currentTarget = hitObject;
                _dwellTimer = 0f;
            }
            else if (IsGazeStable(hit.point))
            {
                _dwellTimer += Time.deltaTime;

                if (_dwellTimer >= dwellTimeThreshold)
                {
                    OnDwellComplete(_currentTarget);
                    _dwellTimer = 0f;
                }
            }

            _lastGazePoint = hit.point;
        }
        else
        {
            if (_currentTarget != null)
            {
                OnGazeTargetChanged(_currentTarget, null);
                _currentTarget = null;
            }
        }
    }

    private bool IsGazeStable(Vector3 currentPoint)
    {
        return Vector3.Distance(currentPoint, _lastGazePoint) < gazeStabilityRadius;
    }

    private void OnGazeTargetChanged(GameObject previous, GameObject current)
    {
        previous?.GetComponent<IGazeTarget>()?.OnGazeExit();
        current?.GetComponent<IGazeTarget>()?.OnGazeEnter();
    }

    private void OnDwellComplete(GameObject target)
    {
        target?.GetComponent<IGazeTarget>()?.OnDwellActivate();
    }

    private void UseHeadGazeFallback()
    {
        var headRay = new Ray(
            CameraCache.Main.transform.position,
            CameraCache.Main.transform.forward
        );
        // Similar raycast logic with head gaze
    }
}

public interface IGazeTarget
{
    void OnGazeEnter();
    void OnGazeExit();
    void OnDwellActivate();
}
```

## Scenario: Remote Rendering Integration

Stream high-fidelity 3D content from Azure Remote Rendering:

```csharp
public class RemoteRenderingManager : MonoBehaviour
{
    private RemoteRenderingClient _client;
    private RenderingSession _session;
    private RemoteManager _remoteManager;

    public async Task ConnectAsync(RemoteRenderingConfig config)
    {
        var sessionConfig = new RenderingSessionCreationOptions
        {
            MaxLeaseInMinutes = 30,
            Size = RenderingSessionVmSize.Standard
        };

        _client = ApiClientFactory.CreateRemoteRenderingClient(
            config.AccountId,
            config.AccountKey,
            config.AccountDomain
        );

        _session = await _client.CreateNewRenderingSessionAsync(sessionConfig);

        await _session.ConnectAsync(new RendererInitOptions
        {
            SessionId = _session.SessionId
        });

        _remoteManager = _session.Connection.RemoteManager;
    }

    public async Task<Entity> LoadModelAsync(string sasUrl)
    {
        var loadParams = new LoadModelFromSasOptions
        {
            ModelUri = sasUrl
        };

        var result = await _remoteManager.LoadModelFromSasAsync(loadParams);
        return result.Root;
    }

    public void PositionModel(Entity modelRoot, Pose worldPose)
    {
        modelRoot.Position = worldPose.position.ToRemote();
        modelRoot.Rotation = worldPose.rotation.ToRemote();
    }

    public async Task DisconnectAsync()
    {
        if (_session != null)
        {
            await _session.StopAsync();
            _session = null;
        }
    }
}
```

## Scenario: Voice Commands with MRTK

Implement voice-activated commands:

```csharp
public class VoiceCommandHandler : MonoBehaviour, IMixedRealitySpeechHandler
{
    private readonly Dictionary<string, Action> _commands = new();

    private void Start()
    {
        RegisterCommands();

        // Ensure speech system is enabled
        if (CoreServices.InputSystem?.IsRegistered(gameObject) == false)
        {
            CoreServices.InputSystem?.RegisterHandler<IMixedRealitySpeechHandler>(this);
        }
    }

    private void RegisterCommands()
    {
        _commands["select"] = () => SelectCurrentTarget();
        _commands["menu"] = () => ToggleMainMenu();
        _commands["reset"] = () => ResetScene();
        _commands["help"] = () => ShowHelp();
        _commands["place here"] = () => PlaceObjectAtGaze();
    }

    public void OnSpeechKeywordRecognized(SpeechEventData eventData)
    {
        var keyword = eventData.Command.Keyword.ToLowerInvariant();

        if (_commands.TryGetValue(keyword, out var action))
        {
            eventData.Use();
            action.Invoke();

            // Provide audio feedback
            AudioFeedback.PlayConfirmation();
        }
    }

    private void SelectCurrentTarget()
    {
        var focusProvider = CoreServices.InputSystem?.FocusProvider;
        var focusedObject = focusProvider?.GetFocusedObject(
            focusProvider.PrimaryPointer
        );

        focusedObject?.GetComponent<ISelectable>()?.Select();
    }

    private void ToggleMainMenu() { /* Implementation */ }
    private void ResetScene() { /* Implementation */ }
    private void ShowHelp() { /* Implementation */ }
    private void PlaceObjectAtGaze() { /* Implementation */ }
}
```

## Scenario: QR Code Tracking

Detect and track QR codes for content anchoring:

```csharp
public class QRCodeTracker : MonoBehaviour
{
    private QRCodeWatcher _watcher;
    private readonly Dictionary<Guid, QRCodeAnchor> _trackedCodes = new();

    public event Action<QRCodeAnchor> OnQRCodeDetected;
    public event Action<Guid> OnQRCodeLost;

    private async void Start()
    {
        var accessStatus = await QRCodeWatcher.RequestAccessAsync();

        if (accessStatus == QRCodeWatcherAccessStatus.Allowed)
        {
            _watcher = new QRCodeWatcher();
            _watcher.Added += OnQRCodeAdded;
            _watcher.Updated += OnQRCodeUpdated;
            _watcher.Removed += OnQRCodeRemoved;
            _watcher.Start();
        }
        else
        {
            Debug.LogError("QR code tracking access denied");
        }
    }

    private void OnQRCodeAdded(object sender, QRCodeAddedEventArgs args)
    {
        UnityDispatcher.InvokeOnMainThread(() =>
        {
            var code = args.Code;
            var anchor = CreateAnchorForCode(code);
            _trackedCodes[code.Id] = anchor;
            OnQRCodeDetected?.Invoke(anchor);
        });
    }

    private void OnQRCodeUpdated(object sender, QRCodeUpdatedEventArgs args)
    {
        UnityDispatcher.InvokeOnMainThread(() =>
        {
            if (_trackedCodes.TryGetValue(args.Code.Id, out var anchor))
            {
                UpdateAnchorPose(anchor, args.Code);
            }
        });
    }

    private void OnQRCodeRemoved(object sender, QRCodeRemovedEventArgs args)
    {
        UnityDispatcher.InvokeOnMainThread(() =>
        {
            if (_trackedCodes.Remove(args.Code.Id))
            {
                OnQRCodeLost?.Invoke(args.Code.Id);
            }
        });
    }

    private QRCodeAnchor CreateAnchorForCode(QRCode code)
    {
        var spatialGraphNode = SpatialGraphInteropPreview.CreateLocatorForNode(
            code.SpatialGraphNodeId
        );

        return new QRCodeAnchor
        {
            Id = code.Id,
            Data = code.Data,
            PhysicalSize = code.PhysicalSideLength,
            SpatialNode = spatialGraphNode
        };
    }
}

public class QRCodeAnchor
{
    public Guid Id { get; set; }
    public string Data { get; set; }
    public float PhysicalSize { get; set; }
    public object SpatialNode { get; set; }
}
```

## Scenario: Shared Experience with Photon

Enable multi-user collaboration in mixed reality:

```csharp
public class SharedExperienceManager : MonoBehaviourPunCallbacks
{
    [SerializeField] private GameObject localAvatarPrefab;
    [SerializeField] private GameObject remoteAvatarPrefab;

    private Dictionary<int, GameObject> _remoteAvatars = new();

    public async Task JoinSharedSessionAsync(string roomName)
    {
        PhotonNetwork.ConnectUsingSettings();

        while (!PhotonNetwork.IsConnectedAndReady)
        {
            await Task.Yield();
        }

        var roomOptions = new RoomOptions
        {
            MaxPlayers = 8,
            IsVisible = true
        };

        PhotonNetwork.JoinOrCreateRoom(roomName, roomOptions, TypedLobby.Default);
    }

    public override void OnJoinedRoom()
    {
        SpawnLocalAvatar();
        SynchronizeSpatialAnchor();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"Player {newPlayer.NickName} joined");
    }

    private void SpawnLocalAvatar()
    {
        var headPose = CameraCache.Main.transform;
        var avatar = PhotonNetwork.Instantiate(
            localAvatarPrefab.name,
            headPose.position,
            headPose.rotation
        );
    }

    private void SynchronizeSpatialAnchor()
    {
        // Share Azure Spatial Anchor ID with other users
        // so all users align to the same world coordinate system
        if (PhotonNetwork.IsMasterClient)
        {
            // Master creates and shares anchor
            CreateAndShareWorldAnchor();
        }
        else
        {
            // Others locate the shared anchor
            LocateSharedWorldAnchor();
        }
    }
}
```
